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
            public napi_threadsafe_function_call_js(
                delegate* unmanaged[Cdecl]<
                    napi_env /*env*/,
                    napi_value /*js_callback*/,
                    nint /*context*/,
                    nint /*data*/,
                    void> handle)
                => Handle = (nint)handle;

            public napi_threadsafe_function_call_js(Delegate callback)
                => Handle = Marshal.GetFunctionPointerForDelegate(callback);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void Delegate(napi_env env, napi_value js_callback, nint context, nint data);
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
        {
            nint funcHandle = GetExport(ref s_fields.napi_module_register);
            var funcDelegate = (delegate* unmanaged[Cdecl]<napi_module*, void>)funcHandle;
            funcDelegate(mod);
        }

        [DoesNotReturn]
        internal static void napi_fatal_error(string location, string message)
        {
            nint funcHandle = GetExport(ref s_fields.napi_fatal_error);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                nint, nuint, nint, nuint, void>)funcHandle;

            nint location_native = location == null ?
                default : StringToHGlobalUtf8(location);
            nint message_native = message == null ?
                default : StringToHGlobalUtf8(message);
            try
            {
                funcDelegate(location_native, NAPI_AUTO_LENGTH, message_native, NAPI_AUTO_LENGTH);
            }
            finally
            {
                if (location_native != default) Marshal.FreeCoTaskMem(location_native);
                if (message_native != default) Marshal.FreeCoTaskMem(message_native);
            }
            throw new InvalidOperationException("This line must be unreachable");
        }

        internal static napi_status napi_async_init(
            napi_env env,
            napi_value async_resource,
            napi_value async_resource_name,
            out napi_async_context result)
            => CallInterop(
                ref s_fields.napi_async_init,
                env,
                async_resource.Handle,
                async_resource_name.Handle,
                out result);

        internal static napi_status napi_async_destroy(
            napi_env env, napi_async_context async_context)
            => CallInterop(ref s_fields.napi_async_destroy, env, async_context.Handle);

        internal static napi_status napi_make_callback(
            napi_env env,
            napi_async_context async_context,
            napi_value recv,
            napi_value func,
            nuint argc,
            nint argv,
            out napi_value result)
            => CallInterop(
                ref s_fields.napi_make_callback,
                env,
                async_context.Handle,
                recv.Handle,
                func.Handle,
                (nint)argc,
                argv,
                out result);

        internal static napi_status napi_create_buffer(
            napi_env env, nuint length, nint* data, napi_value* result)
            => CallInterop(
                ref s_fields.napi_create_buffer,
                env,
                (nint)length,
                (nint)data,
                (nint)result);

        internal static napi_status napi_create_external_buffer(
            napi_env env,
            nuint length,
            nint data,
            napi_finalize finalize_cb,
            nint finalize_hint,
            out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_external_buffer,
                env,
                (nint)length,
                data,
                (nint)finalize_cb.Handle,
                finalize_hint,
                out result);

        internal static napi_status napi_create_buffer_copy(
            napi_env env,
            nuint length,
            nint data,
            out nint result_data,
            out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_buffer_copy);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                 napi_env, nuint, nint, nint*, napi_value*, napi_status>)funcHandle;
            fixed (nint* result_data_native = &result_data)
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, length, data, result_data_native, result_native);
            }
        }

        internal static napi_status napi_is_buffer(
            napi_env env, napi_value value, out c_bool result)
            => CallInterop(ref s_fields.napi_is_buffer, env, value.Handle, out result);

        internal static napi_status napi_get_buffer_info(
            napi_env env, napi_value value, nint* data, nuint* length)
            => CallInterop(
                ref s_fields.napi_get_buffer_info,
                env,
                value.Handle,
                (nint)data,
                (nint)length);

        internal static napi_status napi_create_async_work(
            napi_env env,
            napi_value async_resource,
            napi_value async_resource_name,
            napi_async_execute_callback execute,
            napi_async_complete_callback complete,
            nint data,
            out napi_async_work result)
            => CallInterop(
                ref s_fields.napi_create_async_work,
                env,
                async_resource.Handle,
                async_resource_name.Handle,
                (nint)execute.Handle,
                (nint)complete.Handle,
                data,
                out result);

        internal static napi_status napi_delete_async_work(napi_env env, napi_async_work work)
            => CallInterop(ref s_fields.napi_delete_async_work, env, work.Handle);

        internal static napi_status napi_queue_async_work(napi_env env, napi_async_work work)
            => CallInterop(ref s_fields.napi_queue_async_work, env, work.Handle);

        internal static napi_status napi_cancel_async_work(napi_env env, napi_async_work work)
            => CallInterop(ref s_fields.napi_cancel_async_work, env, work.Handle);

        internal static napi_status napi_get_node_version(napi_env env, out nint version)
            => CallInterop(ref s_fields.napi_get_node_version, env, out version);

        internal static napi_status napi_get_uv_event_loop(napi_env env, out uv_loop_t loop)
            => CallInterop(ref s_fields.napi_get_uv_event_loop, env, out loop);

        internal static napi_status napi_fatal_exception(napi_env env, napi_value err)
            => CallInterop(ref s_fields.napi_fatal_exception, env, err.Handle);

        internal static napi_status napi_add_env_cleanup_hook(
            napi_env env, napi_cleanup_hook fun, nint arg)
            => CallInterop(
                ref s_fields.napi_add_env_cleanup_hook, env, (nint)fun.Handle, arg);

        internal static napi_status napi_remove_env_cleanup_hook(
            napi_env env, napi_cleanup_hook fun, nint arg)
            => CallInterop(
                ref s_fields.napi_remove_env_cleanup_hook, env, (nint)fun.Handle, arg);

        internal static napi_status napi_open_callback_scope(
            napi_env env,
            napi_value resource_object,
            napi_async_context context,
            out napi_callback_scope result)
            => CallInterop(
                ref s_fields.napi_open_callback_scope,
                env,
                resource_object.Handle,
                context.Handle,
                out result);

        internal static napi_status napi_close_callback_scope(
            napi_env env, napi_callback_scope scope)
            => CallInterop(ref s_fields.napi_close_callback_scope, env, scope.Handle);

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
            nint funcHandle = GetExport(ref s_fields.napi_create_threadsafe_function);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
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
                napi_threadsafe_function*,
                napi_status>)funcHandle;
            fixed (napi_threadsafe_function* result_native = &result)
            {
                return funcDelegate(env,
                    func,
                    async_resource,
                    async_resource_name,
                    max_queue_size,
                    initial_thread_count,
                    thread_finalize_data,
                    thread_finalize_cb,
                    context,
                    call_js_cb,
                    result_native);
            }
        }

        internal static napi_status napi_get_threadsafe_function_context(
            napi_threadsafe_function func, out nint result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_get_threadsafe_function_context);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_threadsafe_function,
                nint*,
                napi_status>)funcHandle;
            fixed (nint* result_native = &result)
            {
                return funcDelegate(func, result_native);
            }
        }

        internal static napi_status napi_call_threadsafe_function(
            napi_threadsafe_function func,
            nint data,
            napi_threadsafe_function_call_mode is_blocking)
        {
            nint funcHandle = GetExport(ref s_fields.napi_call_threadsafe_function);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_threadsafe_function,
                nint,
                napi_threadsafe_function_call_mode,
                napi_status>)funcHandle;
            return funcDelegate(func, data, is_blocking);
        }

        internal static napi_status napi_acquire_threadsafe_function(napi_threadsafe_function func)
        {
            nint funcHandle = GetExport(ref s_fields.napi_acquire_threadsafe_function);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_threadsafe_function, napi_status>)funcHandle;
            return funcDelegate(func);
        }

        internal static napi_status napi_release_threadsafe_function(
            napi_threadsafe_function func,
            napi_threadsafe_function_release_mode mode)
        {
            nint funcHandle = GetExport(ref s_fields.napi_release_threadsafe_function);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_threadsafe_function,
                napi_threadsafe_function_release_mode,
                napi_status>)funcHandle;
            return funcDelegate(func, mode);
        }

        internal static napi_status napi_unref_threadsafe_function(
            napi_env env, napi_threadsafe_function func)
        {
            nint funcHandle = GetExport(ref s_fields.napi_unref_threadsafe_function);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, napi_threadsafe_function, napi_status>)funcHandle;
            return funcDelegate(env, func);
        }

        internal static napi_status napi_ref_threadsafe_function(
            napi_env env, napi_threadsafe_function func)
            => CallInterop(ref s_fields.napi_ref_threadsafe_function, env, func.Handle);

        internal static napi_status napi_add_async_cleanup_hook(
            napi_env env,
            napi_async_cleanup_hook hook,
            nint arg,
            out napi_async_cleanup_hook_handle remove_handle)
            => CallInterop(
                ref s_fields.napi_add_async_cleanup_hook,
                env,
                (nint)hook.Handle,
                arg,
                out remove_handle);

        internal static napi_status napi_remove_async_cleanup_hook(
            napi_async_cleanup_hook_handle remove_handle)
        {
            nint funcHandle = GetExport(ref s_fields.napi_remove_async_cleanup_hook);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_async_cleanup_hook_handle,
                napi_status>)funcHandle;
            return funcDelegate(remove_handle);
        }

        internal static napi_status node_api_get_module_file_name(napi_env env, byte** result)
        {
            nint funcHandle = GetExport(ref s_fields.node_api_get_module_file_name);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, byte**, napi_status>)funcHandle;
            return funcDelegate(env, result);
        }
    }
}
