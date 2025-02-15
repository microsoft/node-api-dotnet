// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;
using static Microsoft.JavaScript.NodeApi.Runtime.NodejsRuntime;

namespace Microsoft.JavaScript.NodeApi.Interop;

public delegate void JSThreadSafeCallback(JSValue jsFunction, object? functionContext, object? callbackData);

public delegate void JSThreadSafeFinalizeCallback(object? functionContext);

public class JSThreadSafeFunction
{
    private readonly JSRuntime _runtime;
    private readonly napi_threadsafe_function _tsfn;
    private int _refCount = 1;

    public static explicit operator napi_threadsafe_function(JSThreadSafeFunction function) => function._tsfn;
    public static implicit operator JSThreadSafeFunction(napi_threadsafe_function tsfn) => new(tsfn);

    private JSThreadSafeFunction(napi_threadsafe_function tsfn)
    {
        _runtime = JSValueScope.Current.Runtime;
        _tsfn = tsfn;
    }

    public static bool IsAvailable
        => JSValueScope.Current.Runtime.IsAvailable("napi_create_threadsafe_function");

    // This API may only be called from the main thread.
    public unsafe JSThreadSafeFunction(int maxQueueSize,
        int initialThreadCount,
        in JSValue asyncResourceName,
        in JSValue? jsFunction = null,
        in JSObject? asyncResource = null,
        JSThreadSafeFinalizeCallback? finalize = null,
        object? functionContext = null,
        JSThreadSafeCallback? jsCaller = null)
    {
        _runtime = JSValueScope.Current.Runtime;

        FunctionData functionData = new(functionContext, finalize, jsCaller);

        // Do not use AllocGCHandle() because the runtime context may not be initialized yet.
        GCHandle functionDataHandle = GCHandle.Alloc(functionData);

        napi_status status = _runtime.CreateThreadSafeFunction(
            (napi_env)JSValueScope.Current,
            (napi_value)jsFunction,
            (napi_value)(JSValue?)asyncResource,
            (napi_value)asyncResourceName,
            maxQueueSize,
            initialThreadCount,
            threadFinalizeData: default,
            new napi_finalize(s_finalizeFunctionData),
            (nint)functionDataHandle,
            (jsCaller != null)
                ? new napi_threadsafe_function_call_js(s_customCallJS)
                : new napi_threadsafe_function_call_js(s_defaultCallJS),
            out _tsfn);
        if (status != napi_status.napi_ok)
        {
            functionDataHandle.Free();
            status.ThrowIfFailed();
        }
    }


    // This API may be called from any thread.
    public object? GetFunctionContext()
    {
        _runtime.GetThreadSafeFunctionContext(_tsfn, out nint handle).ThrowIfFailed();
        FunctionData functionData = (FunctionData)GCHandle.FromIntPtr(handle).Target!;
        return functionData.FunctionContext;
    }

    // This API may be called from any thread.
    public void BlockingCall()
    {
        CallInternal(null, napi_threadsafe_function_call_mode.napi_tsfn_blocking);
    }

    // This API may be called from any thread.
    public void BlockingCall(object? data)
    {
        CallInternal(data, napi_threadsafe_function_call_mode.napi_tsfn_blocking);
    }

    // This API may be called from any thread.
    public void BlockingCall(Action callback)
    {
        CallInternal(callback, napi_threadsafe_function_call_mode.napi_tsfn_blocking);
    }

    // This API may be called from any thread.
    public void BlockingCall(Action<JSValue?, object?> callback)
    {
        CallInternal(callback, napi_threadsafe_function_call_mode.napi_tsfn_blocking);
    }

    // This API may be called from any thread.
    public bool NonBlockingCall()
    {
        return CallInternal(null, napi_threadsafe_function_call_mode.napi_tsfn_nonblocking);
    }

    // This API may be called from any thread.
    public bool NonBlockingCall(object? data)
    {
        return CallInternal(data, napi_threadsafe_function_call_mode.napi_tsfn_nonblocking);
    }

    // This API may be called from any thread.
    public bool NonBlockingCall(Action callback)
    {
        return CallInternal(callback, napi_threadsafe_function_call_mode.napi_tsfn_nonblocking);
    }

    // This API may be called from any thread.
    public bool NonBlockingCall(Action<JSValue?, object?> callback)
    {
        return CallInternal(callback, napi_threadsafe_function_call_mode.napi_tsfn_nonblocking);
    }

    // This API may only be called from the main thread.
    public void Ref()
    {
        if (_tsfn.Handle != default)
        {
            if (++_refCount == 1)
            {
                _runtime.RefThreadSafeFunction((napi_env)JSValueScope.Current, _tsfn).ThrowIfFailed();
            }
        }
    }

    // This API may only be called from the main thread.
    public void Unref()
    {
        if (_tsfn.Handle != default)
        {
            if (--_refCount == 0)
            {
                _runtime.UnrefThreadSafeFunction((napi_env)JSValueScope.Current, _tsfn); //TODO: .ThrowIfFailed();
            }
        }
    }

    // This API may be called from any thread.
    public napi_status Acquire()
    {
        return _runtime.AcquireThreadSafeFunction(_tsfn);
    }

