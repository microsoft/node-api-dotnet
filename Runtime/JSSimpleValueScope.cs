using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public sealed class JSSimpleValueScope : JSValueScope
{
    private napi_handle_scope _handleScope;

    public JSSimpleValueScope(napi_env env) : base(env)
    {
        napi_open_handle_scope(env, out napi_handle_scope handleScope).ThrowIfFailed();
        _handleScope = handleScope;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            napi_close_handle_scope((napi_env)this, _handleScope).ThrowIfFailed();
        }
        base.Dispose(disposing);
    }
}
