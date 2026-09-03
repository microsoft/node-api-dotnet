// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Indicates the type of <see cref="JSValueScope" /> within the hierarchy of scopes.
/// </summary>
internal enum JSValueScopeType
{
    /// <summary>
    /// References a <see cref="JSRuntimeContext" /> and marks the call/context boundary for a JS
    /// environment. Opens no napi handle scope; it is the scope a <see cref="JSValue" /> falls back
    /// to for validity when no handle scope is open.
    /// </summary>
    RuntimeContext,

    /// <summary>
    /// Opens a napi handle scope nested within a parent scope, from which it inherits the context.
    /// </summary>
    Handle,

    /// <summary>
    /// Opens an escapable napi handle scope nested within a parent scope, and can escape one value
    /// to the parent scope.
    /// </summary>
    Escapable,
}

/// <summary>
/// A scope that controls the lifetime of JS values. When the scope is disposed, any
/// JS values created within the scope are released unless they are held by a strong
/// <see cref="JSReference" />.
/// </summary>
/// <remarks>
/// Every call from JS to .NET creates a separate scope for the duration of the call.
/// That means any JS values created during the call are released when the call returns,
/// unless they are returned to JS or held by a strong <see cref="JSReference" />.
/// </remarks>
public sealed class JSValueScope : IDisposable
{
    private readonly JSValueScope? _parentScope;
#pragma warning disable IDE0032 // Use auto property
    private readonly napi_env _env;
#pragma warning restore IDE0032
    private readonly SynchronizationContext? _previousSyncContext;
    private readonly nint _scopeHandle;
    private bool _disposeRuntimeContextOnClose;

    internal JSValueScopeType ScopeType { get; }

    /// <summary>
    /// Gets the current JS value scope.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">No scope was established for the current
    /// thread.</exception>
    public static JSValueScope Current => CurrentOrNull ??
        throw new JSInvalidThreadAccessException(currentScope: null);

    /// <summary>
    /// Gets the current JS value scope for the calling thread, or null if no scope is
    /// established. Unlike <see cref="Current"/>, this never throws, so it is safe to use from
    /// contexts that must not throw, such as finalizers.
    /// </summary>
    [field: ThreadStatic]
    internal static JSValueScope? CurrentOrNull { get; private set; }

