using System;
using System.Runtime.InteropServices;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public readonly ref struct JSCallbackArgs
{
    private readonly Span<JSValue> _args;

    internal JSCallbackArgs(JSValue thisArg, Span<JSValue> args, object? data = null)
    {
        ThisArg = thisArg;
        _args = args;
        Data = data;
    }

    public JSValue ThisArg { get; }

    public JSValue this[int index] => _args[index];

    public int Length => _args.Length;

    public object? Data { get; }

    internal static unsafe void GetDataAndLength(
        napi_env scope,
        napi_callback_info callbackInfo,
        out object? data,
        out int length)
    {
        nuint argc = 0;
        nint dataPointer;
        napi_get_cb_info(scope, callbackInfo, &argc, null, null, &dataPointer)
            .ThrowIfFailed();
        data = dataPointer != 0 ? GCHandle.FromIntPtr(dataPointer).Target : null;
        length = (int)argc;
    }

    internal static unsafe void GetArgs(
        napi_env scope,
        napi_callback_info callbackInfo,
        out JSValue thisArg,
        ref Span<JSValue> args)
    {
        nuint argc = (nuint)args.Length;
        napi_value* argv = stackalloc napi_value[args.Length];
        napi_value thisArgHandle;
        napi_get_cb_info(scope, callbackInfo, &argc, argv, &thisArgHandle, null)
            .ThrowIfFailed();
        for (int i = 0; i < args.Length; i++)
        {
            args[i] = i < (int)argc ? new JSValue(argv[i], scope) : default;
        }
        thisArg = new JSValue(thisArgHandle, scope);
    }
}
