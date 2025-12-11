// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Provides access to the arguments for a low-level JavaScript function call or callback, along
/// with the `this` argument and an optional context object.
/// </summary>
/// <remarks>
/// This type is a `ref struct` meaning it can only be used on the call stack.
/// </remarks>
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

    internal JSCallbackArgs(
        JSValueScope scope,
        napi_callback_info callbackInfo,
        Span<napi_value> args,
        object? data = null)
    {
        napi_env env = (napi_env)scope;
        scope.Runtime.GetCallbackArgs(env, callbackInfo, args, out napi_value thisArg)
            .ThrowIfFailed();
        Scope = scope;
        _thisArg = thisArg;
        _args = args;
        Data = data;
    }

    internal JSValueScope Scope { get; }

    /// <summary>
    /// Gets the `this` argument for the call.
    /// </summary>
    public JSValue ThisArg => new(_thisArg, Scope);

    /// <summary>
    /// Gets the argument at the specified (zero-based) index.
    /// </summary>
    /// <remarks>
    /// If the index is out of range, this property returns `default(JSValue)` which is equivalent
    /// to JS `undefined`.
    /// </remarks>
    public JSValue this[int index] => index < _args.Length ? new(_args[index], Scope) : default;

    /// <summary>
    /// Gets the number of arguments.
    /// </summary>
    public int Length => _args.Length;

    /// <summary>
    /// Gets the optional context object that was provided when the callback was registered.
    /// </summary>
    public object? Data { get; }

    internal static void GetDataAndLength(
        JSValueScope scope,
        napi_callback_info callbackInfo,
        out object? data,
        out nuint length)
    {
        scope.Runtime.GetCallbackInfo((napi_env)scope, callbackInfo, out length, out nint data_ptr)
            .ThrowIfFailed();
        data = data_ptr != 0 ? GCHandle.FromIntPtr(data_ptr).Target : null;
    }
}
