// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.Runtime;

public unsafe class NodejsRuntime : JSRuntime
{
#pragma warning disable IDE1006 // Naming: missing prefix '_'

    private readonly nint _libraryHandle;

    public NodejsRuntime(nint libraryHandle = default)
    {
        _libraryHandle = libraryHandle != default ?
            libraryHandle : NativeLibrary.GetMainProgramHandle();
    }

    private nint GetExport(string functionName)
        => NativeLibrary.GetExport(_libraryHandle, functionName);

#pragma warning disable CS0169 // Field is never used
#pragma warning disable IDE0044 // Make field readonly

    //--------------------------------------------------------------------------------------
    // js_native_api.h APIs (sorted alphabetically)
    //--------------------------------------------------------------------------------------

    private delegate* unmanaged[Cdecl]<
        napi_env, napi_value, nint, napi_finalize, nint, nint, napi_status>
        napi_add_finalizer;
    private delegate* unmanaged[Cdecl]<napi_env, long, nint, napi_status>
        napi_adjust_external_memory;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
        napi_check_object_type_tag;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_coerce_to_bool;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_coerce_to_number;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_coerce_to_object;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_coerce_to_string;
    private delegate* unmanaged[Cdecl]<napi_env, long, nint, napi_status>
        napi_create_bigint_int64;
    private delegate* unmanaged[Cdecl]<napi_env, ulong, nint, napi_status>
        napi_create_bigint_uint64;
    private delegate* unmanaged[Cdecl]<napi_env, int, nuint, nint, nint, napi_status>
        napi_create_bigint_words;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
        napi_get_arraybuffer_info;
    private delegate* unmanaged[Cdecl]<
        napi_env, napi_value, nint, nint, nint, nint, napi_status>
        napi_get_dataview_info;
    private delegate* unmanaged[Cdecl]<napi_env, napi_callback_info, nint, napi_status>
        napi_get_new_target;
    private delegate* unmanaged[Cdecl]<
        napi_env, napi_value, nint, nint, nint, nint, nint, napi_status>
        napi_get_typedarray_info;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
        napi_get_value_bigint_int64;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
        napi_get_value_bigint_uint64;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, nint, napi_status>
        napi_get_value_bigint_words;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
        napi_object_freeze;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
        napi_object_seal;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_type_tag_object;

    //--------------------------------------------------------------------------------------
    // node_api.h APIs (sorted alphabetically)
    //--------------------------------------------------------------------------------------

    private delegate* unmanaged[Cdecl]<napi_threadsafe_function, napi_status>
        napi_acquire_threadsafe_function;
    private delegate* unmanaged[Cdecl]<
        napi_env, napi_async_cleanup_hook, nint, nint, napi_status>
        napi_add_async_cleanup_hook;
    private delegate* unmanaged[Cdecl]<napi_env, napi_cleanup_hook, nint, napi_status>
        napi_add_env_cleanup_hook;
    private delegate* unmanaged[Cdecl]<napi_env, napi_async_context, napi_status>
        napi_async_destroy;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_async_init;
    private delegate* unmanaged[Cdecl]<
        napi_threadsafe_function, nint, napi_threadsafe_function_call_mode, napi_status>
        napi_call_threadsafe_function;
    private delegate* unmanaged[Cdecl]<napi_env, napi_async_work, napi_status>
        napi_cancel_async_work;
    private delegate* unmanaged[Cdecl]<napi_env, napi_callback_scope, napi_status>
        napi_close_callback_scope;
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
    private delegate* unmanaged[Cdecl]<napi_env, nuint, nint, nint, napi_status>
        napi_create_buffer;
    private delegate* unmanaged[Cdecl]<napi_env, nuint, nint, nint, nint, napi_status>
        napi_create_buffer_copy;
    private delegate* unmanaged[Cdecl]<
        napi_env, nuint, nint, napi_finalize, nint, nint, napi_status>
        napi_create_external_buffer;
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
    private delegate* unmanaged[Cdecl]<napi_env, napi_async_work, napi_status>
        napi_delete_async_work;
    private delegate* unmanaged[Cdecl]<nint, nuint, nint, nuint, void>
        napi_fatal_error;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
        napi_fatal_exception;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
        napi_get_buffer_info;
    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_get_node_version;
    private delegate* unmanaged[Cdecl]<napi_threadsafe_function, nint, napi_status>
        napi_get_threadsafe_function_context;
    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_get_uv_event_loop;
    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_is_buffer;
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
    private delegate* unmanaged[Cdecl]<nint, void>
        napi_module_register;
    private delegate* unmanaged[Cdecl]<
        napi_env, napi_value, napi_async_context, nint, napi_status>
        napi_open_callback_scope;
    private delegate* unmanaged[Cdecl]<napi_env, napi_async_work, napi_status>
        napi_queue_async_work;
    private delegate* unmanaged[Cdecl]<napi_env, napi_threadsafe_function, napi_status>
        napi_ref_threadsafe_function;
    private delegate* unmanaged[Cdecl]<
        napi_threadsafe_function, napi_threadsafe_function_release_mode, napi_status>
        napi_release_threadsafe_function;
    private delegate* unmanaged[Cdecl]<napi_async_cleanup_hook_handle, napi_status>
        napi_remove_async_cleanup_hook;
    private delegate* unmanaged[Cdecl]<napi_env, napi_cleanup_hook, nint, napi_status>
        napi_remove_env_cleanup_hook;
    private delegate* unmanaged[Cdecl]<napi_env, napi_threadsafe_function, napi_status>
        napi_unref_threadsafe_function;
    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        node_api_get_module_file_name;

    //--------------------------------------------------------------------------------------
    // Embedding APIs
    //--------------------------------------------------------------------------------------

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_await_promise;
    private delegate* unmanaged[Cdecl]<
        napi_platform, napi_error_message_handler, nint, nint, napi_status>
        napi_create_environment;
    private delegate* unmanaged[Cdecl]<
        int, nint, int, nint, napi_error_message_handler, nint, napi_status>
        napi_create_platform;
    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_destroy_environment;
    private delegate* unmanaged[Cdecl]<napi_platform, napi_status>
        napi_destroy_platform;
    private delegate* unmanaged[Cdecl]<napi_env, napi_status>
        napi_run_environment;

#pragma warning restore CS0169
#pragma warning restore IDE0044

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status> napi_get_version;

