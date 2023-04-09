// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.JavaScript.NodeApi;
using static Hermes.Example.HermesApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Hermes.Example;

public static class HermesApi
{
    public static void Load(string hermesEnginePath)
    {
        nint hermesLib = NativeLibrary.Load(hermesEnginePath);
        Interop.Initialize(hermesLib);
        JSNativeApi.Interop.Initialize(hermesLib);
    }

    public static void ThrowIfFailed(
        [DoesNotReturnIf(true)] this hermes_status status,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (status == hermes_status.hermes_ok)
            return;

        throw new Exception($"Error in {memberName} at {sourceFilePath}:{sourceLineNumber}");
    }

    public static TResult ThrowIfFailed<TResult>(
        [DoesNotReturnIf(true)] this hermes_status status,
        TResult result,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (status == hermes_status.hermes_ok)
            return result;

        throw new Exception($"Error in {memberName} at {sourceFilePath}:{sourceLineNumber}");
    }

    [SuppressUnmanagedCodeSecurity]
    public static unsafe class Interop
    {
        private static nint s_libraryHandle;
        private static FunctionFields s_fields = new();
        private static bool s_initialized;

        public static void Initialize(nint libraryHandle = default)
        {
            if (s_initialized) return;
            s_initialized = true;

            if (libraryHandle == default)
            {
#if NET7_0_OR_GREATER
                libraryHandle = NativeLibrary.GetMainProgramHandle();
#else
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    libraryHandle = GetModuleHandleW(default);
                }
                else
                {
                    libraryHandle = dlopen(default, RTLD_LAZY);
                }
#endif
            }

            s_libraryHandle = libraryHandle;
        }

        private struct FunctionFields
        {
            // hermes_api.h APIs
            public nint hermes_create_runtime;
            public nint hermes_delete_runtime;
            public nint hermes_get_node_api_env;
            public nint hermes_dump_crash_data;
            public nint hermes_sampling_profiler_enable;
            public nint hermes_sampling_profiler_disable;
            public nint hermes_sampling_profiler_add;
            public nint hermes_sampling_profiler_remove;
            public nint hermes_sampling_profiler_dump_to_file;

            public nint hermes_create_config;
            public nint hermes_delete_config;
            public nint hermes_config_enable_default_crash_handler;
            public nint hermes_config_enable_debugger;
            public nint hermes_config_set_debugger_runtime_name;
            public nint hermes_config_set_debugger_port;
            public nint hermes_config_set_debugger_break_on_start;
            public nint hermes_config_set_task_runner;
            public nint hermes_config_set_script_cache;

            public nint hermes_set_inspector;

            public nint hermes_create_local_connection;
            public nint hermes_delete_local_connection;
            public nint hermes_local_connection_send_message;
            public nint hermes_local_connection_disconnect;
        }

        public enum hermes_status : int
        {
            hermes_ok,
            hermes_error,
        }

        public record struct hermes_config(nint Handle);
        public record struct hermes_runtime(nint Handle);
        public record struct hermes_local_connection(nint Handle);
        public record struct hermes_remote_connection(nint Handle);

        public record struct hermes_data_delete_cb(nint Handle)
        {
            public hermes_data_delete_cb(delegate* unmanaged[Cdecl]<nint, nint, void> handle)
                : this((nint)handle) { }
        }

        //=============================================================================
        // hermes_runtime
        //=============================================================================

        internal static hermes_status hermes_create_runtime(
            hermes_config config, out hermes_runtime runtime)
            => CallInterop(ref s_fields.hermes_create_runtime, config.Handle, out runtime);

        internal static hermes_status hermes_delete_runtime(hermes_runtime runtime)
            => CallInterop(ref s_fields.hermes_delete_runtime, runtime.Handle);

        internal static hermes_status hermes_get_node_api_env(hermes_runtime runtime, out napi_env env)
            => CallInterop(ref s_fields.hermes_get_node_api_env, runtime.Handle, out env);

        internal static hermes_status hermes_dump_crash_data(hermes_runtime runtime, int fd)
            => CallInterop(ref s_fields.hermes_dump_crash_data, runtime.Handle, fd);

