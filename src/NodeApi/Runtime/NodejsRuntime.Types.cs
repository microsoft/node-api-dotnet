// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.Runtime;

// Type definitions from Node.JS node_api.h and node_api_types.h
public unsafe partial class NodejsRuntime
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
}
