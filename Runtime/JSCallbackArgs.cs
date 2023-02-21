using System;
using System.Runtime.InteropServices;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public readonly ref struct JSCallbackArgs
{
    private readonly JSValueScope _scope;
    private readonly napi_value _thisArg;
    private readonly ReadOnlySpan<napi_value> _args;

    internal JSCallbackArgs(
        JSValueScope scope, napi_value thisArg, ReadOnlySpan<napi_value> args, object? data = null)
    {
        _scope = scope;
        _thisArg = thisArg;
        _args = args;
        Data = data;
    }

    public JSValue ThisArg => new(_thisArg, _scope);

    public JSValue this[int index] => new(_args[index], _scope);

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
        napi_env env,
        napi_callback_info callbackInfo,
        out napi_value thisArg,
        ref Span<napi_value> args)
    {
        napi_value thisArgHandle;
        if (args.Length == 0)
        {
            napi_get_cb_info(env, callbackInfo, null, null, &thisArgHandle, null)
                .ThrowIfFailed();
        }
        else
        {
            fixed (napi_value* argv = &args[0])
            {
                nuint argc = (nuint)args.Length;
                napi_get_cb_info(env, callbackInfo, &argc, argv, &thisArgHandle, null)
                    .ThrowIfFailed();
            }
        }
        thisArg = thisArgHandle;
    }
}
