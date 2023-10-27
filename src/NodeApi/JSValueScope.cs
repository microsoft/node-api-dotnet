// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
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
    private readonly napi_env _env;
    private readonly SynchronizationContext? _previousSyncContext;
    private readonly nint _scopeHandle;

    [ThreadStatic] private static JSValueScope? s_currentScope;

    public JSValueScopeType ScopeType { get; }

    /// <summary>
    /// Gets the current JS value scope.
    /// </summary>
    /// <exception cref="InvalidOperationException">No scope was established for the current
    /// thread.</exception>
    public static JSValueScope Current => s_currentScope ??
        throw new InvalidOperationException("No current scope.");

    public bool IsDisposed { get; private set; }

    public JSRuntime Runtime { get; }
    public JSRuntimeContext RuntimeContext { get; }

    internal static JSRuntime CurrentRuntime => Current.Runtime;
    internal static JSRuntimeContext? CurrentRuntimeContext => s_currentScope?.RuntimeContext;

    public JSModuleContext? ModuleContext { get; internal set; }

    public JSValueScope(
        JSValueScopeType scopeType = JSValueScopeType.Handle,
        napi_env env = default,
        JSRuntime? runtime = null)
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
                    // (created by searate workers), they do not inherit scope.
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
                    throw new InvalidOperationException("Parent scope not found.");
                }

                // AOT module scopes are constructed with an env parameter
                // but without a pre-initialized runtime.
                _env = env.IsNull ? throw new ArgumentNullException(nameof(env)) : env;
                Runtime = runtime ?? new NodejsRuntime();
            }
            else
            {
                if (_parentScope.IsDisposed)
                {
                    throw new InvalidOperationException("Parent scope is disposed.");
                }

                if (!env.IsNull && env != _parentScope._env)
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

                _env = _parentScope._env;
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

            RuntimeContext = scopeType == JSValueScopeType.NoContext ? null! :
                _parentScope?.RuntimeContext ?? new JSRuntimeContext(env);

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
            napi_env env = (napi_env)RuntimeContext;

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

            s_currentScope = _parentScope;
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

    public static explicit operator napi_env(JSValueScope scope)
    {
        if (scope is null) throw new ArgumentNullException(nameof(scope));
        return scope!._env;
    }
}
