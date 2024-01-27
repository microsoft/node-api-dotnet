// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Indicates the type of <see cref="JSValueScope" /> within the hierarchy of scopes.
/// </summary>
public enum JSValueScopeType
{
    /// <summary>
    /// A limited scope without any <see cref="JSRuntimeContext" /> or <see cref="JSModuleContext" />.
    /// Used by the Node API .NET native host to set up callbacks before the managed host is
    /// initialized.
    /// </summary>
    NoContext,

    /// <summary>
    /// A parent scope shared by all (non-AOT) .NET modules loaded in the same process. It has
    /// a <see cref="JSRuntimeContext" /> but no <see cref="JSModuleContext" />.
    /// </summary>
    /// <remarks>
    /// AOT modules do not have any root scope, so each module scope has a separate
    /// <see cref="JSRuntimeContext"/>.
    /// </remarks>
    Root,

    /// <summary>
    /// A scope specific to each module. It inherits the <see cref="JSRuntimeContext" /> from the root
    /// scope, and has a unique <see cref="JSModuleContext" />.
    /// </summary>
    /// <remarks>
    /// AOT modules do not have any root scope, so each module also has a separate
    /// <see cref="JSRuntimeContext"/>.
    /// </remarks>
    Module,

    /// <summary>
    /// Callback scope within a module; inherits context from the module.
    /// </summary>
    Callback,

    /// <summary>
    /// Handle scope within a callback; inherits context from the module.
    /// </summary>
    Handle,

    /// <summary>
    /// Escapable handle scope within a callback; inherits context from the module.
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

    [ThreadStatic] private static JSValueScope? s_currentScope;

    public JSValueScopeType ScopeType { get; }

    /// <summary>
    /// Gets the current JS value scope.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">No scope was established for the current
    /// thread.</exception>
    public static JSValueScope Current => s_currentScope ??
        throw new JSInvalidThreadAccessException(currentScope: null);

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
    /// not related to any <see cref="JSValue" />; for value operations use
    /// <see cref="JSValue.UncheckedEnvironmentHandle" /> instead.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">No scope was established for the current
    /// thread.</exception>
    internal static napi_env CurrentEnvironmentHandle => Current.EnvironmentHandle;

    internal int ThreadId { get; }

    public bool IsDisposed { get; private set; }

    public JSRuntime Runtime { get; }
    public JSRuntimeContext RuntimeContext { get; }
    internal nint RuntimeContextHandle { get; }

    internal static JSRuntime CurrentRuntime => Current.Runtime;
    internal static JSRuntimeContext? CurrentRuntimeContext => s_currentScope?.RuntimeContext;

    public JSModuleContext? ModuleContext { get; internal set; }

    /// <summary>
    /// Creates a new instance of a <see cref="JSValueScope"/> with a specified scope type.
    /// </summary>
    /// <param name="scopeType">The type of scope to create; default is
    /// <see cref="JSValueScopeType.Handle">.</param>
    public JSValueScope(JSValueScopeType scopeType = JSValueScopeType.Handle)
        : this(scopeType, env: default, runtime: default)
    {
    }