    // This API may be called from any thread.
    public napi_status Release()
    {
        return _runtime.ReleaseThreadSafeFunction(
            _tsfn, napi_threadsafe_function_release_mode.napi_tsfn_release);
    }

    // This API may be called from any thread.
    public napi_status Abort()
    {
        return _runtime.ReleaseThreadSafeFunction(
            _tsfn, napi_threadsafe_function_release_mode.napi_tsfn_abort);
    }

    private bool CallInternal(object? callbackOrData, napi_threadsafe_function_call_mode mode)
    {
        // Do not use AllocGCHandle() because we're calling from another thread.
        GCHandle callbackOrDataHandle = GCHandle.Alloc(callbackOrData);
        napi_status status = _runtime.CallThreadSafeFunction(
            _tsfn, (nint)callbackOrDataHandle, mode);
        if (status != napi_status.napi_ok)
        {
            // Do not use FreeGCHandle() - the handle was allocated on a different thread.
            callbackOrDataHandle.Free();
        }

        // TODO: Consider throwing exceptions for status codes other than napi_ok.
        // Note some error statuses are expected:
        // - napi_queue_full: The queue is full, the nonblocking call should be retried later.
        // - napi_closing: The thread-safe function is closing (typically due to env shutdown).
        // - napi_invalid_arg: The TSFN is invalid or disposed (can also occur during shutdown).
        return status == napi_status.napi_ok;
    }

#if !UNMANAGED_DELEGATES
    private static readonly napi_finalize.Delegate s_finalizeFunctionData = FinalizeFunctionData;
    private static readonly napi_threadsafe_function_call_js.Delegate s_customCallJS = CustomCallJS;
    private static readonly napi_threadsafe_function_call_js.Delegate s_defaultCallJS = DefaultCallJS;

#else
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, nint, nint, void> s_finalizeFunctionData = &FinalizeFunctionData;
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_value, nint, nint, void> s_customCallJS = &CustomCallJS;
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_value, nint, nint, void> s_defaultCallJS = &DefaultCallJS;
#endif

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    private static unsafe void FinalizeFunctionData(napi_env env, nint _, nint hint)
    {
        GCHandle functionDataHandle = GCHandle.FromIntPtr(hint);
        if (functionDataHandle.Target is FunctionData functionData && functionData.Finalize is not null)
        {
            functionData.Finalize(functionData.FunctionContext);
        }

        functionDataHandle.Free();
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    private static unsafe void CustomCallJS(napi_env env, napi_value jsCallback, nint context, nint data)
    {
        if (env.IsNull && jsCallback.IsNull)
        {
            return;
        }

        using JSValueScope scope = new(JSValueScopeType.Callback, env, runtime: null);
        try
        {
            object? callbackData = null;
            if (data != default)
            {
                GCHandle dataHandle = GCHandle.FromIntPtr(data);
                callbackData = dataHandle.Target!;

                // Do not use FreeGCHandle() - the handle was allocated on a different thread.
                dataHandle.Free();
            }

            GCHandle contextHandle = GCHandle.FromIntPtr(context);
            FunctionData functionData = (FunctionData)contextHandle.Target!;
            functionData.JSCaller!((JSValue)jsCallback, functionData.FunctionContext, callbackData);
        }
        catch (Exception ex)
        {
            // This will be an unhandled promise rejection, which will either trigger the
            // process.unhandledRejection event (if a handler is registered) or end the process.
            JSPromise.Reject(JSError.CreateErrorValueForException(ex, out _));
        }
    }

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    private static unsafe void DefaultCallJS(napi_env env, napi_value jsCallback, nint context, nint data)
    {
        if (env.IsNull && jsCallback.IsNull)
        {
            return;
        }

        using JSValueScope scope = new(JSValueScopeType.Callback, env, runtime: null);
        try
        {
            if (data != default)
            {
                GCHandle dataHandle = GCHandle.FromIntPtr(data);
                object dataObject = dataHandle.Target!;

                // Do not use FreeGCHandle() - the handle was allocated on a different thread.
                dataHandle.Free();

                if (dataObject is Action action)
                {
                    action();
                }
                else if (dataObject is Action<JSValue?, object?> callback)
                {
                    GCHandle contextHandle = GCHandle.FromIntPtr(context);
                    FunctionData functionData = (FunctionData)contextHandle.Target!;
                    callback((JSValue?)jsCallback, functionData.FunctionContext);
                }
                else
                {
                    throw new ArgumentException("Unexpected data parameter");
                }
            }
            else if (jsCallback.Handle != default)
            {
                ((JSValue)jsCallback).Call();
            }
        }
        catch (Exception ex)
        {
            // This will be an unhandled promise rejection, which will either trigger the
            // process.unhandledRejection event (if a handler is registered) or end the process.
            JSPromise.Reject(JSError.CreateErrorValueForException(ex, out _));
        }
    }

    private class FunctionData
    {
        public FunctionData(
            object? functionContext,
            JSThreadSafeFinalizeCallback? finalize,
            JSThreadSafeCallback? jsCaller)
        {
            FunctionContext = functionContext;
            Finalize = finalize;
            JSCaller = jsCaller;
        }

        public object? FunctionContext { get; }
        public JSThreadSafeFinalizeCallback? Finalize { get; }
        public JSThreadSafeCallback? JSCaller { get; }
    }
}