        internal static hermes_status hermes_sampling_profiler_enable()
            => CallInterop(ref s_fields.hermes_sampling_profiler_enable);

        internal static hermes_status hermes_sampling_profiler_disable()
            => CallInterop(ref s_fields.hermes_sampling_profiler_disable);

        internal static hermes_status hermes_sampling_profiler_add(hermes_runtime runtime)
            => CallInterop(ref s_fields.hermes_sampling_profiler_add, runtime.Handle);

        internal static hermes_status hermes_sampling_profiler_remove(hermes_runtime runtime)
            => CallInterop(ref s_fields.hermes_sampling_profiler_remove, runtime.Handle);

        internal static hermes_status hermes_sampling_profiler_dump_to_file(byte* filename)
            => CallInterop(ref s_fields.hermes_sampling_profiler_dump_to_file, (nint)filename);

        //=============================================================================
        // hermes_config
        //=============================================================================

        internal static hermes_status hermes_create_config(out hermes_config config)
            => CallInterop(ref s_fields.hermes_create_config, out config);

        internal static hermes_status hermes_delete_config(hermes_config config)
            => CallInterop(ref s_fields.hermes_delete_config, config.Handle);

        internal static hermes_status hermes_config_enable_default_crash_handler(
            hermes_config config, c_bool value)
            => CallInterop(
                ref s_fields.hermes_config_enable_default_crash_handler, config.Handle, value);

        internal static hermes_status hermes_config_enable_debugger(hermes_config config, c_bool value)
            => CallInterop(ref s_fields.hermes_config_enable_debugger, config.Handle, value);

        internal static hermes_status hermes_config_set_debugger_runtime_name(
            hermes_config config, byte* name)
            => CallInterop(
                ref s_fields.hermes_config_set_debugger_runtime_name, config.Handle, (nint)name);

        internal static hermes_status hermes_config_set_debugger_port(
            hermes_config config, ushort port)
            => CallInterop(ref s_fields.hermes_config_set_debugger_port, config.Handle, port);

        internal static hermes_status hermes_config_set_debugger_break_on_start(
            hermes_config config, c_bool value)
            => CallInterop(
                ref s_fields.hermes_config_set_debugger_break_on_start, config.Handle, value);

        //=============================================================================
        // hermes_config task runner
        //=============================================================================

        // A callback to run task
        // typedef void (NAPI_CDECL* hermes_task_run_cb) (void* task_data);
        public record struct hermes_task_run_cb(nint Handle)
        {
            public hermes_task_run_cb(delegate* unmanaged[Cdecl]<nint, void> handle)
                : this((nint)handle) { }
        }

        // A callback to post task to the task runner
        //typedef void (NAPI_CDECL* hermes_task_runner_post_task_cb) (
        //    void* task_runner_data,
        //    void* task_data,
        //    hermes_task_run_cb task_run_cb,
        //    hermes_data_delete_cb task_data_delete_cb,
        //    void* deleter_data);
        public record struct hermes_task_runner_post_task_cb(nint Handle)
        {
            public hermes_task_runner_post_task_cb(
                delegate* unmanaged[Cdecl]<
                    nint, nint, hermes_task_run_cb, hermes_data_delete_cb, nint, void> handle)
                : this((nint)handle) { }
        }

        internal static hermes_status hermes_config_set_task_runner(
            hermes_config config,
            nint task_runner_data,
            hermes_task_runner_post_task_cb task_runner_post_task_cb,
            hermes_data_delete_cb task_runner_data_delete_cb,
            nint deleter_data)
            => CallInterop(
                ref s_fields.hermes_config_set_task_runner,
                config.Handle,
                task_runner_data,
                task_runner_post_task_cb.Handle,
                task_runner_data_delete_cb.Handle,
                deleter_data);

        //=============================================================================
        // hermes_config script cache
        //=============================================================================

        public struct hermes_script_cache_metadata
        {
            public nint source_url;
            public ulong source_hash;
            public nint runtime_name;
            public ulong runtime_version;
            public nint tag;
        }

