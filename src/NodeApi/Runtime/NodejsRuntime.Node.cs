// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi.Runtime;

// Imports Node.js native APIs defined in node_api.h
public unsafe partial class NodejsRuntime
{
#pragma warning disable IDE1006 // Naming: missing prefix '_'

    #region Thread-safe functions

    private delegate* unmanaged[Cdecl]<
        napi_env,
        napi_value,
        napi_value,
        napi_value,
        nuint,
        nuint,
        nint,
        napi_finalize,
        nint,
        napi_threadsafe_function_call_js,
        nint,
        napi_status>
        napi_create_threadsafe_function;

    public override napi_status CreateThreadSafeFunction(
        napi_env env,
        napi_value func,
        napi_value asyncResource,
        napi_value asyncResourceName,
        int maxQueueSize,
        int initialThreadCount,
        nint threadFinalizeData,
        napi_finalize threadFinalizeCallback,
        nint context,
        napi_threadsafe_function_call_js callJSCallback,
        out napi_threadsafe_function result)
    {
        if (napi_create_threadsafe_function == null)
        {
            napi_create_threadsafe_function = (delegate* unmanaged[Cdecl]<
                napi_env,
                napi_value,
                napi_value,
                napi_value,
                nuint,
                nuint,
                nint,
                napi_finalize,
                nint,
                napi_threadsafe_function_call_js,
                nint,
                napi_status>)Import(nameof(napi_create_threadsafe_function));
        }

        fixed (napi_threadsafe_function* result_ptr = &result)
        {
            return napi_create_threadsafe_function(
                env,
                func,
                asyncResource,
                asyncResourceName,
                (nuint)maxQueueSize,
                (nuint)initialThreadCount,
                threadFinalizeData,
                threadFinalizeCallback,
                context,
                callJSCallback,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_threadsafe_function, nint, napi_threadsafe_function_call_mode, napi_status>
        napi_call_threadsafe_function;

    public override napi_status CallThreadSafeFunction(
        napi_threadsafe_function func,
        nint data,
        napi_threadsafe_function_call_mode isBlocking)
    {
        return Import(ref napi_call_threadsafe_function)(func, data, isBlocking);
    }

    private delegate* unmanaged[Cdecl]<napi_threadsafe_function, nint, napi_status>
        napi_get_threadsafe_function_context;

    public override napi_status GetThreadSafeFunctionContext(
        napi_threadsafe_function func,
        out nint result)
    {
        fixed (nint* result_ptr = &result)
        {
            return Import(ref napi_get_threadsafe_function_context)(func, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_threadsafe_function, napi_status>
        napi_acquire_threadsafe_function;

    public override napi_status AcquireThreadSafeFunction(napi_threadsafe_function func)
    {
        return Import(ref napi_acquire_threadsafe_function)(func);
    }

    private delegate* unmanaged[Cdecl]<
        napi_threadsafe_function, napi_threadsafe_function_release_mode, napi_status>
        napi_release_threadsafe_function;

    public override napi_status ReleaseThreadSafeFunction(
        napi_threadsafe_function func,
        napi_threadsafe_function_release_mode mode)
    {
        return Import(ref napi_release_threadsafe_function)(func, mode);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_threadsafe_function, napi_status>
        napi_ref_threadsafe_function;

    public override napi_status RefThreadSafeFunction(napi_env env, napi_threadsafe_function func)
    {
        return Import(ref napi_ref_threadsafe_function)(env, func);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_threadsafe_function, napi_status>
        napi_unref_threadsafe_function;

    public override napi_status UnrefThreadSafeFunction(napi_env env, napi_threadsafe_function func)
    {
        return Import(ref napi_unref_threadsafe_function)(env, func);
    }

    #endregion

    #region Async work

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_async_init;

    public override napi_status AsyncInit(
        napi_env env,
        napi_value asyncResource,
        napi_value asyncResourceName,
        out napi_async_context result)
    {
        fixed (napi_async_context* result_ptr = &result)
        {
            return Import(ref napi_async_init)(
                env,
                asyncResource,
                asyncResourceName,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_async_context, napi_status>
        napi_async_destroy;

    public override napi_status AsyncDestroy(napi_env env, napi_async_context asyncContext)
    {
        return Import(ref napi_async_destroy)(env, asyncContext);
    }

    private delegate* unmanaged[Cdecl]<
        napi_env,
        napi_value,
        napi_value,
        napi_async_execute_callback,
        napi_async_complete_callback,
        nint,
        nint,
        napi_status>
        napi_create_async_work;

    public override napi_status CreateAsyncWork(
        napi_env env,
        napi_value asyncResource,
        napi_value asyncResourceName,
        napi_async_execute_callback execute,
        napi_async_complete_callback complete,
        nint data,
        out napi_async_work result)
    {
        fixed (napi_async_work* result_ptr = &result)
        {
            if (napi_create_async_work == null)
            {
                napi_create_async_work = (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_value,
                    napi_value,
                    napi_async_execute_callback,
                    napi_async_complete_callback,
                    nint,
                    nint,
                    napi_status>)Import(nameof(napi_create_async_work));
            }

            return napi_create_async_work(
                env,
                asyncResource,
                asyncResourceName,
                execute,
                complete,
                data,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_async_work, napi_status>
        napi_queue_async_work;

    public override napi_status QueueAsyncWork(napi_env env, napi_async_work work)
    {
        return Import(ref napi_queue_async_work)(env, work);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_async_work, napi_status>
        napi_delete_async_work;

    public override napi_status DeleteAsyncWork(napi_env env, napi_async_work work)
    {
        return Import(ref napi_delete_async_work)(env, work);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_async_work, napi_status>
        napi_cancel_async_work;

    public override napi_status CancelAsyncWork(napi_env env, napi_async_work work)
    {
        return Import(ref napi_cancel_async_work)(env, work);
    }

    private delegate* unmanaged[Cdecl]<
        napi_env,
        napi_async_context,
        napi_value,
        napi_value,
        nuint,
        nint,
        nint,
        napi_status>
        napi_make_callback;

    public override napi_status MakeCallback(
        napi_env env,
        napi_async_context asyncContext,
        napi_value recv,
        napi_value func,
        Span<napi_value> args,
        out napi_value result)
    {
        fixed (napi_value* args_ptr = &args.GetPinnableReference())
        fixed (napi_value* result_ptr = &result)
        {
            if (napi_make_callback == null)
            {
                napi_make_callback = (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_async_context,
                    napi_value,
                    napi_value,
                    nuint,
                    nint,
                    nint,
                    napi_status>)Import(nameof(napi_make_callback));
            }

            return napi_make_callback(
                env,
                asyncContext,
                recv,
                func,
                (nuint)args.Length,
                (nint)args_ptr,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, napi_value, napi_async_context, nint, napi_status>
        napi_open_callback_scope;

    public override napi_status OpenCallbackScope(
        napi_env env,
        napi_value resourceObject,
        napi_async_context asyncContext,
        out napi_callback_scope result)
    {
        fixed (napi_callback_scope* result_ptr = &result)
        {
            return Import(ref napi_open_callback_scope)(
                env,
                resourceObject,
                asyncContext,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_callback_scope, napi_status>
        napi_close_callback_scope;

    public override napi_status CloseCallbackScope(napi_env env, napi_callback_scope scope)
    {
        return Import(ref napi_close_callback_scope)(env, scope);
    }

    #endregion

    #region Cleanup hooks

    private delegate* unmanaged[Cdecl]<
        napi_env, napi_async_cleanup_hook, nint, nint, napi_status>
        napi_add_async_cleanup_hook;

    public override napi_status AddAsyncCleanupHook(
        napi_env env,
        napi_async_cleanup_hook hook,
        nint arg,
        out napi_async_cleanup_hook_handle result)
    {
        fixed (napi_async_cleanup_hook_handle* result_ptr = &result)
        {
            return Import(ref napi_add_async_cleanup_hook)(
                env,
                hook,
                arg,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_async_cleanup_hook_handle, napi_status>
        napi_remove_async_cleanup_hook;

    public override napi_status RemoveAsyncCleanupHook(napi_async_cleanup_hook_handle removeHandle)
    {
        return Import(ref napi_remove_async_cleanup_hook)(removeHandle);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_cleanup_hook, nint, napi_status>
        napi_add_env_cleanup_hook;

    public override napi_status AddEnvCleanupHook(napi_env env, napi_cleanup_hook func, nint arg)
    {
        return Import(ref napi_add_env_cleanup_hook)(env, func, arg);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_cleanup_hook, nint, napi_status>
        napi_remove_env_cleanup_hook;

    public override napi_status RemoveEnvCleanupHook(napi_env env, napi_cleanup_hook func, nint arg)
    {
        return Import(ref napi_remove_env_cleanup_hook)(env, func, arg);
    }

    #endregion

    #region Buffers

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_is_buffer;

    public override napi_status IsBuffer(napi_env env, napi_value value, out bool result)
    {
        fixed (bool* result_ptr = &result)
        {
            return Import(ref napi_is_buffer)(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nuint, nint, nint, napi_status>
        napi_create_buffer;

    public override napi_status CreateBuffer(napi_env env, Span<byte> data, out napi_value result)
    {
        fixed (byte* data_ptr = &data.GetPinnableReference())
        fixed (napi_value* result_ptr = &result)
        {
            return Import(ref napi_create_buffer)(
                env,
                (nuint)data.Length,
                (nint)data_ptr,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nuint, nint, nint, nint, napi_status>
        napi_create_buffer_copy;

    public override napi_status CreateBufferCopy(
        napi_env env,
        ReadOnlySpan<byte> data,
        out nint resultData,
        out napi_value result)
    {
        fixed (byte* data_ptr = &data.GetPinnableReference())
        fixed (nint* resultData_ptr = &resultData)
        fixed (napi_value* result_ptr = &result)
        {
            return Import(ref napi_create_buffer_copy)(
                env,
                (nuint)data.Length,
                (nint)data_ptr,
                (nint)resultData_ptr,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, nuint, nint, napi_finalize, nint, nint, napi_status>
        napi_create_external_buffer;

    public override napi_status CreateExternalBuffer(
        napi_env env,
        Span<byte> data,
        napi_finalize finalizeCallback,
        nint finalizeHint,
        out napi_value result)
    {
        fixed (byte* data_ptr = &data.GetPinnableReference())
        fixed (napi_value* result_ptr = &result)
        {
            return Import(ref napi_create_external_buffer)(
                env,
                (nuint)data.Length,
                (nint)data_ptr,
                finalizeCallback,
                finalizeHint,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
        napi_get_buffer_info;

    public override napi_status GetBufferInfo(
        napi_env env,
        napi_value value,
        out nint data,
        out nuint length)
    {
        fixed (nint* data_ptr = &data)
        fixed (nuint* length_ptr = &length)
        {
            return Import(ref napi_get_buffer_info)(
                env,
                value,
                (nint)data_ptr,
                (nint)length_ptr);
        }
    }

    #endregion

    #region Misc Node.js functions

    private delegate* unmanaged[Cdecl]<nint, nuint, nint, nuint, void>
        napi_fatal_error;

    [DoesNotReturn]
    public override void FatalError(string location, string message)
    {
        using (PooledBuffer locationBuffer = PooledBuffer.FromStringUtf8(location))
        using (PooledBuffer messageBuffer = PooledBuffer.FromStringUtf8(message))
            fixed (byte* location_ptr = &locationBuffer.Pin())
            fixed (byte* message_ptr = &messageBuffer.Pin())
            {
                if (napi_fatal_error == null)
                {
                    napi_fatal_error = (delegate* unmanaged[Cdecl]<nint, nuint, nint, nuint, void>)
                        Import(nameof(napi_fatal_error));
                }

                napi_fatal_error(
                    (nint)location_ptr,
                    (nuint)locationBuffer.Length,
                    (nint)message_ptr,
                    (nuint)messageBuffer.Length);
                throw new Exception("napi_fatal_error() returned.");
            }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
        napi_fatal_exception;

    public override napi_status FatalException(napi_env env, napi_value err)
    {
        return Import(ref napi_fatal_exception)(env, err);
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_get_uv_event_loop;

    public override napi_status GetUVEventLoop(napi_env env, out uv_loop_t result)
    {
        fixed (uv_loop_t* result_ptr = &result)
        {
            return Import(ref napi_get_uv_event_loop)(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<nint, void>
        napi_module_register;

    public override void RegisterModule(ref napi_module module)
    {
        if (napi_module_register == null)
        {
            napi_module_register = (delegate* unmanaged[Cdecl]<nint, void>)
                Import(nameof(napi_module_register));
        }

        fixed (napi_module* module_ptr = &module)
        {
            napi_module_register((nint)module_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        node_api_get_module_file_name;

    public override napi_status GetModuleFileName(napi_env env, out string result)
    {
        byte* result_ptr = null;
        byte** result_ptr_ptr = &result_ptr;
        napi_status status = Import(ref node_api_get_module_file_name)(env, (nint)result_ptr_ptr);
        result = (status == napi_status.napi_ok ? PtrToStringUTF8(result_ptr) : null)!;
        return status;
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_get_node_version;

    public override napi_status GetNodeVersion(napi_env env, out napi_node_version result)
    {
        fixed (napi_node_version* result_ptr = &result)
        {
            return Import(ref napi_get_node_version)(env, (nint)result_ptr);
        }
    }

    #endregion

#pragma warning restore IDE1006
}
