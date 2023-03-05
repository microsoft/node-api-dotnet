using System;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public enum JSValueScopeType { Handle, Escapable, Callback, Root, RootNoContext, }

public sealed class JSValueScope : IDisposable
{
    private readonly JSValueScope? _parentScope;
    private readonly napi_env _env;
    private readonly SynchronizationContext? _previousSyncContext;
    private readonly nint _scopeHandle;

    [ThreadStatic] private static JSValueScope? s_currentScope;

    public JSValueScopeType ScopeType { get; }

    public static JSValueScope? Current => s_currentScope;

    public bool IsDisposed { get; private set; }

    public JSContext ModuleContext { get; }

    public JSValueScope(
        JSValueScopeType scopeType = JSValueScopeType.Handle, napi_env env = default)
    {
        ScopeType = scopeType;

        _parentScope = s_currentScope;
        s_currentScope = this;

        _env = !env.IsNull
               ? env
               : _parentScope?._env ?? throw new ArgumentException("env is null", nameof(env));

        ModuleContext = scopeType switch
        {
            JSValueScopeType.Root => new JSContext(_env),
            JSValueScopeType.Callback => (JSContext)_env,
            JSValueScopeType.RootNoContext => null!,
            _ => _parentScope?.ModuleContext
                 ?? throw new InvalidOperationException("Parent scope not found"),
        };

        if (scopeType == JSValueScopeType.Root || scopeType == JSValueScopeType.Callback)
        {
            _previousSyncContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(ModuleContext.SynchronizationContext);
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
            _ => nint.Zero,
        };
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        if (ScopeType != JSValueScopeType.RootNoContext)
        {
            napi_env env = (napi_env)ModuleContext;

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

    public static explicit operator napi_env(JSValueScope? scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return scope._env;
    }
}