        //typedef void (NAPI_CDECL* hermes_script_cache_load_cb) (
        //    void* script_cache_data,
        //    hermes_script_cache_metadata* script_metadata,
        //    const uint8_t** buffer,
        //    size_t * buffer_size,
        //    hermes_data_delete_cb * buffer_delete_cb,
        //    void** deleter_data);
        public record struct hermes_script_cache_load_cb(nint Handle)
        {
            public hermes_script_cache_load_cb(
                delegate* unmanaged[Cdecl]<
                    nint, hermes_script_cache_metadata*, nint, nint, nint, nint, void> handle)
                : this((nint)handle) { }
        }

        //typedef void (NAPI_CDECL* hermes_script_cache_store_cb) (
        //    void* script_cache_data,
        //    hermes_script_cache_metadata* script_metadata,
        //    const uint8_t* buffer,
        //    size_t buffer_size,
        //    hermes_data_delete_cb buffer_delete_cb,
        //    void* deleter_data);
        public record struct hermes_script_cache_store_cb(nint Handle)
        {
            public hermes_script_cache_store_cb(
                delegate* unmanaged[Cdecl]<
                    nint,
                    hermes_script_cache_metadata*,
                    nint,
                    nuint,
                    hermes_data_delete_cb,
                    nint,
                    void> handle)
                : this((nint)handle) { }
        }

        internal static hermes_status hermes_config_set_script_cache(
            hermes_config config,
            nint script_cache_data,
            hermes_script_cache_load_cb script_cache_load_cb,
            hermes_script_cache_store_cb script_cache_store_cb,
            hermes_data_delete_cb script_cache_data_delete_cb,
            nint deleter_data)
            => CallInterop(
                ref s_fields.hermes_config_set_script_cache,
                config.Handle,
                script_cache_data,
                script_cache_load_cb.Handle,
                script_cache_store_cb.Handle,
                script_cache_data_delete_cb.Handle,
                deleter_data);

        //=============================================================================
        // Setting inspector singleton
        //=============================================================================

        //typedef int32_t(NAPI_CDECL* hermes_inspector_add_page_cb)(
        //    const char* title,
        //    const char* vm,
        //    void* connectFunc);
        public record struct hermes_inspector_add_page_cb(nint Handle)
        {
            public hermes_inspector_add_page_cb(
                delegate* unmanaged[Cdecl]<nint, nint, nint, void> handle)
                : this((nint)handle) { }
        }

        //typedef void (NAPI_CDECL* hermes_inspector_remove_page_cb) (int32_t page_id);
        public record struct hermes_inspector_remove_page_cb(nint Handle)
        {
            public hermes_inspector_remove_page_cb(
                delegate* unmanaged[Cdecl]<int, void> handle)
                : this((nint)handle) { }
        }

        internal static hermes_status hermes_set_inspector(
            hermes_inspector_add_page_cb add_page_cb,
            hermes_inspector_remove_page_cb remove_page_cb)
            => CallInterop(
                ref s_fields.hermes_set_inspector,
                add_page_cb.Handle,
                remove_page_cb.Handle);

        //=============================================================================
        // Local and remote inspector connections.
        // Local is defined in Hermes VM, Remote is defined by inspector outside of VM.
        //=============================================================================

        //typedef void (NAPI_CDECL* hermes_remote_connection_send_message_cb) (
        //    hermes_remote_connection remote_connection,
        //    const char* message);
        public record struct hermes_remote_connection_send_message_cb(nint Handle)
        {
            public hermes_remote_connection_send_message_cb(
                delegate* unmanaged[Cdecl]<nint, nint, void> handle)
                : this((nint)handle) { }
        }

        //typedef void (NAPI_CDECL* hermes_remote_connection_disconnect_cb) (
        //    hermes_remote_connection remote_connection);
        public record struct hermes_remote_connection_disconnect_cb(nint Handle)
        {
            public hermes_remote_connection_disconnect_cb(
                delegate* unmanaged[Cdecl]<nint, void> handle)
                : this((nint)handle) { }
        }

