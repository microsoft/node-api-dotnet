using System.Runtime.InteropServices;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public class JSCallbackArgs
{
    private readonly JSValue[] _args;

    public JSValue this[int index] => _args[index];

    public int Length => _args.Length;

    public JSValue ThisArg { get; }

    public object? Data { get; set; } = null;

    public JSValue GetNewTarget()
    {
        napi_get_new_target((napi_env)Scope, CallbackInfo, out napi_value result).ThrowIfFailed();
        return result;
    }

    internal JSValueScope Scope { get; }

    internal napi_callback_info CallbackInfo { get; }

    public JSCallbackArgs(JSValueScope scope, napi_callback_info callbackInfo)
    {
        Scope = scope;
        CallbackInfo = callbackInfo;
        unsafe
        {
            nuint argc = 0;
            napi_get_cb_info((napi_env)scope, callbackInfo, &argc, null, null, nint.Zero).ThrowIfFailed();
            napi_value* argv = stackalloc napi_value[(int)argc];
            napi_value thisArg;
            nint data;
            napi_get_cb_info((napi_env)scope, callbackInfo, &argc, argv, &thisArg, new nint(&data)).ThrowIfFailed();

            _args = new JSValue[(int)argc];
            for (int i = 0; i < (int)argc; ++i)
            {
                _args[i] = argv[i];
            }

            ThisArg = thisArg;
            Data = data != nint.Zero ? GCHandle.FromIntPtr(data).Target : null;
        }
    }
}
