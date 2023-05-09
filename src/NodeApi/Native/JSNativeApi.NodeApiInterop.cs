// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Definitions from Node.JS node_api.h and node_api_types.h

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi;

public static partial class JSNativeApi
{
    // Node-API Interop definitions and functions.
    public unsafe partial class Interop
    {
        public record struct napi_callback_scope(nint Handle);
        public record struct napi_async_context(nint Handle);
        public record struct napi_async_work(nint Handle);

        public struct napi_cleanup_hook
        {
            public delegate* unmanaged[Cdecl]<nint /*arg*/, void> Handle;
            public napi_cleanup_hook(delegate* unmanaged[Cdecl]<nint /*arg*/, void> handle)
                => Handle = handle;
        }

        public record struct napi_threadsafe_function(nint Handle);

        public enum napi_threadsafe_function_release_mode : int
        {
            napi_tsfn_release,
            napi_tsfn_abort
        }

        public enum napi_threadsafe_function_call_mode : int
        {
            napi_tsfn_nonblocking,
            napi_tsfn_blocking
        }

        public struct napi_async_execute_callback
        {
            public delegate* unmanaged[Cdecl]<napi_env /*env*/, void* /*data*/, void> Handle;
            public napi_async_execute_callback(
                delegate* unmanaged[Cdecl]<napi_env /*env*/, void* /*data*/, void> handle)
                => Handle = handle;
        }

        public struct napi_async_complete_callback
        {
            public delegate* unmanaged[Cdecl]<
                napi_env /*env*/, napi_status /*status*/, void* /*data*/, void> Handle;
            public napi_async_complete_callback(
                delegate* unmanaged[Cdecl]<
                    napi_env /*env*/, napi_status /*status*/, void* /*data*/, void> handle)
                => Handle = handle;
        }

        public struct napi_threadsafe_function_call_js
        {
            public nint Handle;

#if NETFRAMEWORK
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void Delegate(
                napi_env env, napi_value js_callback, nint context, nint data);

            public napi_threadsafe_function_call_js(Delegate callback)
                => Handle = Marshal.GetFunctionPointerForDelegate(callback);
#else
            public napi_threadsafe_function_call_js(
                delegate* unmanaged[Cdecl]<
                    napi_env /*env*/,
                    napi_value /*js_callback*/,
                    nint /*context*/,
                    nint /*data*/,
                    void> handle)
                => Handle = (nint)handle;
#endif
        }

        public struct napi_node_version
        {
            public uint major;
            public uint minor;
            public uint patch;
            public byte* release;
        }

        public record struct napi_async_cleanup_hook_handle(nint Handle);

        public struct napi_async_cleanup_hook
        {
            public delegate* unmanaged[Cdecl]<
                napi_async_cleanup_hook_handle /*handle*/, void* /*data*/, void> Handle;
            public napi_async_cleanup_hook(
                delegate* unmanaged[Cdecl]<
                    napi_async_cleanup_hook_handle /*handle*/, void* /*data*/, void> handle)
                => Handle = handle;
        }

        public struct napi_addon_register_func
        {
            public delegate* unmanaged[Cdecl]<
                napi_env /*env*/, napi_value /*exports*/, napi_value> Handle;
            public napi_addon_register_func(
                delegate* unmanaged[Cdecl]<
                    napi_env /*env*/, napi_value /*exports*/, napi_value> handle)
                => Handle = handle;
        }

        public struct napi_module
        {
            public int nm_version;
            public uint nm_flags;
            public byte* nm_filename;
            public napi_addon_register_func nm_register_func;
            public byte* nm_modname;
            public nuint nm_priv;
            public nuint reserved0;
            public nuint reserved1;
            public nuint reserved2;
            public nuint reserved3;
        }

        public record struct uv_loop_t(nint Handle);

        internal static void napi_module_register(napi_module* mod)
            => s_funcs.napi_module_register((nint)mod);