        internal static hermes_status hermes_create_local_connection(
            nint page_data,
            hermes_remote_connection remote_connection,
            hermes_remote_connection_send_message_cb on_send_message_cb,
            hermes_remote_connection_disconnect_cb on_disconnect_cb,
            hermes_data_delete_cb on_delete_cb,
            nint deleter_data,
            out hermes_local_connection local_connection)
            => CallInterop(
                ref s_fields.hermes_create_local_connection,
                page_data,
                remote_connection.Handle,
                on_send_message_cb.Handle,
                on_disconnect_cb.Handle,
                on_delete_cb.Handle,
                deleter_data,
                out local_connection);

        internal static hermes_status hermes_delete_local_connection(
            hermes_local_connection local_connection)
            => CallInterop(
                ref s_fields.hermes_delete_local_connection,
                local_connection.Handle);

        internal static hermes_status hermes_local_connection_send_message(
            hermes_local_connection local_connection,
            byte* message)
            => CallInterop(
                ref s_fields.hermes_local_connection_send_message,
                local_connection.Handle,
                (nint)message);

        internal static hermes_status hermes_local_connection_disconnect(
            hermes_local_connection local_connection)
            => CallInterop(
                ref s_fields.hermes_local_connection_disconnect,
                local_connection.Handle);

        private static nint GetExport(ref nint field, [CallerMemberName] string functionName = "")
        {
            nint methodPtr = field;
            if (methodPtr == default)
            {
                methodPtr = NativeLibrary.GetExport(s_libraryHandle, functionName);
                field = methodPtr;
            }

            return methodPtr;
        }

        private static hermes_status CallInterop(
            ref nint field,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<hermes_status>)funcHandle;
            return funcDelegate();
        }

        private static hermes_status CallInterop(
            ref nint field,
            nint value1,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<nint, hermes_status>)funcHandle;
            return funcDelegate(value1);
        }

        private static hermes_status CallInterop(
            ref nint field,
            nint value1,
            nint value2,
        [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<nint, nint, hermes_status>)funcHandle;
            return funcDelegate(value1, value2);
        }
        private static hermes_status CallInterop(
            ref nint field,
            nint value1,
            int value2,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<nint, int, hermes_status>)funcHandle;
            return funcDelegate(value1, value2);
        }

        private static hermes_status CallInterop(
            ref nint field,
            nint value1,
            ushort value2,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<nint, ushort, hermes_status>)funcHandle;
            return funcDelegate(value1, value2);
        }

        private static hermes_status CallInterop(
            ref nint field,
            nint value1,
            c_bool value2,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<nint, c_bool, hermes_status>)funcHandle;
            return funcDelegate(value1, value2);
        }

        private static hermes_status CallInterop(
            ref nint field,
            nint value1,
            nint value2,
            nint value3,
            nint value4,
            nint value5,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                nint, nint, nint, nint, nint, hermes_status>)funcHandle;
            return funcDelegate(value1, value2, value3, value4, value5);
        }

        private static hermes_status CallInterop(
            ref nint field,
            nint value1,
            nint value2,
            nint value3,
            nint value4,
            nint value5,
            nint value6,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                nint, nint, nint, nint, nint, nint, hermes_status>)funcHandle;
            return funcDelegate(value1, value2, value3, value4, value5, value6);
        }

        private static hermes_status CallInterop<TResult>(
            ref nint field,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<nint, hermes_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate((nint)result_native);
            }
        }

        private static hermes_status CallInterop<TResult>(
            ref nint field,
            nint value1,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<nint, nint, hermes_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate(value1, (nint)result_native);
            }
        }

        private static hermes_status CallInterop<TResult>(
            ref nint field,
            nint value1,
            nint value2,
            nint value3,
            nint value4,
            nint value5,
            nint value6,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                nint, nint, nint, nint, nint, nint, nint, hermes_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate(
                    value1, value2, value3, value4, value5, value6, (nint)result_native);
            }
        }
    }
}