    /// <summary>
    /// Gets the environment handle for the scope, or throws an exception if the scope is
    /// disposed or access from the current thread is invalid.
    /// </summary>
    /// <exception cref="JSValueScopeClosedException">The scope has been closed.</exception>
    /// <exception cref="JSInvalidThreadAccessException">The scope is not valid on the current
    /// thread.</exception>
    public napi_env EnvironmentHandle
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfInvalidThreadAccess();
            return _env;
        }
    }

    public static explicit operator napi_env(JSValueScope scope)
    {
        if (scope is null) throw new ArgumentNullException(nameof(scope));
        return scope.EnvironmentHandle;
    }

    /// <summary>
    /// Gets the environment handle without checking whether the scope is disposed or
    /// whether access from the current thread is valid. WARNING: This must only be used
    /// to avoid redundant handle checks when there is another (checked) access to
    /// <see cref="EnvironmentHandle" /> for the same call.
    /// </summary>
    internal napi_env UncheckedEnvironmentHandle => _env;

    /// <summary>
    /// Gets the environment handle for the current thread scope, or throws an exception if
    /// there is no environment for the current thread. For use only with static operations
    /// not related to any <see cref="JSValue" />; for value operations use the
    /// <see cref="EnvironmentHandle" /> from the value's <see cref="JSValue.Scope"/> instead.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">No scope was established for the current
    /// thread.</exception>
    internal static napi_env CurrentEnvironmentHandle => Current.EnvironmentHandle;

    internal int ThreadId { get; }

    public bool IsDisposed { get; private set; }

    public JSRuntime Runtime { get; }
    public JSRuntimeContext RuntimeContext { get; }

    internal static JSRuntime CurrentRuntime => Current.Runtime;
    internal static JSRuntimeContext? CurrentRuntimeContext => CurrentOrNull?.RuntimeContext;

    /// <summary>
    /// Holds the instance of the module class for the current module. It is a shared mutable cell
    /// so callback descriptors can capture it during initialization, before the module instance
    /// exists, and observe the instance once it is assigned.
    /// </summary>
    internal StrongBox<object?>? ModuleHolder { get; set; }

    /// <summary>
    /// Gets the instance of the module class for the current module, used as the 'this' argument
    /// for module-level instance members, or null if there is no module class.
    /// </summary>
    public object? Module => ModuleHolder?.Value;

    /// <summary>
    /// Creates a scope that references a <see cref="JSRuntimeContext" /> and marks the call/context
    /// boundary for a JS environment. It opens no napi handle scope.
    /// </summary>
    /// <param name="env">The JS environment handle.</param>
    /// <param name="context">The runtime context to reference. When null it is inherited from the
    /// parent scope, or recovered from the environment instance data.</param>
    public static JSValueScope CreateRuntimeScope(
        napi_env env = default, JSRuntimeContext? context = null)
        => new(env, context);

    /// <summary>
    /// Attempts to create a <see cref="JSValueScopeType.RuntimeContext" /> scope for a callback
    /// entering from JS, returning null when no live context can be resolved for the environment --
    /// for example a retained JS function invoked after its context was disposed. Callback adapters
    /// use it to become a no-op in that case. It never throws: an exception escaping an
    /// <see cref="System.Runtime.InteropServices.UnmanagedCallersOnly" /> callback would cross the
    /// native boundary and terminate the process.
    /// </summary>
    internal static JSValueScope? TryCreateRuntimeScope(napi_env env)
    {
        try
        {
            // Inherit the current scope's context -- every scope on a thread shares one context per
            // environment -- or recover it from env instance data when no scope is open yet.
            JSRuntimeContext? context = CurrentOrNull?.RuntimeContext ?? JSRuntimeContext.FromEnv(env);
            return context is { IsDisposed: false } ? new JSValueScope(env, context) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a <see cref="JSValueScopeType.RuntimeContext" /> scope that starts a fresh module
    /// boundary: it references the same <see cref="JSRuntimeContext" /> (inherited or supplied) but
    /// begins a new module holder, so each loaded module resolves its own module instance via
    /// <see cref="Module" />.
    /// </summary>
    /// <param name="env">The JS environment handle.</param>
    /// <param name="context">The runtime context to reference. When null it is inherited from the
    /// parent scope, or recovered from the environment instance data.</param>
    public static JSValueScope CreateModuleScope(
        napi_env env = default, JSRuntimeContext? context = null)
        => new(env, context, moduleBoundary: true);

    /// <summary>
    /// Creates a napi handle scope nested within the current scope. JS values created within it
    /// are released when it is disposed, unless held by a <see cref="JSReference" />.
    /// </summary>
    public static JSValueScope CreateHandleScope() => new(JSValueScopeType.Handle);

    /// <summary>
    /// Creates an escapable napi handle scope nested within the current scope. One value may be
    /// promoted to the parent scope with <see cref="Escape" />.
    /// </summary>
    public static JSValueScope CreateEscapableScope() => new(JSValueScopeType.Escapable);

    /// <summary>
    /// Creates a <see cref="JSValueScopeType.RuntimeContext" /> scope that references an existing
    /// <see cref="JSRuntimeContext" /> (it never creates one). When <paramref name="moduleBoundary" />
    /// is true it starts a fresh module holder even if the context is inherited from the parent.
    /// </summary>
    private JSValueScope(napi_env env, JSRuntimeContext? context, bool moduleBoundary = false)
    {
        ScopeType = JSValueScopeType.RuntimeContext;
        _parentScope = CurrentOrNull;

        // Inherit the parent scope's context, else recover it from the env instance data.
        context ??= _parentScope?.RuntimeContext
            ?? JSRuntimeContext.FromEnv(env)
            ?? throw new InvalidOperationException(
                "A runtime context could not be resolved for the scope.");

        // Every scope on a thread's stack shares one runtime context (one per environment per loaded
        // module -- separate modules have separate thread-static scope stacks), so a nested scope must
        // continue its parent's context. A mismatch is a caller error, not something to reconcile.
        if (_parentScope is not null && context != _parentScope.RuntimeContext)
        {
            throw new InvalidOperationException(
                "A nested value scope must use the same runtime context as its parent scope.");
        }

        // A disposed context's environment is torn down; entering it would call Node-API on a
        // dead env, which the scope's own unchecked handle would not catch.
        if (context.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(JSRuntimeContext));
        }

        // A supplied env must match the resolved context — whether passed explicitly (a root
        // boundary: host, AOT module, or embedding) or inherited from the parent — otherwise this
        // scope would wrap handles from a different environment.
        if (!env.IsNull && env != context.UncheckedEnvironmentHandle)
        {
            throw new ArgumentException(
                "Environment does not match the runtime context.", nameof(env));
        }

        _env = context.UncheckedEnvironmentHandle;

        // A runtime context is bound to the thread that created it (its env's JS thread); entering it
        // from another thread would allow napi to be called off that thread.
        if (context.OwningThreadId != Environment.CurrentManagedThreadId)
        {
            throw new JSInvalidThreadAccessException(
                _parentScope,
                "A runtime context may be entered only on the thread that created it.");
        }

        ThreadId = Environment.CurrentManagedThreadId;
        Runtime = context.Runtime;

        // A nested runtime scope that continues the parent's context inherits its module holder; a
        // module boundary, or a root with a new/explicit context, starts a fresh one so each loaded
        // module resolves its own module instance.
        ModuleHolder = !moduleBoundary && _parentScope?.RuntimeContext == context
            ? _parentScope.ModuleHolder
            : new StrongBox<object?>();

        JSValueScope? previousScope = CurrentOrNull;
        try
        {
            CurrentOrNull = this;
            RuntimeContext = context;

            _previousSyncContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(context.SynchronizationContext);
        }
        catch (Exception)
        {
            CurrentOrNull = previousScope;
            throw;
        }
    }

    /// <summary>
    /// Creates a <see cref="JSValueScopeType.Handle" /> or <see cref="JSValueScopeType.Escapable" />
    /// scope that opens a napi handle scope nested within the current scope.
    /// </summary>
    private JSValueScope(JSValueScopeType scopeType)
    {
        ScopeType = scopeType;
        _parentScope = CurrentOrNull ?? throw new InvalidOperationException(
            $"A {scopeType} scope cannot be created without a parent scope.");

        if (_parentScope.IsDisposed)
        {
            throw new InvalidOperationException("Parent scope is disposed.");
        }

        _parentScope.ThrowIfInvalidThreadAccess();
        _env = _parentScope._env;
        ThreadId = _parentScope.ThreadId;
        Runtime = _parentScope.Runtime;
        ModuleHolder = _parentScope.ModuleHolder;

        _scopeHandle = scopeType switch
        {
            JSValueScopeType.Handle
                => Runtime.OpenHandleScope(_env, out napi_handle_scope handleScope)
                   .ThrowIfFailed(handleScope).Handle,
            JSValueScopeType.Escapable
                => Runtime.OpenEscapableHandleScope(
                    _env, out napi_escapable_handle_scope handleScope)
                   .ThrowIfFailed(handleScope).Handle,
            _ => throw new ArgumentException(
                $"Invalid handle scope type: {scopeType}", nameof(scopeType)),
        };

        JSValueScope? previousScope = CurrentOrNull;
        try
        {
            CurrentOrNull = this;
            RuntimeContext = _parentScope.RuntimeContext;
        }
        catch (Exception)
        {
            CurrentOrNull = previousScope;
            throw;
        }
    }

    /// <summary>
    /// Disposes <paramref name="runtimeContext"/> once every value scope open on it has closed, or
    /// immediately if none is open. A dispose request can arrive through a native callback nested
    /// inside open value scopes (JS calling native calling JS…); disposing the context then would
    /// leave those scopes to close their napi handle scopes on a disposed context as they unwind, an
    /// unbalanced close that Node-API rejects. Every scope on the stack shares this one context, so
    /// the innermost open scope is flagged and the outermost disposes the context as it closes.
    /// </summary>
    internal static void DisposeRuntimeContextWhenIdle(JSRuntimeContext runtimeContext)
    {
        if (CurrentOrNull is { } scope)
        {
            scope._disposeRuntimeContextOnClose = true;
        }
        else
        {
            runtimeContext.Dispose();
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        // A scope must be disposed on its creating thread and in reverse order of creation: an
        // off-thread or out-of-order close calls napi on the wrong thread or restores the wrong parent.
        if (CurrentOrNull != this)
        {
            if (CurrentOrNull?._env != _env)
            {
                throw new JSInvalidThreadAccessException(
                    currentScope: CurrentOrNull,
                    targetScope: this,
                    "A value scope must be disposed on the thread that created it.");
            }

            throw new InvalidOperationException(
                "A value scope cannot be disposed while a scope created within it is still open.");
        }

        switch (ScopeType)
        {
            // Fetch the env only where it is used, so disposing a runtime scope after its context
            // is torn down does not throw from the checked handle accessor.
            case JSValueScopeType.Handle:
                Runtime.CloseHandleScope(
                    RuntimeContext.EnvironmentHandle,
                    new napi_handle_scope(_scopeHandle)).ThrowIfFailed();
                break;
            case JSValueScopeType.Escapable:
                Runtime.CloseEscapableHandleScope(
                    RuntimeContext.EnvironmentHandle,
                    new napi_escapable_handle_scope(_scopeHandle)).ThrowIfFailed();
                break;
            default:
                SynchronizationContext.SetSynchronizationContext(_previousSyncContext);
                break;
        }

        IsDisposed = true;
        CurrentOrNull = _parentScope;

        // Carry a deferred context-disposal request (see DisposeRuntimeContextWhenIdle) out to the
        // parent, or -- at the outermost scope, where none of the context's scopes remain open --
        // dispose the context now.
        if (_disposeRuntimeContextOnClose)
        {
            if (_parentScope is null)
            {
                RuntimeContext.Dispose();
            }
            else
            {
                _parentScope._disposeRuntimeContextOnClose = true;
            }
        }
    }

    public JSValue Escape(JSValue value)
    {
        if (_parentScope == null)
            throw new InvalidOperationException("Parent scope must not be null.");

        if (ScopeType != JSValueScopeType.Escapable)
            throw new InvalidOperationException(
                "It can be called only for Escapable value scopes.");

        Runtime.EscapeHandle(
            (napi_env)this,
            new napi_escapable_handle_scope(_scopeHandle),
            (napi_value)value,
            out napi_value result);
        return new JSValue(result, _parentScope);
    }

    /// <summary>
    /// Checks that this scope has not been closed (disposed).
    /// </summary>
    /// <exception cref="JSValueScopeClosedException">The scope is closed.</exception>
    internal void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new JSValueScopeClosedException(scope: this);
        }
    }

    /// <summary>
    /// Checks that the current thread is the thread that is running the JavaScript environment
    /// that this scope is in.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">The scope cannot be accessed from the current
    /// thread.</exception>
    internal void ThrowIfInvalidThreadAccess()
    {
        if (CurrentOrNull?._env != _env)
        {
            throw new JSInvalidThreadAccessException(currentScope: CurrentOrNull, targetScope: this);
        }
    }
}