    /// <summary>
    /// Creates a new instance of a <see cref="JSValueScope"/>, which may be a parentless scope
    /// with initial environment handle and JS runtime.
    /// </summary>
    /// <param name="scopeType">The type of scope to create.</param>
    /// <param name="env">JS environment handle, required only for creating a scope
    /// without a parent, otherwise the environment is inherited from the parent scope.</param>
    /// <param name="runtime">JS runtime interface, required only for creating a scope
    /// without a parent, otherwise the JS runtime is inherited from the parent scope.</param>
    /// <param name="synchronizationContext">Optional synchronization context to use for async
    /// operations; if omitted then a default synchronization context is used.</param>
    public JSValueScope(
        JSValueScopeType scopeType,
        napi_env env,
        JSRuntime? runtime,
        JSSynchronizationContext? synchronizationContext = null)
    {
        ScopeType = scopeType;

        if (scopeType == JSValueScopeType.NoContext)
        {
            // A NoContext scope can inherit the env from a parent NoContext scope.
            _parentScope = s_currentScope;
            if (_parentScope != null && _parentScope.ScopeType != JSValueScopeType.NoContext)
            {
                throw new InvalidOperationException(
                    "A NoContext scope cannot be created within another type of scope.");
            }

            if (env.IsNull)
            {
                env = _parentScope?._env ??
                    throw new ArgumentNullException(nameof(env), "An environment is required.");
            }

            runtime ??= _parentScope?.Runtime ??
                    throw new ArgumentNullException(nameof(runtime), "A runtime is required.");

            _parentScope = null;
            _env = env;
            ThreadId = Environment.CurrentManagedThreadId;
            Runtime = runtime;
        }
        else if (scopeType == JSValueScopeType.Root)
        {
            _parentScope = s_currentScope;
            if (_parentScope != null)
            {
                if (_parentScope.ScopeType == JSValueScopeType.Root)
                {
                    // When there are multiple instances of the managed host in a process
                    // (created by separate workers), they do not inherit scope.
                    _parentScope = null;
                }
                else
                {
                    throw new InvalidOperationException(
                        "A Root scope cannot be created within another scope.");
                }
            }

            if (env.IsNull)
            {
                throw new ArgumentNullException(
                    nameof(env), "An environment is required for a root scope.");
            }
            else if (runtime == null)
            {
                throw new ArgumentNullException(
                    nameof(runtime), "A runtime is required for a root scope.");
            }

            _env = env;
            ThreadId = Environment.CurrentManagedThreadId;
            Runtime = runtime;
        }
        else
        {
            _parentScope = s_currentScope;

            if (scopeType == JSValueScopeType.Module &&
                _parentScope != null && _parentScope.ScopeType == JSValueScopeType.Module)
            {
                // When there are multiple AOT modules in a process, they do not inherit scope.
                _parentScope = null;
            }

            if (_parentScope == null)
            {
                // Module scopes may be created without a parent scope (for AOT modules).
                if (scopeType != JSValueScopeType.Module)
                {
                    throw new InvalidOperationException(
                        $"A {scopeType} scope cannot be created without a parent scope.");
                }

                // AOT module scopes are constructed with an env parameter
                // but without a pre-initialized runtime.
                _env = env.IsNull ? throw new ArgumentNullException(nameof(env)) : env;
                ThreadId = Environment.CurrentManagedThreadId;
                Runtime = runtime ?? new NodejsRuntime();
            }
            else if (_parentScope.IsDisposed)
            {
                // This should never happen because disposing a scope removes it from
                // s_currentScope (which is used to initialize _parentScope above).
                throw new InvalidOperationException("Parent scope is disposed.");
            }
            else if (scopeType == JSValueScopeType.Callback &&
                _parentScope.ScopeType != JSValueScopeType.Callback &&
                _parentScope.ScopeType != JSValueScopeType.Module &&
                _parentScope.ScopeType != JSValueScopeType.Root &&
                _parentScope.ScopeType != JSValueScopeType.NoContext)
            {
                throw new InvalidOperationException(
                    $"A Callback scope must be created within a Root, Module, or Callback scope. " +
                    $"Current scope: {scopeType}");
            }
            else if (!env.IsNull && env != _parentScope._env)
            {
                throw new ArgumentException(
                    "Environment must not be provided for a non-root scope.",
                    nameof(env));
            }
            else if (runtime != null && runtime != _parentScope.Runtime)
            {
                throw new ArgumentException(
                    "Runtime must not be provided for a non-root scope.",
                    nameof(runtime));
            }
            else
            {
                _parentScope.ThrowIfInvalidThreadAccess();
                _env = _parentScope._env;
                ThreadId = _parentScope.ThreadId;
                Runtime = _parentScope.Runtime;
            }

            if (scopeType == JSValueScopeType.Module)
            {
                if (_parentScope?.ModuleContext != null)
                {
                    throw new InvalidOperationException("Module scope cannot be nested.");
                }

                ModuleContext = new JSModuleContext();
            }
            else
            {
                ModuleContext = _parentScope!.ModuleContext;
            }
        }

        _scopeHandle = ScopeType switch
        {
            JSValueScopeType.Handle
                => Runtime.OpenHandleScope(_env, out napi_handle_scope handleScope)
                   .ThrowIfFailed(handleScope).Handle,
            JSValueScopeType.Escapable
                => Runtime.OpenEscapableHandleScope(
                    _env, out napi_escapable_handle_scope handleScope)
                   .ThrowIfFailed(handleScope).Handle,
            _ => default,
        };

        JSValueScope? previousScope = s_currentScope;
        try
        {
            s_currentScope = this;

            if (scopeType == JSValueScopeType.NoContext)
            {
                // NoContext scopes do not have a runtime context.
                RuntimeContext = null!;
                RuntimeContextHandle = default;
            }
            else if (_parentScope?.RuntimeContext != null)
            {
                // Nested scopes inherit the runtime context from the parent scope.
                RuntimeContext = _parentScope.RuntimeContext;
                RuntimeContextHandle = _parentScope.RuntimeContextHandle;
            }
            else
            {
                // Unparented scopes initialize a new runtime context.
                RuntimeContext = new JSRuntimeContext(env, Runtime, synchronizationContext);
                RuntimeContextHandle = (nint)GCHandle.Alloc(RuntimeContext);
            }

            if (scopeType == JSValueScopeType.Root || scopeType == JSValueScopeType.Callback)
            {
                _previousSyncContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(
                    RuntimeContext.SynchronizationContext);
            }
        }
        catch (Exception)
        {
            s_currentScope = previousScope;
            throw;
        }
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        if (ScopeType != JSValueScopeType.NoContext)
        {
            napi_env env = RuntimeContext.EnvironmentHandle;

            switch (ScopeType)
            {
                case JSValueScopeType.Handle:
                    Runtime.CloseHandleScope(
                        env, new napi_handle_scope(_scopeHandle)).ThrowIfFailed();
                    break;
                case JSValueScopeType.Escapable:
                    Runtime.CloseEscapableHandleScope(
                        env, new napi_escapable_handle_scope(_scopeHandle)).ThrowIfFailed();
                    break;
                default:
                    SynchronizationContext.SetSynchronizationContext(_previousSyncContext);
                    break;
            }
        }

        s_currentScope = _parentScope;
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
        if (s_currentScope?._env != _env)
        {
            throw new JSInvalidThreadAccessException(currentScope: s_currentScope, targetScope: this);
        }
    }
}
