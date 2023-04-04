// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Indicates the type of <see cref="JSValueScope" /> within the hiearchy of scopes.
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

public sealed class JSValueScope : IDisposable
{
    private readonly JSValueScope? _parentScope;
    private readonly napi_env _env;
    private readonly SynchronizationContext? _previousSyncContext;
    private readonly nint _scopeHandle;

    [ThreadStatic] private static JSValueScope? s_currentScope;

    public JSValueScopeType ScopeType { get; }

    public static JSValueScope Current => s_currentScope ??
        throw new InvalidOperationException("No current scope.");

    public bool IsDisposed { get; private set; }

    public JSRuntimeContext RuntimeContext { get; }

    public JSModuleContext? ModuleContext { get; internal set; }

    public JSValueScope(
        JSValueScopeType scopeType = JSValueScopeType.Handle, napi_env env = default)
    {
        ScopeType = scopeType;

        _parentScope = s_currentScope;
        s_currentScope = this;

        _env = !env.IsNull
               ? env
               : _parentScope?._env ?? throw new ArgumentException("env is null", nameof(env));

        RuntimeContext = scopeType switch
        {
            JSValueScopeType.NoContext => null!,
            JSValueScopeType.Root => _parentScope?.RuntimeContext ?? new JSRuntimeContext(_env),
            JSValueScopeType.Module => _parentScope?.RuntimeContext ?? new JSRuntimeContext(_env),
            JSValueScopeType.Callback => (JSRuntimeContext)_env,
            _ => _parentScope?.RuntimeContext
                 ?? throw new InvalidOperationException("Parent scope not found."),
        };

        ModuleContext = _parentScope?.ModuleContext;
        if (scopeType == JSValueScopeType.Module)
        {
            if (ModuleContext != null)
            {
                throw new InvalidOperationException("Module scope cannot be nested.");
            }

            ModuleContext = new JSModuleContext();
        }

        if (scopeType == JSValueScopeType.Root || scopeType == JSValueScopeType.Callback)
        {
            _previousSyncContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(RuntimeContext.SynchronizationContext);
        }

        _scopeHandle = ScopeType switch
        {
            JSValueScopeType.Handle
                => napi_open_handle_scope(_env, out napi_handle_scope handleScope)
                   .ThrowIfFailed(handleScope).Handle,
            JSValueScopeType.Escapable
                => napi_open_escapable_handle_scope(
                    _env, out napi_escapable_handle_scope handleScope)
                   .ThrowIfFailed(handleScope).Handle,
            _ => default,
        };
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
                    napi_close_handle_scope(
                        env, new napi_handle_scope(_scopeHandle)).ThrowIfFailed();
                    break;
                case JSValueScopeType.Escapable:
                    napi_close_escapable_handle_scope(
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

        napi_escape_handle(
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
