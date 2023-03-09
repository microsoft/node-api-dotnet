using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.Interop;

public delegate void JSThreadSafeCallback(JSValue jsFunction, object? functionContext, object? callbackData);

public delegate void JSThreadSafeFinalizeCallback(object? functionContext);

public class JSThreadSafeFunction
{
    private napi_threadsafe_function _tsfn;
    private int _refCount = 1;

    public static explicit operator napi_threadsafe_function(JSThreadSafeFunction function) => function._tsfn;
    public static implicit operator JSThreadSafeFunction(napi_threadsafe_function tsfn) => new(tsfn);

    private JSThreadSafeFunction(napi_threadsafe_function tsfn) => _tsfn = tsfn;

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
        FunctionData functionData = new()
        {
            FunctionContext = functionContext,
            Finalize = finalize,
            JSCaller = jsCaller
        };
        GCHandle functionDataHandle = GCHandle.Alloc(functionData);
        napi_status status = napi_create_threadsafe_function(
                                 (napi_env)JSValueScope.Current,
                                 (napi_value)jsFunction,
                                 (napi_value)(JSValue?)asyncResource,
                                 (napi_value)asyncResourceName,
                                 (nuint)maxQueueSize,
                                 (nuint)initialThreadCount,
                                 thread_finalize_data: nint.Zero,
                                 new napi_finalize(&FinalizeFunctionData),
                                 (nint)functionDataHandle,
                                 (jsCaller != null)
                                     ? new napi_threadsafe_function_call_js(&CustomCallJS)
                                     : new napi_threadsafe_function_call_js(&DefaultCallJS),
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
        napi_get_threadsafe_function_context(_tsfn, out nint handle).ThrowIfFailed();
        FunctionData functionData = (FunctionData)GCHandle.FromIntPtr(handle).Target!;
        return functionData.FunctionContext;
    }

    // This API may be called from any thread.
    public void BlockingCall()
    {
        CallInternal(null, napi_threadsafe_function_call_mode.napi_tsfn_blocking).ThrowIfFailed();
    }

    // This API may be called from any thread.
    public void BlockingCall(object? data)
    {
        CallInternal(data, napi_threadsafe_function_call_mode.napi_tsfn_blocking).ThrowIfFailed();
    }

    // This API may be called from any thread.
    public void BlockingCall(Action callback)
    {
        CallInternal(callback, napi_threadsafe_function_call_mode.napi_tsfn_blocking).ThrowIfFailed();
    }

    // This API may be called from any thread.
    public void BlockingCall(Action<JSValue?, object?> callback)
    {
        CallInternal(callback, napi_threadsafe_function_call_mode.napi_tsfn_blocking).ThrowIfFailed();
    }

    // This API may be called from any thread.
    public bool NonBlockingCall()
    {
        return NonBlockingCall(CallInternal(null, napi_threadsafe_function_call_mode.napi_tsfn_nonblocking));
    }

    // This API may be called from any thread.
    public bool NonBlockingCall(object? data)
    {
        return NonBlockingCall(CallInternal(data, napi_threadsafe_function_call_mode.napi_tsfn_nonblocking));
    }

    // This API may be called from any thread.
    public bool NonBlockingCall(Action callback)
    {
        return NonBlockingCall(CallInternal(callback, napi_threadsafe_function_call_mode.napi_tsfn_nonblocking));
    }

    // This API may be called from any thread.
    public bool NonBlockingCall(Action<JSValue?, object?> callback)
    {
        return NonBlockingCall(CallInternal(callback, napi_threadsafe_function_call_mode.napi_tsfn_nonblocking));
    }

    // This API may only be called from the main thread.
    public void Ref()
    {
        if (_tsfn.Handle != nint.Zero)
        {
            if (++_refCount == 1)
            {
                napi_ref_threadsafe_function((napi_env)JSValueScope.Current, _tsfn).ThrowIfFailed();
            }
        }
    }

    // This API may only be called from the main thread.
    public void Unref()
    {
        if (_tsfn.Handle != nint.Zero)
        {
            if (--_refCount == 0)
            {
                napi_unref_threadsafe_function((napi_env)JSValueScope.Current, _tsfn); //TODO: .ThrowIfFailed();
            }
        }
    }

    // This API may be called from any thread.
    public napi_status Acquire()
    {
        return napi_acquire_threadsafe_function(_tsfn);
    }

    // This API may be called from any thread.
    public napi_status Release()
    {
        return napi_release_threadsafe_function(_tsfn, napi_threadsafe_function_release_mode.napi_tsfn_release);
    }

    // This API may be called from any thread.
    public napi_status Abort()
    {
        return napi_release_threadsafe_function(_tsfn, napi_threadsafe_function_release_mode.napi_tsfn_abort);
    }

    private static bool NonBlockingCall(napi_status status)
    {
        if (status == napi_status.napi_ok)
        {
            return true;
        }
        else if (status != napi_status.napi_queue_full)
        {
            status.ThrowIfFailed();
        }
        return false;
    }

    private napi_status CallInternal(object? callbackOrData, napi_threadsafe_function_call_mode mode)
    {
        GCHandle callbackOrDataHandle = GCHandle.Alloc(callbackOrData);
        napi_status status = napi_call_threadsafe_function(_tsfn, (nint)callbackOrDataHandle, mode);
        if (status != napi_status.napi_ok && callbackOrData != null)
        {
            (callbackOrData as IDisposable)?.Dispose();
            callbackOrDataHandle.Free();
        }

        return status;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void FinalizeFunctionData(napi_env env, nint _, nint hint)
    {
        GCHandle functionDataHandle = GCHandle.FromIntPtr(hint);
        if (functionDataHandle.Target is FunctionData functionData && functionData.Finalize is not null)
        {
            functionData.Finalize(functionData.FunctionContext);
        }

        functionDataHandle.Free();
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void CustomCallJS(napi_env env, napi_value jsCallback, nint context, nint data)
    {
        if (env.IsNull && jsCallback.IsNull)
        {
            return;
        }

        try
        {
            using JSValueScope scope = new(JSValueScopeType.Callback, env);

            object? callbackData = null;
            if (data != nint.Zero)
            {
                GCHandle dataHandle = GCHandle.FromIntPtr(data);
                callbackData = dataHandle.Target!;
                dataHandle.Free();
            }

            GCHandle contextHandle = GCHandle.FromIntPtr(context);
            FunctionData functionData = (FunctionData)contextHandle.Target!;
            functionData.JSCaller!((JSValue)jsCallback, functionData.FunctionContext, callbackData);
        }
        catch (Exception ex)
        {
            JSError.Fatal(ex.Message);
        }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void DefaultCallJS(napi_env env, napi_value jsCallback, nint context, nint data)
    {
        if (env.IsNull && jsCallback.IsNull)
        {
            return;
        }

        try
        {
            using JSValueScope scope = new(JSValueScopeType.Callback, env);

            if (data != nint.Zero)
            {
                GCHandle dataHandle = GCHandle.FromIntPtr(data);
                object dataObject = dataHandle.Target!;
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
            else if (jsCallback.Handle != nint.Zero)
            {
                ((JSValue)jsCallback).Call();
            }
        }
        catch (Exception ex)
        {
            JSError.Fatal(ex.Message);
        }
    }

    private class FunctionData
    {
        public object? FunctionContext { get; init; }
        public JSThreadSafeFinalizeCallback? Finalize { get; init; }
        public JSThreadSafeCallback? JSCaller { get; init; }
    }
}
