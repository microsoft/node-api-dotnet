using System;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public sealed class JSEscapableValueScope : JSValueScope
{
    private napi_escapable_handle_scope _handleScope;

    public JSEscapableValueScope(napi_env env) : base(env)
    {
        napi_open_escapable_handle_scope(env, out napi_escapable_handle_scope handleScope).ThrowIfFailed();
        _handleScope = handleScope;
    }

    public JSValue Escape(JSValue value)
    {
        if (ParentScope == null)
            throw new InvalidOperationException($"{ParentScope} must not be null");

        napi_escape_handle((napi_env)this, _handleScope, (napi_value)value, out napi_value result);
        return new JSValue(ParentScope, result);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsInvalid)
        {
            napi_close_escapable_handle_scope((napi_env)this, _handleScope).ThrowIfFailed();
        }
        base.Dispose(disposing);
    }
}
