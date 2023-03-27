// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public readonly ref struct JSCallbackArgs
{
    private readonly napi_value _thisArg;
    private readonly ReadOnlySpan<napi_value> _args;

    internal JSCallbackArgs(
        JSValueScope scope,
        napi_value thisArg,
        ReadOnlySpan<napi_value> args,
        object? data)
    {
        Scope = scope;
        _thisArg = thisArg;
        _args = args;
        Data = data;
    }

    internal unsafe JSCallbackArgs(JSValueScope scope,
                                   napi_callback_info callbackInfo,
                                   ReadOnlySpan<napi_value> args,
                                   object? data = null)
    {
        napi_env env = (napi_env)scope;
        nint dataPointer;
        napi_value thisArgHandle;
        if (args.Length == 0)
        {
            napi_get_cb_info(env, callbackInfo, null, null, &thisArgHandle, &dataPointer)
                .ThrowIfFailed();
        }
        else
        {
            fixed (napi_value* argv = &args[0])
            {
                nuint argc = (nuint)args.Length;
                napi_get_cb_info(env, callbackInfo, &argc, argv, &thisArgHandle, &dataPointer)
                    .ThrowIfFailed();
            }
        }
        Scope = scope;
        _thisArg = thisArgHandle;
        _args = args;
        Data = data;
    }

    internal JSValueScope Scope { get; }

    public JSValue ThisArg => new(_thisArg, Scope);

    public JSValue this[int index] => index < _args.Length ? new(_args[index], Scope) : default;

    public int Length => _args.Length;

    public object? Data { get; }

    internal static unsafe void GetDataAndLength(
        napi_env env,
        napi_callback_info callbackInfo,
        out object? data,
        out int length)
    {
        nuint argc = 0;
        nint dataPointer;
        napi_get_cb_info(env, callbackInfo, &argc, null, null, &dataPointer).ThrowIfFailed();
        data = dataPointer != 0 ? GCHandle.FromIntPtr(dataPointer).Target : null;
        length = (int)argc;
    }
}
