// Definitions from Node.JS node_api.h and node_api_types.h

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public static partial class JSNativeApi
{
    // Node-API Interop definitions and functions.
    [SuppressUnmanagedCodeSecurity]
    public static unsafe partial class NodeApiInterop
    {
        public record struct napi_callback_scope(nint Handle);
        public record struct napi_async_context(nint Handle);
        public record struct napi_async_work(nint Handle);

        public unsafe struct napi_cleanup_hook
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

        public unsafe struct napi_async_execute_callback
        {
            public delegate* unmanaged[Cdecl]<napi_env /*env*/, void* /*data*/, void> Handle;
            public napi_async_execute_callback(
                delegate* unmanaged[Cdecl]<napi_env /*env*/, void* /*data*/, void> handle)
                => Handle = handle;
        }

        public unsafe struct napi_async_complete_callback
        {
            public delegate* unmanaged[Cdecl]<napi_env /*env*/, napi_status /*status*/, void* /*data*/, void> Handle;
            public napi_async_complete_callback(
                delegate* unmanaged[Cdecl]<napi_env /*env*/, napi_status /*status*/, void* /*data*/, void> handle)
                => Handle = handle;
        }

        public unsafe struct napi_threadsafe_function_call_js
        {
            public delegate* unmanaged[Cdecl]<napi_env /*env*/, napi_value /*js_callback*/, nint /*context*/, nint /*data*/, void> Handle;
            public napi_threadsafe_function_call_js(
                delegate* unmanaged[Cdecl]<napi_env /*env*/, napi_value /*js_callback*/, nint /*context*/, nint /*data*/, void> handle)
                => Handle = handle;
        }

        public unsafe struct napi_node_version
        {
            public uint major;
            public uint minor;
            public uint patch;
            public byte* release;
        }

        public record struct napi_async_cleanup_hook_handle(nint Handle);

        public unsafe struct napi_async_cleanup_hook
        {
            public delegate* unmanaged[Cdecl]<napi_async_cleanup_hook_handle /*handle*/, void* /*data*/, void> Handle;
            public napi_async_cleanup_hook(
                delegate* unmanaged[Cdecl]<napi_async_cleanup_hook_handle /*handle*/, void* /*data*/, void> handle)
                => Handle = handle;
        }

        public unsafe struct napi_addon_register_func
        {
            public delegate* unmanaged[Cdecl]<napi_env /*env*/, napi_value /*exports*/, napi_value> Handle;
            public napi_addon_register_func(
                delegate* unmanaged[Cdecl]<napi_env /*env*/, napi_value /*exports*/, napi_value> handle)
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

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial void napi_module_register(napi_module* mod);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        [DoesNotReturn]
        internal static unsafe partial void napi_fatal_error(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string location,
            nuint location_len,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string message,
            nuint message_len);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_async_init(
            napi_env env,
            napi_value async_resource,
            napi_value async_resource_name,
            napi_async_context* result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_async_destroy(napi_env env, napi_async_context async_context);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_make_callback(
            napi_env env,
            napi_async_context async_context,
            napi_value recv,
            napi_value func,
            nuint argc,
            napi_value* argv,
            napi_value* result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_create_buffer(napi_env env, nuint length, void** data, napi_value* result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_create_external_buffer(
            napi_env env,
            nuint length,
            void* data,
            napi_finalize finalize_cb,
            void* finalize_hint,
            napi_value* result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_create_buffer_copy(
            napi_env env,
            nuint length,
            void* data,
            void** result_data,
            napi_value* result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_is_buffer(napi_env env, napi_value value, c_bool* result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_get_buffer_info(napi_env env, napi_value value, void** data, nuint* length);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_create_async_work(
            napi_env env,
            napi_value async_resource,
            napi_value async_resource_name,
            napi_async_execute_callback execute,
            napi_async_complete_callback complete,
            void* data,
            napi_async_work* result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_delete_async_work(napi_env env, napi_async_work work);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_queue_async_work(napi_env env, napi_async_work work);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_cancel_async_work(napi_env env, napi_async_work work);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_get_node_version(napi_env env, napi_node_version** version);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_get_uv_event_loop(napi_env env, uv_loop_t* loop);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_fatal_exception(napi_env env, napi_value err);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_add_env_cleanup_hook(napi_env env, napi_cleanup_hook fun, nint arg);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_remove_env_cleanup_hook(napi_env env, napi_cleanup_hook fun, nint arg);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_open_callback_scope(
            napi_env env,
            napi_value resource_object,
            napi_async_context context,
            napi_callback_scope* result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_close_callback_scope(napi_env env, napi_callback_scope scope);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_create_threadsafe_function(
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
            out napi_threadsafe_function result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_get_threadsafe_function_context(
            napi_threadsafe_function func,
            out nint result);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_call_threadsafe_function(
            napi_threadsafe_function func,
            nint data,
            napi_threadsafe_function_call_mode is_blocking);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_acquire_threadsafe_function(napi_threadsafe_function func);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_release_threadsafe_function(
            napi_threadsafe_function func,
            napi_threadsafe_function_release_mode mode);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status
        napi_unref_threadsafe_function(napi_env env, napi_threadsafe_function func);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_ref_threadsafe_function(napi_env env, napi_threadsafe_function func);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_add_async_cleanup_hook(
            napi_env env,
            napi_async_cleanup_hook hook,
            void* arg,
            napi_async_cleanup_hook_handle* remove_handle);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status napi_remove_async_cleanup_hook(napi_async_cleanup_hook_handle remove_handle);

        [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static unsafe partial napi_status node_api_get_module_file_name(napi_env env, byte** result);
    }
}