    public override napi_status GetVersion(napi_env env, out uint result)
    {
        if (napi_get_version == null)
        {
            napi_get_version = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                GetExport(nameof(napi_get_version));
        }

        result = default;
        fixed (uint* result_ptr = &result)
        {
            return napi_get_version(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_run_script;

    public override napi_status RunScript(napi_env env, napi_value script, out napi_value result)
    {
        if (napi_run_script == null)
        {
            napi_run_script = (delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_run_script));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_run_script(env, script, (nint)result_ptr);
        }
    }

    #region Instance data

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_get_instance_data;

    public override napi_status GetInstanceData(napi_env env, out nint result)
    {
        if (napi_get_instance_data == null)
        {
            napi_get_instance_data = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_status>)GetExport(nameof(napi_get_instance_data));
        }

        result = default;
        fixed (nint* result_ptr = &result)
        {
            return napi_get_instance_data(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_finalize, nint, napi_status>
        napi_set_instance_data;

    public override napi_status SetInstanceData(
        napi_env env,
        nint data,
        napi_finalize finalize_cb,
        nint finalize_hint)
    {
        if (napi_set_instance_data == null)
        {
            napi_set_instance_data = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_finalize, nint, napi_status>)
                GetExport(nameof(napi_set_instance_data));
        }

        return napi_set_instance_data(env, data, finalize_cb, finalize_hint);
    }

    #endregion

    #region Error handling

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_create_error;

    public override napi_status CreateError(
        napi_env env, napi_value code, napi_value msg, out napi_value result)
    {
        if (napi_create_error == null)
        {
            napi_create_error = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(napi_create_error));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_error(env, code, msg, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_create_type_error;

    public override napi_status CreateTypeError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result)
    {
        if (napi_create_type_error == null)
        {
            napi_create_type_error = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(napi_create_type_error));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_type_error(env, code, msg, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_create_range_error;

    public override napi_status CreateRangeError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result)
    {
        if (napi_create_range_error == null)
        {
            napi_create_range_error = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(napi_create_range_error));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_range_error(env, code, msg, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        node_api_create_syntax_error;

    public override napi_status CreateSyntaxError(
        napi_env env,
        napi_value code,
        napi_value msg,
        out napi_value result)
    {
        if (node_api_create_syntax_error == null)
        {
            node_api_create_syntax_error = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(node_api_create_syntax_error));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return node_api_create_syntax_error(env, code, msg, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
        napi_throw;

    public override napi_status Throw(napi_env env, napi_value error)
    {
        if (napi_throw == null)
        {
            napi_throw = (delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>)
                GetExport(nameof(napi_throw));
        }

        return napi_throw(env, error);
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status> napi_throw_error;

    public override napi_status ThrowError(napi_env env, string? code, string msg)
    {
        if (napi_throw_error == null)
        {
            napi_throw_error = (delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>)
                GetExport(nameof(napi_throw_error));
        }

        using (PooledBuffer codeBuffer = PooledBuffer.FromStringUtf8(code))
        using (PooledBuffer msgBuffer = PooledBuffer.FromStringUtf8(msg))
            fixed (byte* code_ptr = &codeBuffer.Pin())
            fixed (byte* msg_ptr = &codeBuffer.Pin())
            {
                return napi_throw_error(
                    env,
                    code == null ? 0 : (nint)code_ptr,
                    msg == null ? 0 : (nint)msg_ptr);
            }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status> napi_throw_type_error;

    public override napi_status ThrowTypeError(napi_env env, string? code, string msg)
    {
        if (napi_throw_type_error == null)
        {
            napi_throw_type_error = (delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>)
                GetExport(nameof(napi_throw_type_error));
        }

        using (PooledBuffer codeBuffer = PooledBuffer.FromStringUtf8(code))
        using (PooledBuffer msgBuffer = PooledBuffer.FromStringUtf8(msg))
            fixed (byte* code_ptr = &codeBuffer.Pin())
            fixed (byte* msg_ptr = &codeBuffer.Pin())
            {
                return napi_throw_type_error(
                    env,
                    code == null ? 0 : (nint)code_ptr,
                    msg == null ? 0 : (nint)msg_ptr);
            }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>
        napi_throw_range_error;

    public override napi_status ThrowRangeError(napi_env env, string? code, string msg)
    {
        if (napi_throw_range_error == null)
        {
            napi_throw_range_error = (delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>)
                GetExport(nameof(napi_throw_range_error));
        }

        using (PooledBuffer codeBuffer = PooledBuffer.FromStringUtf8(code))
        using (PooledBuffer msgBuffer = PooledBuffer.FromStringUtf8(msg))
            fixed (byte* code_ptr = &codeBuffer.Pin())
            fixed (byte* msg_ptr = &codeBuffer.Pin())

            {
                return napi_throw_range_error(
                    env,
                    code == null ? 0 : (nint)code_ptr,
                    msg == null ? 0 : (nint)msg_ptr);
            }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>
        node_api_throw_syntax_error;

    public override napi_status ThrowSyntaxError(napi_env env, string? code, string msg)
    {
        if (node_api_throw_syntax_error == null)
        {
            node_api_throw_syntax_error =
                (delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>)
                    GetExport(nameof(node_api_throw_syntax_error));
        }

        using (PooledBuffer codeBuffer = PooledBuffer.FromStringUtf8(code))
        using (PooledBuffer msgBuffer = PooledBuffer.FromStringUtf8(msg))
            fixed (byte* code_ptr = &codeBuffer.Pin())
            fixed (byte* msg_ptr = &codeBuffer.Pin())
            {
                return node_api_throw_syntax_error(
                    env,
                    code == null ? 0 : (nint)code_ptr,
                    msg == null ? 0 : (nint)msg_ptr);
            }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status> napi_is_exception_pending;

    public override napi_status IsExceptionPending(napi_env env, out bool result)
    {
        if (napi_is_exception_pending == null)
        {
            napi_is_exception_pending = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                GetExport(nameof(napi_is_exception_pending));
        }

        result = default;
        fixed (bool* result_ptr = &result)
        {
            return napi_is_exception_pending(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status> napi_get_last_error_info;

    public override napi_status GetLastErrorInfo(napi_env env, out napi_extended_error_info result)
    {
        if (napi_get_last_error_info == null)
        {
            napi_get_last_error_info = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                GetExport(nameof(napi_get_last_error_info));
        }

        result = default;
        fixed (napi_extended_error_info* result_ptr = &result)
        {
            return napi_is_exception_pending(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_get_and_clear_last_exception;

    public override napi_status GetAndClearLastException(napi_env env, out napi_value result)
    {
        if (napi_get_and_clear_last_exception == null)
        {
            napi_get_and_clear_last_exception =
                (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetExport(nameof(napi_get_and_clear_last_exception));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_and_clear_last_exception(env, (nint)result_ptr);
        }
    }

    #endregion

    #region Value type checking

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_typeof;

    public override napi_status GetValueType(
        napi_env env,
        napi_value value,
        out napi_valuetype result)
    {
        if (napi_typeof == null)
        {
            napi_typeof = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_typeof));
        }

        result = default;
        fixed (napi_valuetype* result_ptr = &result)
        {
            return napi_typeof(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_is_date;

    public override napi_status IsDate(napi_env env, napi_value value, out bool result)
    {
        if (napi_is_date == null)
        {
            napi_is_date = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_is_date));
        }

        c_bool resultBool = default;
        c_bool* result_ptr = &resultBool;
        napi_status status = napi_is_date(env, value, (nint)result_ptr);
        result = (bool)resultBool;
        return status;
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_is_promise;

    public override napi_status IsPromise(napi_env env, napi_value value, out bool result)
    {
        if (napi_is_promise == null)
        {
            napi_is_promise = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_is_promise));
        }

        c_bool resultBool = default;
        c_bool* result_ptr = &resultBool;
        napi_status status = napi_is_promise(env, value, (nint)result_ptr);
        result = (bool)resultBool;
        return status;
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_is_error;

    public override napi_status IsError(napi_env env, napi_value value, out bool result)
    {
        if (napi_is_error == null)
        {
            napi_is_error = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_is_error));
        }

        c_bool resultBool = default;
        c_bool* result_ptr = &resultBool;
        napi_status status = napi_is_error(env, value, (nint)result_ptr);
        result = (bool)resultBool;
        return status;
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_is_array;

    public override napi_status IsArray(napi_env env, napi_value value, out bool result)
    {
        if (napi_is_array == null)
        {
            napi_is_array = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_is_array));
        }

        c_bool resultBool = default;
        c_bool* result_ptr = &resultBool;
        napi_status status = napi_is_array(env, value, (nint)result_ptr);
        result = (bool)resultBool;
        return status;
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_is_arraybuffer;

    public override napi_status IsArrayBuffer(napi_env env, napi_value value, out bool result)
    {
        if (napi_is_arraybuffer == null)
        {
            napi_is_arraybuffer = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_is_arraybuffer));
        }

        c_bool resultBool = default;
        c_bool* result_ptr = &resultBool;
        napi_status status = napi_is_arraybuffer(env, value, (nint)result_ptr);
        result = (bool)resultBool;
        return status;
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_is_detached_arraybuffer;

    public override napi_status IsDetachedArrayBuffer(
        napi_env env,
        napi_value value,
        out bool result)
    {
        if (napi_is_detached_arraybuffer == null)
        {
            napi_is_detached_arraybuffer = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_is_detached_arraybuffer));
        }

        c_bool resultBool = default;
        c_bool* result_ptr = &resultBool;
        napi_status status = napi_is_detached_arraybuffer(env, value, (nint)result_ptr);
        result = (bool)resultBool;
        return status;
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_is_typedarray;

    public override napi_status IsTypedArray(napi_env env, napi_value value, out bool result)
    {
        if (napi_is_typedarray == null)
        {
            napi_is_typedarray = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_is_typedarray));
        }

        c_bool resultBool = default;
        c_bool* result_ptr = &resultBool;
        napi_status status = napi_is_typedarray(env, value, (nint)result_ptr);
        result = (bool)resultBool;
        return status;
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_is_dataview;

    public override napi_status IsDataView(napi_env env, napi_value value, out bool result)
    {
        if (napi_is_dataview == null)
        {
            napi_is_dataview = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_is_dataview));
        }

        c_bool resultBool = default;
        c_bool* result_ptr = &resultBool;
        napi_status status = napi_is_dataview(env, value, (nint)result_ptr);
        result = (bool)resultBool;
        return status;
    }

    #endregion

    #region Value retrieval

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_get_value_double;

    public override napi_status GetValueDouble(napi_env env, napi_value value, out double result)
    {
        if (napi_get_value_double == null)
        {
            napi_get_value_double = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_value_double));
        }

        result = default;
        fixed (double* result_ptr = &result)
        {
            return napi_get_value_double(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_get_value_int32;

    public override napi_status GetValueInt32(napi_env env, napi_value value, out int result)
    {
        if (napi_get_value_int32 == null)
        {
            napi_get_value_int32 = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_value_int32));
        }

        result = default;
        fixed (int* result_ptr = &result)
        {
            return napi_get_value_int32(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_get_value_uint32;

    public override napi_status GetValueUInt32(napi_env env, napi_value value, out uint result)
    {
        if (napi_get_value_uint32 == null)
        {
            napi_get_value_uint32 = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_value_uint32));
        }

        result = default;
        fixed (uint* result_ptr = &result)
        {
            return napi_get_value_uint32(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_get_value_int64;

    public override napi_status GetValueInt64(napi_env env, napi_value value, out long result)
    {
        if (napi_get_value_int64 == null)
        {
            napi_get_value_int64 = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_value_int64));
        }

        result = default;
        fixed (long* result_ptr = &result)
        {
            return napi_get_value_int64(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_get_value_bool;

    public override napi_status GetValueBool(napi_env env, napi_value value, out bool result)
    {
        if (napi_get_value_bool == null)
        {
            napi_get_value_bool = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_value_bool));
        }

        result = default;
        fixed (bool* result_ptr = &result)
        {
            return napi_get_value_bool(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nuint, nint, napi_status>
        napi_get_value_string_utf8;

    public override napi_status GetValueStringUtf8(
        napi_env env, napi_value value, Span<byte> buf, out int result)
    {
        if (napi_get_value_string_utf8 == null)
        {
            napi_get_value_string_utf8 = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, nuint, nint, napi_status>)
                GetExport(nameof(napi_get_value_string_utf8));
        }

        fixed (int* result_ptr = &result)
        fixed (byte* buf_ptr = &buf.GetPinnableReference())
        {
            return napi_get_value_string_utf8(
                env, value, (nint)buf_ptr, (nuint)buf.Length, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nuint, nint, napi_status>
        napi_get_value_string_utf16;

    public override napi_status GetValueStringUtf16(
        napi_env env, napi_value value, Span<char> buf, out int result)
    {
        if (napi_get_value_string_utf16 == null)
        {
            napi_get_value_string_utf16 = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, nuint, nint, napi_status>)
                GetExport(nameof(napi_get_value_string_utf16));
        }

        fixed (int* result_ptr = &result)
        fixed (char* buf_ptr = &buf.GetPinnableReference())
        {
            return napi_get_value_string_utf16(
                env, value, (nint)buf_ptr, (nuint)buf.Length, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_get_date_value;

    public override napi_status GetValueDate(napi_env env, napi_value value, out double result)
    {
        if (napi_get_date_value == null)
        {
            napi_get_date_value = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_date_value));
        }

        result = default;
        fixed (double* result_ptr = &result)
        {
            return napi_get_date_value(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, nuint, nint, napi_status>
        node_api_symbol_for;

    public override napi_status GetSymbolFor(napi_env env, string name, out napi_value result)
    {
        if (node_api_symbol_for == null)
        {
            node_api_symbol_for = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nuint, nint, napi_status>)
                GetExport(nameof(node_api_symbol_for));
        }

        result = default;
        using (PooledBuffer nameBuffer = PooledBuffer.FromStringUtf8(name))
            fixed (byte* name_ptr = &nameBuffer.Pin())
            fixed (napi_value* result_ptr = &result)
            {
                return node_api_symbol_for(
                    env,
                    name == null ? 0 : (nint)name_ptr,
                    (nuint)nameBuffer.Length,
                    (nint)result_ptr);
            }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_get_array_length;

    public override napi_status GetArrayLength(napi_env env, napi_value value, out int result)
    {
        if (napi_get_date_value == null)
        {
            napi_get_array_length = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_array_length));
        }

        result = default;
        fixed (int* result_ptr = &result)
        {
            return napi_get_array_length(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_get_value_external;

    public override napi_status GetValueExternal(napi_env env, napi_value value, out nint result)
    {
        if (napi_get_value_external == null)
        {
            napi_get_value_external = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_value_external));
        }

        result = default;
        fixed (nint* result_ptr = &result)
        {
            return napi_get_value_external(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_strict_equals;

    public override napi_status StrictEquals(
        napi_env env, napi_value lhs, napi_value rhs, out bool result)
    {
        if (napi_strict_equals == null)
        {
            napi_strict_equals = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(napi_strict_equals));
        }

        result = default;
        fixed (bool* result_ptr = &result)
        {
            return napi_strict_equals(env, lhs, rhs, (nint)result_ptr);
        }
    }

    #endregion

    #region Value creation

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status> napi_get_global;

    public override napi_status GetGlobal(napi_env env, out napi_value result)
    {
        if (napi_get_global == null)
        {
            napi_get_global = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_status>)
                GetExport(nameof(napi_get_global));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_global(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status> napi_get_undefined;

    public override napi_status GetUndefined(napi_env env, out napi_value result)
    {
        if (napi_get_undefined == null)
        {
            napi_get_undefined = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_status>)
                GetExport(nameof(napi_get_undefined));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_undefined(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status> napi_get_null;

    public override napi_status GetNull(napi_env env, out napi_value result)
    {
        if (napi_get_null == null)
        {
            napi_get_null = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_status>)
                GetExport(nameof(napi_get_undefined));
        }


        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_null(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, c_bool, nint, napi_status> napi_get_boolean;

    public override napi_status GetBoolean(napi_env env, bool value, out napi_value result)
    {
        if (napi_get_boolean == null)
        {
            napi_get_boolean = (delegate* unmanaged[Cdecl]<
                napi_env, c_bool, nint, napi_status>)
                GetExport(nameof(napi_get_boolean));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_boolean(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, double, nint, napi_status> napi_create_double;

    public override napi_status CreateNumber(napi_env env, double value, out napi_value result)
    {
        if (napi_create_double == null)
        {
            napi_create_double = (delegate* unmanaged[Cdecl]<
                napi_env, double, nint, napi_status>)
                GetExport(nameof(napi_create_double));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_double(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, int, nint, napi_status>
        napi_create_int32;

    public override napi_status CreateNumber(napi_env env, int value, out napi_value result)
    {
        if (napi_create_int32 == null)
        {
            napi_create_int32 = (delegate* unmanaged[Cdecl]<
                napi_env, int, nint, napi_status>)
                GetExport(nameof(napi_create_int32));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_int32(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, uint, nint, napi_status>
        napi_create_uint32;

    public override napi_status CreateNumber(napi_env env, uint value, out napi_value result)
    {
        if (napi_create_uint32 == null)
        {
            napi_create_uint32 = (delegate* unmanaged[Cdecl]<
                napi_env, uint, nint, napi_status>)
                GetExport(nameof(napi_create_uint32));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_uint32(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, long, nint, napi_status>
        napi_create_int64;

    public override napi_status CreateNumber(napi_env env, long value, out napi_value result)
    {
        if (napi_create_int64 == null)
        {
            napi_create_int64 = (delegate* unmanaged[Cdecl]<
                napi_env, long, nint, napi_status>)
                GetExport(nameof(napi_create_int64));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_int64(env, value, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, nuint, nint, napi_status>
        napi_create_string_utf8;

    public override napi_status CreateString(
        napi_env env, ReadOnlySpan<byte> utf8Str, out napi_value result)
    {
        if (napi_create_string_utf8 == null)
        {
            napi_create_string_utf8 = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nuint, nint, napi_status>)
                GetExport(nameof(napi_create_string_utf8));
        }

        fixed (byte* str_ptr = &utf8Str.GetPinnableReference())
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_string_utf8(
                env, (nint)str_ptr, (nuint)utf8Str.Length, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, nuint, nint, napi_status>
        napi_create_string_utf16;

    public override napi_status CreateString(
        napi_env env, ReadOnlySpan<char> utf16Str, out napi_value result)
    {
        if (napi_create_string_utf16 == null)
        {
            napi_create_string_utf16 = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nuint, nint, napi_status>)
                GetExport(nameof(napi_create_string_utf16));
        }

        fixed (char* str_ptr = &utf16Str.GetPinnableReference())
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_string_utf16(
                env, (nint)str_ptr, (nuint)utf16Str.Length, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, double, nint, napi_status> napi_create_date;

    public override napi_status CreateDate(napi_env env, double time, out napi_value result)
    {
        if (napi_create_date == null)
        {
            napi_create_date = (delegate* unmanaged[Cdecl]<
                napi_env, double, nint, napi_status>)
                GetExport(nameof(napi_create_date));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_date(env, time, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_create_symbol;

    public override napi_status CreateSymbol(
        napi_env env, napi_value description, out napi_value result)
    {
        if (napi_create_symbol == null)
        {
            napi_create_symbol = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_create_symbol));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_symbol(env, description, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status> napi_create_object;

    public override napi_status CreateObject(napi_env env, out napi_value result)
    {
        if (napi_create_object == null)
        {
            napi_create_object = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_status>)
                GetExport(nameof(napi_create_object));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_object(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status> napi_create_array;

    public override napi_status CreateArray(napi_env env, out napi_value result)
    {
        if (napi_create_array == null)
        {
            napi_create_array = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_status>)
                GetExport(nameof(napi_create_array));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_array(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nuint, nint, napi_status>
        napi_create_array_with_length;

    public override napi_status CreateArray(napi_env env, int length, out napi_value result)
    {
        if (napi_create_array_with_length == null)
        {
            napi_create_array_with_length = (delegate* unmanaged[Cdecl]<
                napi_env, nuint, nint, napi_status>)
                GetExport(nameof(napi_create_array_with_length));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_array_with_length(env, (nuint)length, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nuint, nint, nint, napi_status>
        napi_create_arraybuffer;

    public override napi_status CreateArrayBuffer(
        napi_env env,
        int byte_length,
        out nint data,
        out napi_value result)
    {
        if (napi_create_arraybuffer == null)
        {
            napi_create_arraybuffer = (delegate* unmanaged[Cdecl]<
                napi_env, nuint, nint, nint, napi_status>)
                GetExport(nameof(napi_create_arraybuffer));
        }

        data = default;
        result = default;
        fixed (nint* data_ptr = &data)
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_arraybuffer(
                env, (nuint)byte_length, (nint)data_ptr, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, nint, nuint, napi_finalize, nint, nint, napi_status>
        napi_create_external_arraybuffer;

    public override napi_status CreateArrayBuffer(
        napi_env env,
        nint external_data,
        int byte_length,
        napi_finalize finalize_cb,
        nint finalize_hint,
        out napi_value result)
    {
        if (napi_create_external_arraybuffer == null)
        {
            napi_create_external_arraybuffer = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nuint, napi_finalize, nint, nint, napi_status>)
                GetExport(nameof(napi_create_external_arraybuffer));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_external_arraybuffer(
                env,
                external_data,
                (nuint)byte_length,
                finalize_cb,
                finalize_hint,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
        napi_detach_arraybuffer;

    public override napi_status DetachArrayBuffer(napi_env env, napi_value arraybuffer)
    {
        if (napi_detach_arraybuffer == null)
        {
            napi_detach_arraybuffer = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_status>)
                GetExport(nameof(napi_detach_arraybuffer));
        }

        return napi_detach_arraybuffer(env, arraybuffer);
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, napi_typedarray_type, nuint, napi_value, nuint, nint, napi_status>
        napi_create_typedarray;

    public override napi_status CreateTypedArray(
        napi_env env,
        napi_typedarray_type type,
        int length,
        napi_value arraybuffer,
        int byte_offset,
        out napi_value result)
    {
        if (napi_create_typedarray == null)
        {
            napi_create_typedarray = (delegate* unmanaged[Cdecl]<
                napi_env, napi_typedarray_type, nuint, napi_value, nuint, nint, napi_status>)
                GetExport(nameof(napi_create_typedarray));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_typedarray(
                env, type, (nuint)length, arraybuffer, (nuint)byte_offset, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, nuint, napi_value, nuint, nint, napi_status>
        napi_create_dataview;

    public override napi_status CreateDataView(
        napi_env env,
        int length,
        napi_value arraybuffer,
        int byte_offset,
        out napi_value result)
    {
        if (napi_create_dataview == null)
        {
            napi_create_dataview = (delegate* unmanaged[Cdecl]<
                napi_env, nuint, napi_value, nuint, nint, napi_status>)
                GetExport(nameof(napi_create_dataview));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_dataview(
                env, (nuint)length, arraybuffer, (nuint)byte_offset, (nint)result_ptr);
        }
    }


    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_finalize, nint, nint, napi_status>
        napi_create_external;

    public override napi_status CreateExternal(
        napi_env env,
        nint data,
        napi_finalize finalize_cb,
        nint finalize_hint,
        out napi_value result)
    {
        if (napi_create_external == null)
        {
            napi_create_external = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_finalize, nint, nint, napi_status>)
                GetExport(nameof(napi_create_external));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_create_external(env, data, finalize_cb, finalize_hint, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, nint, nuint, napi_callback, nint, nint, napi_status>
        napi_create_function;

    public override napi_status CreateFunction(
        napi_env env,
        string? name,
        napi_callback cb,
        nint data,
        out napi_value result)
    {
        if (napi_create_function == null)
        {
            napi_create_function = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nuint, napi_callback, nint, nint, napi_status>)
                GetExport(nameof(napi_create_function));
        }

        using (PooledBuffer nameBuffer = PooledBuffer.FromStringUtf8(name))
            fixed (byte* name_ptr = &nameBuffer.Pin())
            fixed (napi_value* result_ptr = &result)
            {
                return napi_create_function(
                    env,
                    name == null ? 0 : (nint)name_ptr,
                    (nuint)nameBuffer.Length,
                    cb,
                    data,
                    (nint)result_ptr);
            }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status> napi_create_promise;

    public override napi_status CreatePromise(
        napi_env env, out napi_deferred deferred, out napi_value promise)
    {
        if (napi_create_promise == null)
        {
            napi_create_promise = (delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>)
                GetExport(nameof(napi_create_promise));
        }

        deferred = default;
        promise = default;
        fixed (napi_deferred* deferred_ptr = &deferred)
        fixed (napi_value* result_ptr = &promise)
        {
            return napi_create_promise(env, (nint)deferred_ptr, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_deferred, napi_value, napi_status>
        napi_resolve_deferred;

    public override napi_status ResolveDeferred(
        napi_env env, napi_deferred deferred, napi_value resolution)
    {
        if (napi_resolve_deferred == null)
        {
            napi_resolve_deferred = (delegate* unmanaged[Cdecl]<
                napi_env, napi_deferred, napi_value, napi_status>)
                GetExport(nameof(napi_resolve_deferred));
        }

        return napi_resolve_deferred(env, deferred, resolution);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_deferred, napi_value, napi_status>
        napi_reject_deferred;

    public override napi_status RejectDeferred(
        napi_env env, napi_deferred deferred, napi_value rejection)
    {
        if (napi_reject_deferred == null)
        {
            napi_reject_deferred = (delegate* unmanaged[Cdecl]<
                napi_env, napi_deferred, napi_value, napi_status>)
                GetExport(nameof(napi_reject_deferred));
        }

        return napi_reject_deferred(env, deferred, rejection);
    }

    #endregion

    #region Value coercion

    #endregion

    #region Handle scopes

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_open_handle_scope;

    public override napi_status OpenHandleScope(napi_env env, out napi_handle_scope result)
    {
        if (napi_open_handle_scope == null)
        {
            napi_open_handle_scope = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_status>)
                GetExport(nameof(napi_open_handle_scope));
        }

        fixed (napi_handle_scope* result_ptr = &result)
        {
            return napi_open_handle_scope(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_handle_scope, napi_status>
        napi_close_handle_scope;

    public override napi_status CloseHandleScope(napi_env env, napi_handle_scope scope)
    {
        if (napi_close_handle_scope == null)
        {
            napi_close_handle_scope = (delegate* unmanaged[Cdecl]<
                napi_env, napi_handle_scope, napi_status>)
                GetExport(nameof(napi_close_handle_scope));
        }

        return napi_close_handle_scope(env, scope);
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_open_escapable_handle_scope;

    public override napi_status OpenEscapableHandleScope(
        napi_env env,
        out napi_escapable_handle_scope result)
    {
        if (napi_open_escapable_handle_scope == null)
        {
            napi_open_escapable_handle_scope = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_status>)
                GetExport(nameof(napi_open_escapable_handle_scope));
        }

        fixed (napi_escapable_handle_scope* result_ptr = &result)
        {
            return napi_open_escapable_handle_scope(env, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_escapable_handle_scope, napi_status>
        napi_close_escapable_handle_scope;

    public override napi_status CloseEscapableHandleScope(
        napi_env env, napi_escapable_handle_scope scope)
    {
        if (napi_close_escapable_handle_scope == null)
        {
            napi_close_escapable_handle_scope = (delegate* unmanaged[Cdecl]<
                napi_env, napi_escapable_handle_scope, napi_status>)
                GetExport(nameof(napi_close_escapable_handle_scope));
        }

        return napi_close_escapable_handle_scope(env, scope);
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, napi_escapable_handle_scope, napi_value, nint, napi_status>
        napi_escape_handle;

    public override napi_status EscapeHandle(
        napi_env env,
        napi_escapable_handle_scope scope,
        napi_value escapee,
        out napi_value result)
    {
        if (napi_escape_handle == null)
        {
            napi_escape_handle = (delegate* unmanaged[Cdecl]<
                napi_env, napi_escapable_handle_scope, napi_value, nint, napi_status>)
                GetExport(nameof(napi_escape_handle));
        }

        fixed (napi_value* result_ptr = &result)
        {
            return napi_escape_handle(env, scope, escapee, (nint)result_ptr);
        }
    }

    #endregion

    #region References

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, nint, napi_status>
        napi_create_reference;

    public override napi_status CreateReference(
        napi_env env,
        napi_value value,
        uint initial_refcount,
        out napi_ref result)
    {
        if (napi_create_reference == null)
        {
            napi_create_reference = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, uint, nint, napi_status>)
                GetExport(nameof(napi_create_reference));
        }

        fixed (napi_ref* result_ptr = &result)
        {
            return napi_create_reference(env, value, initial_refcount, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_ref, napi_status>
        napi_delete_reference;

    public override napi_status DeleteReference(napi_env env, napi_ref @ref)
    {
        if (napi_delete_reference == null)
        {
            napi_delete_reference = (delegate* unmanaged[Cdecl]<
                napi_env, napi_ref, napi_status>)
                GetExport(nameof(napi_delete_reference));
        }

        return napi_delete_reference(env, @ref);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_ref, nint, napi_status>
        napi_reference_ref;

    public override napi_status RefReference(napi_env env, napi_ref @ref, out uint result)
    {
        if (napi_reference_ref == null)
        {
            napi_reference_ref = (delegate* unmanaged[Cdecl]<
                napi_env, napi_ref, nint, napi_status>)
                GetExport(nameof(napi_reference_ref));
        }

        fixed (uint* result_ptr = &result)
        {
            return napi_reference_ref(env, @ref, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_ref, nint, napi_status>
        napi_reference_unref;

    public override napi_status UnrefReference(napi_env env, napi_ref @ref, out uint result)
    {
        if (napi_reference_unref == null)
        {
            napi_reference_unref = (delegate* unmanaged[Cdecl]<
                napi_env, napi_ref, nint, napi_status>)
                GetExport(nameof(napi_reference_unref));
        }

        fixed (uint* result_ptr = &result)
        {
            return napi_reference_unref(env, @ref, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_ref, nint, napi_status>
        napi_get_reference_value;

    public override napi_status GetReferenceValue(
        napi_env env, napi_ref @ref, out napi_value result)
    {
        if (napi_get_reference_value == null)
        {
            napi_get_reference_value = (delegate* unmanaged[Cdecl]<
                napi_env, napi_ref, nint, napi_status>)
                GetExport(nameof(napi_get_reference_value));
        }

        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_reference_value(env, @ref, (nint)result_ptr);
        }
    }

    #endregion

    #region Function calls

    private delegate* unmanaged[Cdecl]<
        napi_env, napi_value, napi_value, nuint, nint, nint, napi_status>
        napi_call_function;

    public override napi_status CallFunction(
        napi_env env,
        napi_value recv,
        napi_value func,
        ReadOnlySpan<napi_value> args,
        out napi_value result)
    {
        if (napi_call_function == null)
        {
            napi_call_function = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nuint, nint, nint, napi_status>)
                GetExport(nameof(napi_call_function));
        }

        nuint argc = (nuint)args.Length;
        result = default;
        fixed (napi_value* argv_ptr = &args.GetPinnableReference())
        fixed (napi_value* result_ptr = &result)
        {
            return napi_call_function(env, recv, func, argc, (nint)argv_ptr, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, napi_callback_info, nint, nint, nint, nint, napi_status>
        napi_get_cb_info;

    public override napi_status GetCallbackInfo(
        napi_env env,
        napi_callback_info cbinfo,
        out int argc,
        out nint data)
    {
        if (napi_get_cb_info == null)
        {
            napi_get_cb_info = (delegate* unmanaged[Cdecl]<
                napi_env, napi_callback_info, nint, nint, nint, nint, napi_status>)
                GetExport(nameof(napi_get_cb_info));
        }

        argc = default;
        data = default;
        fixed (int* argc_ptr = &argc)
        fixed (nint* data_ptr = &data)
        {
            return napi_get_cb_info(env, cbinfo, (nint)argc_ptr, default, default, (nint)data_ptr);
        }
    }

    public override napi_status GetCallbackArgs(
        napi_env env,
        napi_callback_info cbinfo,
        Span<napi_value> args,
        out napi_value this_arg)
    {
        if (napi_get_cb_info == null)
        {
            napi_get_cb_info = (delegate* unmanaged[Cdecl]<
                napi_env, napi_callback_info, nint, nint, nint, nint, napi_status>)
                GetExport(nameof(napi_get_cb_info));
        }

        nint argc = args.Length;
        nint* argc_ptr = &argc;
        this_arg = default;
        fixed (napi_value* argv_ptr = &args.GetPinnableReference())
        fixed (napi_value* this_ptr = &this_arg)
        {
            return napi_get_cb_info(
                env, cbinfo, (nint)argc_ptr, (nint)argv_ptr, (nint)this_ptr, default);
        }
    }

    #endregion

    #region Object properties

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_has_property;

    public override napi_status HasProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result)
    {
        if (napi_has_property == null)
        {
            napi_has_property = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(napi_has_property));
        }

        result = default;
        fixed (bool* result_ptr = &result)
        {
            return napi_has_property(env, js_object, key, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_has_own_property;

    public override napi_status HasOwnProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result)
    {
        if (napi_has_own_property == null)
        {
            napi_has_own_property = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(napi_has_own_property));
        }

        result = default;
        fixed (bool* result_ptr = &result)
        {
            return napi_has_own_property(env, js_object, key, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_get_property;

    public override napi_status GetProperty(
        napi_env env, napi_value js_object, napi_value key, out napi_value result)
    {
        if (napi_get_property == null)
        {
            napi_get_property = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_property));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_property(env, js_object, key, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, napi_value, napi_value, napi_value, napi_status>
        napi_set_property;

    public override napi_status SetProperty(
        napi_env env, napi_value js_object, napi_value key, napi_value value)
    {
        if (napi_set_property == null)
        {
            napi_set_property = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, napi_value, napi_status>)
                GetExport(nameof(napi_set_property));
        }

        return napi_set_property(env, js_object, key, value);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_delete_property;

    public override napi_status DeleteProperty(
        napi_env env, napi_value js_object, napi_value key, out bool result)
    {
        if (napi_delete_property == null)
        {
            napi_delete_property = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(napi_delete_property));
        }

        result = default;
        fixed (bool* result_ptr = &result)
        {
            return napi_delete_property(env, js_object, key, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
        napi_has_named_property;

    public override napi_status HasNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, out bool result)
    {
        if (napi_has_named_property == null)
        {
            napi_has_named_property = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, nint, napi_status>)
                GetExport(nameof(napi_has_named_property));
        }

        result = default;
        fixed (byte* name_ptr = &utf8name.GetPinnableReference())
        fixed (bool* result_ptr = &result)
        {
            return napi_has_named_property(env, js_object, (nint)name_ptr, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
        napi_get_named_property;

    public override napi_status GetNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, out napi_value result)
    {
        if (napi_get_named_property == null)
        {
            napi_get_named_property = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, nint, napi_status>)
                GetExport(nameof(napi_get_named_property));
        }

        result = default;
        fixed (byte* name_ptr = &utf8name.GetPinnableReference())
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_named_property(env, js_object, (nint)name_ptr, (nint)result_ptr);
        }
    }


    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_value, napi_status>
        napi_set_named_property;

    public override napi_status SetNamedProperty(
        napi_env env, napi_value js_object, ReadOnlySpan<byte> utf8name, napi_value value)
    {
        if (napi_set_named_property == null)
        {
            napi_set_named_property = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_value, napi_status>)
                GetExport(nameof(napi_set_property));
        }

        fixed (byte* name_ptr = &utf8name.GetPinnableReference())
        {
            return napi_set_named_property(env, js_object, (nint)name_ptr, value);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, nint, napi_status>
        napi_has_element;

    public override napi_status HasElement(
        napi_env env, napi_value js_object, uint index, out bool result)
    {
        if (napi_has_element == null)
        {
            napi_has_element = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, uint, nint, napi_status>)
                GetExport(nameof(napi_has_element));
        }

        result = default;
        fixed (bool* result_ptr = &result)
        {
            return napi_has_element(env, js_object, index, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, nint, napi_status>
        napi_get_element;

    public override napi_status GetElement(
        napi_env env, napi_value js_object, uint index, out napi_value result)
    {
        if (napi_get_element == null)
        {
            napi_get_element = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, uint, nint, napi_status>)
                GetExport(nameof(napi_get_element));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_element(env, js_object, index, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, napi_value, napi_status>
        napi_set_element;

    public override napi_status SetElement(
        napi_env env, napi_value js_object, uint index, napi_value value)
    {
        if (napi_set_element == null)
        {
            napi_set_element = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, uint, napi_value, napi_status>)
                GetExport(nameof(napi_set_element));
        }

        return napi_set_element(env, js_object, index, value);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, nint, napi_status>
        napi_delete_element;

    public override napi_status DeleteElement(
        napi_env env, napi_value js_object, uint index, out bool result)
    {
        if (napi_delete_element == null)
        {
            napi_delete_element = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, uint, nint, napi_status>)
                GetExport(nameof(napi_delete_element));
        }

        result = default;
        fixed (bool* result_ptr = &result)
        {
            return napi_delete_element(env, js_object, index, (nint)result_ptr);
        }
    }


    #endregion

    #region Property and class definition

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_get_property_names;

    public override napi_status GetPropertyNames(
        napi_env env,
        napi_value js_object,
        out napi_value result)
    {
        if (napi_get_property_names == null)
        {
            napi_get_property_names = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_property_names));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_property_names(env, js_object, (nint)result_ptr);
        }
    }


    private delegate* unmanaged[Cdecl]<
        napi_env,
        napi_value,
        napi_key_collection_mode,
        napi_key_filter,
        napi_key_conversion,
        nint,
        napi_status>
        napi_get_all_property_names;

    public override napi_status GetAllPropertyNames(
        napi_env env,
        napi_value js_object,
        napi_key_collection_mode key_mode,
        napi_key_filter key_filter,
        napi_key_conversion key_conversion,
        out napi_value result)
    {
        if (napi_get_all_property_names == null)
        {
            napi_get_all_property_names =
                (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_value,
                    napi_key_collection_mode,
                    napi_key_filter,
                    napi_key_conversion,
                    nint,
                    napi_status>)
                GetExport(nameof(napi_get_all_property_names));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_all_property_names(
                env, js_object, key_mode, key_filter, key_conversion, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nuint, nint, napi_status>
        napi_define_properties;

    public override napi_status DefineProperties(
        napi_env env,
        napi_value js_object,
        ReadOnlySpan<napi_property_descriptor> properties)
    {
        if (napi_define_properties == null)
        {
            napi_define_properties = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nuint, nint, napi_status>)
                GetExport(nameof(napi_define_properties));
        }

        fixed (napi_property_descriptor* properties_ptr = &properties.GetPinnableReference())
        {
            return napi_define_properties(
                env, js_object, (nuint)properties.Length, (nint)properties_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, nint, nuint, napi_callback, nint, nuint, nint, nint, napi_status>
        napi_define_class;

    public override napi_status DefineClass(
        napi_env env,
        string name,
        napi_callback constructor,
        nint data,
        ReadOnlySpan<napi_property_descriptor> properties,
        out napi_value result)
    {
        if (napi_define_class == null)
        {
            napi_define_class = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nuint, napi_callback, nint, nuint, nint, nint, napi_status>)
                GetExport(nameof(napi_define_class));
        }

        result = default;
        using (PooledBuffer nameBuffer = PooledBuffer.FromStringUtf8(name))
            fixed (byte* name_ptr = &nameBuffer.Pin())
            fixed (napi_property_descriptor* properties_ptr = &properties.GetPinnableReference())
            fixed (napi_value* result_ptr = &result)
            {
                return napi_define_class(
                    env,
                    name == null ? 0 : (nint)name_ptr,
                    (nuint)nameBuffer.Length,
                    constructor,
                    data,
                    (nuint)properties.Length,
                    (nint)properties_ptr,
                    (nint)result_ptr);
            }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_get_prototype;

    public override napi_status GetPrototype(
        napi_env env,
        napi_value js_object,
        out napi_value result)
    {
        if (napi_get_prototype == null)
        {
            napi_get_prototype = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_get_prototype));
        }

        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return napi_get_prototype(env, js_object, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nuint, nint, nint, napi_status>
        napi_new_instance;

    public override napi_status NewInstance(
        napi_env env,
        napi_value constructor,
        ReadOnlySpan<napi_value> args,
        out napi_value result)
    {
        if (napi_new_instance == null)
        {
            napi_new_instance = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nuint, nint, nint, napi_status>)
                GetExport(nameof(napi_new_instance));
        }

        result = default;
        fixed (napi_value* args_ptr = &args.GetPinnableReference())
        fixed (napi_value* result_ptr = &result)
        {
            return napi_new_instance(
                env, constructor, (nuint)args.Length, (nint)args_ptr, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
        napi_instanceof;

    public override napi_status InstanceOf(
        napi_env env,
        napi_value js_object,
        napi_value constructor,
        out bool result)
    {
        if (napi_instanceof == null)
        {
            napi_instanceof = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)
                GetExport(nameof(napi_instanceof));
        }

        result = default;
        fixed (bool* result_ptr = &result)
        {
            return napi_instanceof(env, js_object, constructor, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        napi_env, napi_value, nint, napi_finalize, nint, nint, napi_status>
        napi_wrap;

    public override napi_status Wrap(
        napi_env env,
        napi_value js_object,
        nint native_object,
        napi_finalize finalize_cb,
        nint finalize_hint,
        out napi_ref result)
    {
        if (napi_wrap == null)
        {
            napi_wrap = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_finalize, nint, nint, napi_status>)
                GetExport(nameof(napi_wrap));
        }

        result = default;
        fixed (napi_ref* result_ptr = &result)
        {
            return napi_wrap(
                env, js_object, native_object, finalize_cb, finalize_hint, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_unwrap;

    public override napi_status Unwrap(napi_env env, napi_value js_object, out nint result)
    {
        if (napi_unwrap == null)
        {
            napi_unwrap = (delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_unwrap));
        }

        result = default;
        fixed (nint* result_ptr = &result)
        {
            return napi_unwrap(env, js_object, (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status> napi_remove_wrap;

    public override napi_status RemoveWrap(napi_env env, napi_value js_object, out nint result)
    {
        if (napi_remove_wrap == null)
        {
            napi_remove_wrap = (delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>)
                GetExport(nameof(napi_remove_wrap));
        }

        result = default;
        fixed (nint* result_ptr = &result)
        {
            return napi_remove_wrap(env, js_object, (nint)result_ptr);
        }
    }

    #endregion

#pragma warning restore IDE1006
}
