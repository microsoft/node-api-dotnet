// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.Runtime;

// Type definitions from Node.JS js_native_api.h and js_native_api_types.h
public unsafe partial class JSRuntime
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate napi_value napi_register_module_v1(napi_env env, napi_value exports);

    //===========================================================================
    // Specialized pointer types
    //===========================================================================

    public record struct napi_env(nint Handle)
    {
        public readonly bool IsNull => Handle == default;
        public static napi_env Null => new(default);
    }
    public record struct napi_value(nint Handle)
    {
        public static napi_value Null => new(default);
        public readonly bool IsNull => Handle == default;
    }
    public record struct napi_ref(nint Handle);
    public record struct napi_handle_scope(nint Handle);
    public record struct napi_escapable_handle_scope(nint Handle);
    public record struct napi_callback_info(nint Handle);
    public record struct napi_deferred(nint Handle);

    public record struct node_embedding_platform(nint Handle);
    public record struct node_embedding_runtime(nint Handle);
    public record struct node_embedding_platform_config(nint Handle);
    public record struct node_embedding_runtime_config(nint Handle);
    public record struct node_embedding_node_api_scope(nint Handle);

    //===========================================================================
    // Enum types
    //===========================================================================

    public enum napi_property_attributes : int
    {
        napi_default = 0,
        napi_writable = 1 << 0,
        napi_enumerable = 1 << 1,
        napi_configurable = 1 << 2,

        // Used with napi_define_class to distinguish static properties
        // from instance properties. Ignored by napi_define_properties.
        napi_static = 1 << 10,

        // Default for class methods.
        napi_default_method = napi_writable | napi_configurable,

        // Default for object properties, like in JS obj[prop].
        napi_default_jsproperty = napi_writable | napi_enumerable | napi_configurable,
    }

    public enum napi_valuetype : int
    {
        // ES6 types (corresponds to typeof)
        napi_undefined,
        napi_null,
        napi_boolean,
        napi_number,
        napi_string,
        napi_symbol,
        napi_object,
        napi_function,
        napi_external,
        napi_bigint,
    }

    public enum napi_typedarray_type : int
    {
        napi_int8_array,
        napi_uint8_array,
        napi_uint8_clamped_array,
        napi_int16_array,
        napi_uint16_array,
        napi_int32_array,
        napi_uint32_array,
        napi_float32_array,
        napi_float64_array,
        napi_bigint64_array,
        napi_biguint64_array,
    }

    public enum napi_status : int
    {
        napi_ok,
        napi_invalid_arg,
        napi_object_expected,
        napi_string_expected,
        napi_name_expected,
        napi_function_expected,
        napi_number_expected,
        napi_boolean_expected,
        napi_array_expected,
        napi_generic_failure,
        napi_pending_exception,
        napi_cancelled,
        napi_escape_called_twice,
        napi_handle_scope_mismatch,
        napi_callback_scope_mismatch,
        napi_queue_full,
        napi_closing,
        napi_bigint_expected,
        napi_date_expected,
        napi_arraybuffer_expected,
        napi_detachable_arraybuffer_expected,
        napi_would_deadlock,
    }

    public enum node_embedding_status : int
    {
        ok = 0,
        generic_error = 1,
        null_arg = 2,
        bad_arg = 3,
        // This value is added to the exit code in cases when Node.js API returns
        // an error exit code.
        error_exit_code = 512,
    }

    [Flags]
    public enum node_embedding_platform_flags : int
    {
        none = 0,
        // Enable stdio inheritance, which is disabled by default.
        // This flag is also implied by
        // node_embedding_platform_flags_no_stdio_initialization.
        enable_stdio_inheritance = 1 << 0,
        // Disable reading the NODE_OPTIONS environment variable.
        disable_node_options_env = 1 << 1,
        // Do not parse CLI options.
        disable_cli_options = 1 << 2,
        // Do not initialize ICU.
        no_icu = 1 << 3,
        // Do not modify stdio file descriptor or TTY state.
        no_stdio_initialization = 1 << 4,
        // Do not register Node.js-specific signal handlers
        // and reset other signal handlers to default state.
        no_default_signal_handling = 1 << 5,
        // Do not initialize OpenSSL config.
        no_init_openssl = 1 << 8,
        // Do not initialize Node.js debugging based on environment variables.
        no_parse_global_debug_variables = 1 << 9,
        // Do not adjust OS resource limits for this process.
        no_adjust_resource_limits = 1 << 10,
        // Do not map code segments into large pages for this process.
        no_use_large_pages = 1 << 11,
        // Skip printing output for --help, --version, --v8-options.
        no_print_help_or_version_output = 1 << 12,
        // Initialize the process for predictable snapshot generation.
        generate_predictable_snapshot = 1 << 14,
    }

    // The flags for the Node.js runtime initialization.
    // They match the internal EnvironmentFlags::Flags enum.
    [Flags]
    public enum node_embedding_runtime_flags : int
    {
        none = 0,
        // Use the default behavior for Node.js instances.
        default_flags = 1 << 0,
        // Controls whether this Environment is allowed to affect per-process state
        // (e.g. cwd, process title, uid, etc.).
        // This is set when using default.
        owns_process_state = 1 << 1,
        // Set if this Environment instance is associated with the global inspector
        // handling code (i.e. listening on SIGUSR1).
        // This is set when using default.
        owns_inspector = 1 << 2,
        // Set if Node.js should not run its own esm loader. This is needed by some
        // embedders, because it's possible for the Node.js esm loader to conflict
        // with another one in an embedder environment, e.g. Blink's in Chromium.
        no_register_esm_loader = 1 << 3,
        // Set this flag to make Node.js track "raw" file descriptors, i.e. managed
        // by fs.open() and fs.close(), and close them during
        // node_embedding_delete_runtime().
        track_unmanaged_fds = 1 << 4,
        // Set this flag to force hiding console windows when spawning child
        // processes. This is usually used when embedding Node.js in GUI programs on
        // Windows.
        hide_console_windows = 1 << 5,
        // Set this flag to disable loading native addons via `process.dlopen`.
        // This environment flag is especially important for worker threads
        // so that a worker thread can't load a native addon even if `execArgv`
        // is overwritten and `--no-addons` is not specified but was specified
        // for this Environment instance.
        no_native_addons = 1 << 6,
        // Set this flag to disable searching modules from global paths like
        // $HOME/.node_modules and $NODE_PATH. This is used by standalone apps that
        // do not expect to have their behaviors changed because of globally
        // installed modules.
        no_global_search_paths = 1 << 7,
        // Do not export browser globals like setTimeout, console, etc.
        no_browser_globals = 1 << 8,
        // Controls whether or not the Environment should call V8Inspector::create().
        // This control is needed by embedders who may not want to initialize the V8
        // inspector in situations where one has already been created,
        // e.g. Blink's in Chromium.
        no_create_inspector = 1 << 9,
        // Controls whether or not the InspectorAgent for this Environment should
        // call StartDebugSignalHandler. This control is needed by embedders who may
        // not want to allow other processes to start the V8 inspector.
        no_start_debug_signal_handler = 1 << 10,
        // Controls whether the InspectorAgent created for this Environment waits for
        // Inspector frontend events during the Environment creation. It's used to
        // call node::Stop(env) on a Worker thread that is waiting for the events.
        no_wait_for_inspector_frontend = 1 << 11
    }

    public enum node_embedding_event_loop_run_mode : int
    {
        // Run the event loop until it is completed.
        // It matches the UV_RUN_DEFAULT behavior.
        default_mode = 0,
        // Run the event loop once and wait if there are no items.
        // It matches the UV_RUN_ONCE behavior.
        once = 1,
        // Run the event loop once and do not wait if there are no items.
        // It matches the UV_RUN_NOWAIT behavior.
        nowait = 2,
    }

    public record struct napi_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        /// <summary>TEST TEST TEST</summary>
        public napi_callback(
            delegate* unmanaged[Cdecl]<napi_env, napi_callback_info, napi_value> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate napi_value Delegate(napi_env env, napi_callback_info callbackInfo);

        public napi_callback(napi_callback.Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct napi_finalize(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public napi_finalize(delegate* unmanaged[Cdecl]<napi_env, nint, nint, void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(napi_env env, nint data, nint hint);

        public napi_finalize(napi_finalize.Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_release_data_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_release_data_callback(delegate* unmanaged[Cdecl]<nint, void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(nint data);

        public node_embedding_release_data_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_handle_error_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_handle_error_callback(delegate* unmanaged[Cdecl]<
            nint, nint, nuint, node_embedding_status, node_embedding_status> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate node_embedding_status Delegate(
            nint cb_data,
            nint messages,
            nuint messages_size,
            node_embedding_status status);

        public node_embedding_handle_error_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_configure_platform_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_configure_platform_callback(delegate* unmanaged[Cdecl]<
            nint, node_embedding_platform_config, node_embedding_status> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate node_embedding_status Delegate(
            nint cb_data,
            node_embedding_platform_config platform_config);

        public node_embedding_configure_platform_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_configure_runtime_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_configure_runtime_callback(delegate* unmanaged[Cdecl]<
            nint,
            node_embedding_platform,
            node_embedding_runtime_config,
            node_embedding_status> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate node_embedding_status Delegate(
            nint cb_data,
            node_embedding_platform platform,
            node_embedding_runtime_config runtime_config);

        public node_embedding_configure_runtime_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_get_args_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_get_args_callback(delegate* unmanaged[Cdecl]<
            nint, int, nint, void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(nint cb_data, int argc, nint argv);

        public node_embedding_get_args_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_preload_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_preload_callback(delegate* unmanaged[Cdecl]<
            nint, node_embedding_runtime, napi_env, napi_value, napi_value, void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_runtime runtime,
            napi_env env,
            napi_value process,
            napi_value require);

        public node_embedding_preload_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_start_execution_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_start_execution_callback(delegate* unmanaged[Cdecl]<
            nint,
            node_embedding_runtime,
            napi_env,
            napi_value,
            napi_value,
            napi_value,
            napi_value> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate napi_value Delegate(
            nint cb_data,
            node_embedding_runtime runtime,
            napi_env env,
            napi_value process,
            napi_value require,
            napi_value run_cjs);

        public node_embedding_start_execution_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_handle_result_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_handle_result_callback(delegate* unmanaged[Cdecl]<
            nint,
            node_embedding_runtime,
            napi_env,
            napi_value,
            void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_runtime runtime,
            napi_env env,
            napi_value value);

        public node_embedding_handle_result_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_initialize_module_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_initialize_module_callback(delegate* unmanaged[Cdecl]<
            nint,
            node_embedding_runtime,
            napi_env,
            nint,
            napi_value,
            napi_value> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate napi_value Delegate(
            nint cb_data,
            node_embedding_runtime runtime,
            napi_env env,
            nint module_name,
            napi_value exports);

        public node_embedding_initialize_module_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_run_task_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_run_task_callback(delegate* unmanaged[Cdecl]<nint, void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(nint cb_data);

        public node_embedding_run_task_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_post_task_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_post_task_callback(delegate* unmanaged[Cdecl]<
            nint,
            node_embedding_run_task_functor,
            void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_run_task_functor run_task);

        public node_embedding_post_task_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_run_node_api_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_run_node_api_callback(delegate* unmanaged[Cdecl]<
            nint,
            node_embedding_runtime,
            napi_env,
            void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_runtime runtime,
            napi_env env);

        public node_embedding_run_node_api_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public struct napi_property_descriptor
    {
        // One of utf8name or name should be NULL.
        public nint utf8name;
        public napi_value name;

        public napi_callback method;
        public napi_callback getter;
        public napi_callback setter;
        public napi_value value;

        public napi_property_attributes attributes;
        public nint data;
    }

    public struct napi_extended_error_info
    {
        public byte* error_message;
        public nint engine_reserved;
        public uint engine_error_code;
        public napi_status error_code;
    }

    public enum napi_key_collection_mode : int
    {
        napi_key_include_prototypes,
        napi_key_own_only,
    }

    [Flags]
    public enum napi_key_filter : int
    {
        napi_key_all_properties = 0,
        napi_key_writable = 1 << 0,
        napi_key_enumerable = 1 << 1,
        napi_key_configurable = 1 << 2,
        napi_key_skip_strings = 1 << 3,
        napi_key_skip_symbols = 1 << 4,
    }

    public enum napi_key_conversion : int
    {
        napi_key_keep_numbers,
        napi_key_numbers_to_strings,
    }

    public readonly struct c_bool
    {
        private readonly byte _value;

        public c_bool(bool value) => _value = (byte)(value ? 1 : 0);

        public static implicit operator c_bool(bool value) => new(value);
        public static explicit operator bool(c_bool value) => value._value != 0;

        public static readonly c_bool True = new(true);
        public static readonly c_bool False = new(false);
    }

    public struct node_embedding_handle_error_functor
    {
        public nint data;
        public node_embedding_handle_error_callback invoke;
        public node_embedding_release_data_callback release;
    }

    public struct node_embedding_get_args_functor_ref : IDisposable
    {
        public nint data;
        public node_embedding_get_args_callback invoke;

        public node_embedding_get_args_functor_ref(
            object? functor, node_embedding_get_args_callback invoke)
        {
            data = (functor != null) ? (nint)GCHandle.Alloc(functor) : default;
            this.invoke = (functor != null) ? invoke : new node_embedding_get_args_callback(0);
        }

        public void Dispose()
        {
            if (data == 0) return;
            GCHandle.FromIntPtr(data).Free();
            data = 0;
        }
    }

    public struct node_embedding_configure_platform_functor_ref : IDisposable
    {
        public nint data;
        public node_embedding_configure_platform_callback invoke;

        public node_embedding_configure_platform_functor_ref(
            object? functor, node_embedding_configure_platform_callback invoke)
        {
            data = (functor != null) ? (nint)GCHandle.Alloc(functor) : default;
            this.invoke = (functor != null)
                ? invoke
                : new node_embedding_configure_platform_callback(0);
        }

        public void Dispose()
        {
            if (data == 0) return;
            GCHandle.FromIntPtr(data).Free();
            data = 0;
        }
    }

    public struct node_embedding_configure_runtime_functor_ref
    {
        public nint data;
        public node_embedding_configure_runtime_callback invoke;

        public node_embedding_configure_runtime_functor_ref(
            object? functor, node_embedding_configure_runtime_callback invoke)
        {
            data = (functor != null) ? (nint)GCHandle.Alloc(functor) : default;
            this.invoke = (functor != null)
                ? invoke
                : new node_embedding_configure_runtime_callback(0);
        }

        public void Dispose()
        {
            if (data == 0) return;
            GCHandle.FromIntPtr(data).Free();
            data = 0;
        }
    }

    public struct node_embedding_preload_functor
    {
        public nint data;
        public node_embedding_preload_callback invoke;
        public node_embedding_release_data_callback release;
    }

    public struct node_embedding_start_execution_functor
    {
        public nint data;
        public node_embedding_start_execution_callback invoke;
        public node_embedding_release_data_callback release;
    }

    public struct node_embedding_handle_result_functor
    {
        public nint data;
        public node_embedding_handle_result_callback invoke;
        public node_embedding_release_data_callback release;
    }

    public struct node_embedding_initialize_module_functor
    {
        public nint data;
        public node_embedding_initialize_module_callback invoke;
        public node_embedding_release_data_callback release;
    }

    public struct node_embedding_run_task_functor
    {
        public nint data;
        public node_embedding_run_task_callback invoke;
        public node_embedding_release_data_callback release;
    }

    public struct node_embedding_post_task_functor
    {
        public nint data;
        public node_embedding_post_task_callback invoke;
        public node_embedding_release_data_callback release;
    }

    public struct node_embedding_run_node_api_functor_ref : IDisposable
    {
        public nint data;
        public node_embedding_run_node_api_callback invoke;

        public node_embedding_run_node_api_functor_ref(
            object? functor, node_embedding_run_node_api_callback invoke)
        {
            data = (functor != null) ? (nint)GCHandle.Alloc(functor) : default;
            this.invoke = (functor != null)
                ? invoke
                : new node_embedding_run_node_api_callback(0);
        }

        public void Dispose()
        {
            if (data == 0) return;
            GCHandle.FromIntPtr(data).Free();
            data = 0;
        }
    }
}