        [DoesNotReturn]
        internal static void napi_fatal_error(string location, string message)
        {
            nint location_ptr = location == null ? default : StringToHGlobalUtf8(location);
            nint message_ptr = message == null ? default : StringToHGlobalUtf8(message);
            try
            {
                s_funcs.napi_fatal_error(
                    location_ptr, NAPI_AUTO_LENGTH, message_ptr, NAPI_AUTO_LENGTH);
            }
            finally
            {
                if (location_ptr != default) Marshal.FreeHGlobal(location_ptr);
                if (message_ptr != default) Marshal.FreeHGlobal(message_ptr);
            }
            throw new InvalidOperationException("This line must be unreachable");
        }

        internal static napi_status napi_async_init(
            napi_env env,
            napi_value async_resource,
            napi_value async_resource_name,
            out napi_async_context result)
        {
            result = default;
            fixed (napi_async_context* result_ptr = &result)
            {
                return s_funcs.napi_async_init(
                    env, async_resource, async_resource_name, (nint)result_ptr);
            }
        }

        internal static napi_status napi_async_destroy(
            napi_env env, napi_async_context async_context)
            => s_funcs.napi_async_destroy(env, async_context);

        internal static napi_status napi_make_callback(
            napi_env env,
            napi_async_context async_context,
            napi_value recv,
            napi_value func,
            nuint argc,
            nint argv,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_make_callback(
                    env, async_context, recv, func, argc, argv, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_buffer(
            napi_env env, nuint length, nint* data, napi_value* result)
            => s_funcs.napi_create_buffer(env, length, (nint)data, (nint)result);

        internal static napi_status napi_create_external_buffer(
            napi_env env,
            nuint length,
            nint data,
            napi_finalize finalize_cb,
            nint finalize_hint,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_external_buffer(
                    env, length, data, finalize_cb, finalize_hint, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_buffer_copy(
            napi_env env,
            nuint length,
            nint data,
            out nint result_data,
            out napi_value result)
        {
            result_data = default;
            result = default;
            fixed (nint* result_data_ptr = &result_data)
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_buffer_copy(
                    env, length, data, (nint)result_data_ptr, (nint)result_ptr);
            }
        }

        internal static napi_status napi_is_buffer(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_buffer(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_buffer_info(
            napi_env env, napi_value value, nint* data, nuint* length)
            => s_funcs.napi_get_buffer_info(env, value, (nint)data, (nint)length);

        internal static napi_status napi_create_async_work(
            napi_env env,
            napi_value async_resource,
            napi_value async_resource_name,
            napi_async_execute_callback execute,
            napi_async_complete_callback complete,
            nint data,
            out napi_async_work result)
        {
            result = default;
            fixed (napi_async_work* result_ptr = &result)
            {
                return s_funcs.napi_create_async_work(
                    env,
                    async_resource,
                    async_resource_name,
                    execute,
                    complete,
                    data,
                    (nint)result_ptr);
            }
        }

        internal static napi_status napi_delete_async_work(napi_env env, napi_async_work work)
            => s_funcs.napi_delete_async_work(env, work);

        internal static napi_status napi_queue_async_work(napi_env env, napi_async_work work)
            => s_funcs.napi_queue_async_work(env, work);

        internal static napi_status napi_cancel_async_work(napi_env env, napi_async_work work)
            => s_funcs.napi_cancel_async_work(env, work);

        internal static napi_status napi_get_node_version(napi_env env, out nint version)
        {
            version = default;
            fixed (nint* version_ptr = &version)
            {
                return s_funcs.napi_get_node_version(env, (nint)version_ptr);
            }
        }

        internal static napi_status napi_get_uv_event_loop(napi_env env, out uv_loop_t loop)
        {
            loop = default;
            fixed (uv_loop_t* loop_ptr = &loop)
            {
                return s_funcs.napi_get_uv_event_loop(env, (nint)loop_ptr);
            }
        }

        internal static napi_status napi_fatal_exception(napi_env env, napi_value err)
            => s_funcs.napi_fatal_exception(env, err);

        internal static napi_status napi_add_env_cleanup_hook(
            napi_env env, napi_cleanup_hook fun, nint arg)
            => s_funcs.napi_add_env_cleanup_hook(env, fun, arg);

        internal static napi_status napi_remove_env_cleanup_hook(
            napi_env env, napi_cleanup_hook fun, nint arg)
            => s_funcs.napi_remove_env_cleanup_hook(env, fun, arg);

        internal static napi_status napi_open_callback_scope(
            napi_env env,
            napi_value resource_object,
            napi_async_context context,
            out napi_callback_scope result)
        {
            result = default;
            fixed (napi_callback_scope* result_ptr = &result)
            {
                return s_funcs.napi_open_callback_scope(
                    env, resource_object, context, (nint)result_ptr);
            }
        }

        internal static napi_status napi_close_callback_scope(
            napi_env env, napi_callback_scope scope)
            => s_funcs.napi_close_callback_scope(env, scope);

        internal static napi_status napi_create_threadsafe_function(
            napi_env env,
            napi_value func,
            napi_value async_resource,
            napi_value async_resource_name,
            nuint max_queue_size,
            nuint initial_thread_count,
            nint thread_finalize_data,
            napi_finalize thread_finalize_cb,
            nint context,
            napi_threadsafe_function_call_js call_js_cb,
            out napi_threadsafe_function result)
        {
            result = default;
            fixed (napi_threadsafe_function* result_ptr = &result)
            {
                return s_funcs.napi_create_threadsafe_function(
                    env,
                    func,
                    async_resource,
                    async_resource_name,
                    max_queue_size,
                    initial_thread_count,
                    thread_finalize_data,
                    thread_finalize_cb,
                    context,
                    call_js_cb,
                    (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_threadsafe_function_context(
            napi_threadsafe_function func, out nint result)
        {
            result = default;
            fixed (nint* result_ptr = &result)
            {
                return s_funcs.napi_get_threadsafe_function_context(func, (nint)result_ptr);
            }
        }

        internal static napi_status napi_call_threadsafe_function(
            napi_threadsafe_function func,
            nint data,
            napi_threadsafe_function_call_mode is_blocking)
            => s_funcs.napi_call_threadsafe_function(func, data, is_blocking);

        internal static napi_status napi_acquire_threadsafe_function(napi_threadsafe_function func)
            => s_funcs.napi_acquire_threadsafe_function(func);

        internal static napi_status napi_release_threadsafe_function(
            napi_threadsafe_function func,
            napi_threadsafe_function_release_mode mode)
            => s_funcs.napi_release_threadsafe_function(func, mode);

        internal static napi_status napi_unref_threadsafe_function(
            napi_env env, napi_threadsafe_function func)
            => s_funcs.napi_unref_threadsafe_function(env, func);

        internal static napi_status napi_ref_threadsafe_function(
            napi_env env, napi_threadsafe_function func)
            => s_funcs.napi_ref_threadsafe_function(env, func);

        internal static napi_status napi_add_async_cleanup_hook(
            napi_env env,
            napi_async_cleanup_hook hook,
            nint arg,
            out napi_async_cleanup_hook_handle remove_handle)
        {
            remove_handle = default;
            fixed (napi_async_cleanup_hook_handle* remove_handle_ptr = &remove_handle)
            {
                return s_funcs.napi_add_async_cleanup_hook(
                    env, hook, arg, (nint)remove_handle_ptr);
            }
        }

        internal static napi_status napi_remove_async_cleanup_hook(
            napi_async_cleanup_hook_handle remove_handle)
            => s_funcs.napi_remove_async_cleanup_hook(remove_handle);

        internal static napi_status node_api_get_module_file_name(napi_env env, byte** result)
            => s_funcs.node_api_get_module_file_name(env, (nint)result);
    }
}
