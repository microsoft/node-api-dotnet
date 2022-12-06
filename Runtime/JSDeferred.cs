using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public class JSDeferred
{
    private napi_deferred _handle;

    public JSDeferred(napi_deferred handle)
    {
        _handle = handle;
    }

    public void Resolve(JSValue resolution)
    {
        // _handle becomes invalid after this call
        napi_resolve_deferred((napi_env)resolution.Scope, _handle, (napi_value)resolution).ThrowIfFailed();
    }

    public void Reject(JSValue rejection)
    {
        // _handle becomes invalid after this call
        napi_resolve_deferred((napi_env)rejection.Scope, _handle, (napi_value)rejection).ThrowIfFailed();
    }
}
