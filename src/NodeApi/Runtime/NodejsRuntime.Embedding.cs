// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.Runtime;

// Imports embedding APIs from libnode.
public unsafe partial class NodejsRuntime
{
#pragma warning disable IDE1006 // Naming: missing prefix '_'

    #region Enums

    public enum node_embedding_platform_flags
    {
        no_flags = 0,
        // Enable stdio inheritance, which is disabled by default.
        // This flag is also implied by
        // no_stdio_initialization.
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

    public enum node_embedding_runtime_flags
    {
        no_flags = 0,
        // Use the default behavior for Node.js instances.
        default_flags = 1 << 0,
        // Controls whether this Environment is allowed to affect per-process state
        // (e.g. cwd, process title, uid, etc.).
        // This is set when using default_flags.
        owns_process_state = 1 << 1,
        // Set if this Environment instance is associated with the global inspector
        // handling code (i.e. listening on SIGUSR1).
        // This is set when using default_flags.
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

    public enum node_embedding_event_loop_run_mode
    {
        // Run the event loop until it is completed.
        // It matches the UV_RUN_DEFAULT behavior.
        run_default = 0,
        // Run the event loop once and wait if there are no items.
        // It matches the UV_RUN_ONCE behavior.
        run_once = 1,
        // Run the event loop once and do not wait if there are no items.
        // It matches the UV_RUN_NOWAIT behavior.
        run_nowait = 2,
    }

    #endregion

    #region Callbacks

    private struct node_embedding_error_handler
    {
        public nint Handle;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint handler_data,
            nint messages,
            nuint messages_size,
            node_embedding_status exit_code);

        public node_embedding_error_handler(node_embedding_error_handler.Delegate handler)
            => Handle = Marshal.GetFunctionPointerForDelegate(handler);
    }

    private struct node_embedding_configure_platform_callback
    {
        public nint Handle;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_platform platform);

        public node_embedding_configure_platform_callback(
            node_embedding_configure_platform_callback.Delegate callback)
            => Handle = Marshal.GetFunctionPointerForDelegate(callback);
    }

    private struct node_embedding_configure_runtime_callback
    {
        public nint Handle;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_platform platform,
            node_embedding_runtime runtime);

        public node_embedding_configure_runtime_callback(
            node_embedding_configure_runtime_callback.Delegate callback)
            => Handle = Marshal.GetFunctionPointerForDelegate(callback);
    }

    private struct node_embedding_node_api_callback
    {
        public nint Handle;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_runtime runtime,
            napi_env env);

        public node_embedding_node_api_callback(
            node_embedding_node_api_callback.Delegate callback)
            => Handle = Marshal.GetFunctionPointerForDelegate(callback);
    }

    private struct node_embedding_get_args_callback
    {
        public nint Handle;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            nuint argc,
            nint argv);

        public node_embedding_get_args_callback(
            node_embedding_get_args_callback.Delegate callback)
            => Handle = Marshal.GetFunctionPointerForDelegate(callback);
    }
    
    private struct node_embedding_preload_callback
    {
        public nint Handle;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_runtime runtime,
            napi_env env,
            napi_value process,
            napi_value require);

        public node_embedding_preload_callback(
            node_embedding_preload_callback.Delegate callback)
            => Handle = Marshal.GetFunctionPointerForDelegate(callback);
    }

    private struct node_embedding_start_execution_callback
    {
        public nint Handle;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_runtime runtime,
            napi_env env,
            napi_value process,
            napi_value require,
            napi_value run_cjs);

        public node_embedding_start_execution_callback(
            node_embedding_start_execution_callback.Delegate callback)
            => Handle = Marshal.GetFunctionPointerForDelegate(callback);
    }

    private struct node_embedding_initialize_module_callback
    {
        public nint Handle;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_runtime runtime,
            napi_env env,
            nint module_name,
            napi_value exports);

        public node_embedding_initialize_module_callback(
            node_embedding_initialize_module_callback.Delegate callback)
            => Handle = Marshal.GetFunctionPointerForDelegate(callback);
    }

    private struct node_embedding_event_loop_handler
    {
        public nint Handle;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            node_embedding_runtime runtime);

        public node_embedding_event_loop_handler(
            node_embedding_event_loop_handler.Delegate callback)
            => Handle = Marshal.GetFunctionPointerForDelegate(callback);
    }

    #endregion

    private delegate* unmanaged[Cdecl]<
        node_embedding_error_handler, nint, node_embedding_status>
        node_embedding_on_error;

    public override node_embedding_status OnEmbeddingError(
        Action<string[], node_embedding_status> errorHandler)
    {
        node_embedding_error_handler nativeErrorHandler =
            new ((
                nint handler_data,
                nint messages,
                nuint messages_size,
                node_embedding_status exit_code) =>
            {
                string[] args = new string[messages_size];
                for (nuint i = 0; i < messages_size; i++)
                {
                    args[i] = PtrToStringUTF8(
                        (byte*)Marshal.ReadIntPtr(messages, (int)i * (int)UIntPtr.Size))
                        ?? string.Empty;
                }

                errorHandler(args, exit_code);
            });

        return Import(ref node_embedding_on_error)(nativeErrorHandler, default);
    }

    private delegate* unmanaged[Cdecl]<
        int, int, node_embedding_status>
        node_embedding_set_api_version;

    public override node_embedding_status SetEmbeddingApiVersion(
        int versionMajor,
        int versionMinor)
    {
        return Import(ref node_embedding_set_api_version)(versionMajor, versionMinor);
    }

    private delegate* unmanaged[Cdecl]<
        int,
        nint,
        node_embedding_configure_platform_callback,
        nint,
        node_embedding_configure_runtime_callback,
        nint,
        node_embedding_node_api_callback,
        nint,
        node_embedding_status>
        node_embedding_run_main;

    public override node_embedding_status RunEmbeddingMain(
        string[] args,
        Action<node_embedding_platform>? configurePlatform,
        Action<node_embedding_platform, node_embedding_runtime>? configureRuntime,
        Action<node_embedding_runtime, napi_env> nodeApiCallback)
    {
        node_embedding_configure_platform_callback configurePlatformCallback =
            configurePlatform == null ? default :
            new ((nint cb_data, node_embedding_platform platform) =>
            {
                configurePlatform!.Invoke(platform);
            });
        node_embedding_configure_runtime_callback configureRuntimeCallback =
            configureRuntime == null ? default :
            new ((nint cb_data, node_embedding_platform platform, node_embedding_runtime runtime) =>
            {
                configureRuntime!.Invoke(platform, runtime);
            });
        node_embedding_node_api_callback nativeNodeApiCallback =
            new ((nint cb_data, node_embedding_runtime runtime, napi_env env) =>
            {
                nodeApiCallback(runtime, env);
            });

        nint args_ptr = StringsToHGlobalUtf8(args, out int args_count);

        try
        {
            return Import(ref node_embedding_run_main)(
                args_count,
                args_ptr,
                configurePlatformCallback,
                default,
                configureRuntimeCallback,
                default,
                nativeNodeApiCallback,
                default);
        }
        finally
        {
            FreeStringsHGlobal(args_ptr, args_count);
        }
    }

    private delegate* unmanaged[Cdecl]<
        int,
        nint,
        node_embedding_configure_platform_callback,
        nint,
        nint,
        node_embedding_status>
        node_embedding_create_platform;

    public override node_embedding_status CreateEmbeddingPlatform(
        string[] args,
        Action<node_embedding_platform>? configurePlatform,
        out node_embedding_platform result)
    {
        node_embedding_configure_platform_callback configurePlatformCallback =
            configurePlatform == null ? default :
            new ((nint cb_data, node_embedding_platform platform) =>
            {
                configurePlatform!.Invoke(platform);
            });

        nint args_ptr = StringsToHGlobalUtf8(args, out int args_count);

        fixed (node_embedding_platform* result_ptr = &result)
        try
        {
            return Import(ref node_embedding_create_platform)(
                args_count,
                args_ptr,
                configurePlatformCallback,
                default,
                (nint)result_ptr);
        }
        finally
        {
            FreeStringsHGlobal(args_ptr, args_count);
        }
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_status>
        node_embedding_delete_platform;

    public override node_embedding_status DeleteEmbeddingPlatform(
        node_embedding_platform platform)
    {
        return Import(ref node_embedding_delete_platform)(platform);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_platform_flags,
        node_embedding_status>
        node_embedding_platform_set_flags;

    public override node_embedding_status SetEmbeddingPlatformFlags(
        node_embedding_platform platform,
        node_embedding_platform_flags flags)
    {
        return Import(ref node_embedding_platform_set_flags)(platform, flags);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_get_args_callback,
        nint,
        node_embedding_get_args_callback,
        nint,
        node_embedding_status>
        node_embedding_platform_get_parsed_args;

    public override node_embedding_status GetEmbeddingPlatformParsedArgs(
        node_embedding_platform platform,
        Action<string[]>? getArgsCallback,
        Action<string[]>? getExecArgsCallback)
    {
        node_embedding_get_args_callback nativeGetArgsCallback =
            getArgsCallback == null ? default :
            new ((nint cb_data, nuint argc, nint argv) =>
            {
                string[] args = new string[argc];
                for (nuint i = 0; i < argc; i++)
                {
                    args[i] = PtrToStringUTF8(
                        (byte*)Marshal.ReadIntPtr(argv, (int)i * UIntPtr.Size))
                        ?? string.Empty;
                }

                getArgsCallback(args);
            });
        node_embedding_get_args_callback nativeGetExecArgsCallback =
            getExecArgsCallback == null ? default :
            new ((nint cb_data, nuint argc, nint argv) =>
            {
                string[] args = new string[argc];
                for (nuint i = 0; i < argc; i++)
                {
                    args[i] = PtrToStringUTF8(
                        (byte*)Marshal.ReadIntPtr(argv, (int)i * UIntPtr.Size))
                        ?? string.Empty;
                }

                getExecArgsCallback(args);
            });

        return Import(ref node_embedding_platform_get_parsed_args)(
            platform,
            nativeGetArgsCallback,
            default,
            nativeGetExecArgsCallback,
            default);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_configure_runtime_callback,
        nint,
        node_embedding_node_api_callback,
        nint,
        node_embedding_status>
        node_embedding_run_runtime;

    public override node_embedding_status RunEmbeddingRuntime(
        node_embedding_platform platform,
        string[] args,
        Action<node_embedding_platform, node_embedding_runtime>? configureRuntime,
        Action<node_embedding_runtime, napi_env> nodeApiCallback)
    {
        node_embedding_configure_runtime_callback configureRuntimeCallback =
            configureRuntime == null ? default :
            new ((nint cb_data, node_embedding_platform platform, node_embedding_runtime runtime) =>
            {
                configureRuntime!.Invoke(platform, runtime);
            });
        node_embedding_node_api_callback nativeNodeApiCallback =
            new ((nint cb_data, node_embedding_runtime runtime, napi_env env) =>
            {
                nodeApiCallback(runtime, env);
            });

        nint args_ptr = StringsToHGlobalUtf8(args, out int args_count);

        try
        {
            return Import(ref node_embedding_run_runtime)(
                platform,
                configureRuntimeCallback,
                args_ptr,
                nativeNodeApiCallback,
                default);
        }
        finally
        {
            FreeStringsHGlobal(args_ptr, args_count);
        }
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_configure_runtime_callback,
        nint,
        nint,
        node_embedding_status>
        node_embedding_create_runtime;

    public override node_embedding_status CreateEmbeddingRuntime(
        node_embedding_platform platform,
        Action<node_embedding_platform, node_embedding_runtime>? configureRuntime,
        out node_embedding_runtime result)
    {
        node_embedding_configure_runtime_callback configureRuntimeCallback =
            configureRuntime == null ? default :
            new ((nint cb_data, node_embedding_platform platform, node_embedding_runtime runtime) =>
            {
                configureRuntime!.Invoke(platform, runtime);
            });

        fixed (node_embedding_runtime* result_ptr = &result)
        {
            return Import(ref node_embedding_create_runtime)(
                platform,
                configureRuntimeCallback,
                default,
                (nint)result_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_status>
        node_embedding_delete_runtime;

    public override node_embedding_status DeleteEmbeddingRuntime(node_embedding_runtime runtime)
    {
        return Import(ref node_embedding_delete_runtime)(runtime);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_runtime_flags,
        node_embedding_status>
        node_embedding_runtime_set_flags;

    public override node_embedding_status SetEmbeddingRuntimeFlags(
        node_embedding_runtime runtime,
        node_embedding_runtime_flags flags)
    {
        return Import(ref node_embedding_runtime_set_flags)(runtime, flags);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        int,
        nint,
        int,
        nint,
        node_embedding_status>
        node_embedding_runtime_set_args;

    public override node_embedding_status SetEmbeddingRuntimeArgs(
        node_embedding_runtime runtime,
        string[] args,
        string[] execArgs)
    {
        nint args_ptr = StringsToHGlobalUtf8(args, out int args_count);
        nint execArgs_ptr = StringsToHGlobalUtf8(execArgs, out int execArgs_count);

        try
        {
            return Import(ref node_embedding_runtime_set_args)(
                runtime,
                args_count,
                args_ptr,
                execArgs_count,
                execArgs_ptr);
        }
        finally
        {
            FreeStringsHGlobal(args_ptr, args_count);
            FreeStringsHGlobal(execArgs_ptr, execArgs_count);
        }
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_preload_callback,
        nint,
        node_embedding_status>
        node_embedding_runtime_on_preload;

    public override node_embedding_status OnEmbeddingRuntimePreload(
        node_embedding_runtime runtime,
        Action<node_embedding_runtime, napi_env, napi_value, napi_value> preloadCallback)
    {
        node_embedding_preload_callback nativePreloadCallback =
            new ((
                nint cb_data,
                node_embedding_runtime runtime,
                napi_env env,
                napi_value process,
                napi_value require) =>
            {
                preloadCallback(runtime, env, process, require);
            });

        return Import(ref node_embedding_runtime_on_preload)(
            runtime, nativePreloadCallback, default);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_start_execution_callback,
        nint,
        node_embedding_status>
        node_embedding_runtime_on_start_execution;

    public override node_embedding_status OnEmbeddingRuntimeStartExecution(
        node_embedding_runtime runtime,
        Action<node_embedding_runtime, napi_env, napi_value, napi_value, napi_value>
            startExecutionCallback)
    {
        node_embedding_start_execution_callback nativeStartExecutionCallback =
            new ((
                nint cb_data,
                node_embedding_runtime runtime,
                napi_env env,
                napi_value process,
                napi_value require,
                napi_value run_cjs) =>
            {
                startExecutionCallback(runtime, env, process, require, run_cjs);
            });

        return Import(ref node_embedding_runtime_on_start_execution)(
            runtime, nativeStartExecutionCallback, default);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        nint,
        node_embedding_initialize_module_callback,
        nint,
        int,
        node_embedding_status>
        node_embedding_runtime_add_module;

    public override node_embedding_status AddEmbeddingRuntimeModule(
        node_embedding_runtime runtime,
        string moduleName,
        Action<node_embedding_runtime, napi_env, string, napi_value> initializeModuleCallback,
        int module_node_api_version)
    {
        node_embedding_initialize_module_callback nativeInitializeModuleCallback =
            new ((
                nint cb_data,
                node_embedding_runtime runtime,
                napi_env env,
                nint module_name,
                napi_value exports) =>
            {
                string moduleName = PtrToStringUTF8((byte*)module_name) ?? string.Empty;

                initializeModuleCallback(runtime, env, moduleName, exports);
            });

        nint moduleName_ptr = StringToHGlobalUtf8(moduleName);

        try
        {
            return Import(ref node_embedding_runtime_add_module)(
                runtime,
                moduleName_ptr,
                nativeInitializeModuleCallback,
                default,
                module_node_api_version);
        }
        finally
        {
            Marshal.FreeHGlobal(moduleName_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_event_loop_handler,
        nint,
        node_embedding_status>
        node_embedding_on_wake_up_event_loop;

    public override node_embedding_status OnEmbeddingWakeUpEventLoop(
        node_embedding_runtime runtime,
        Action<node_embedding_runtime> eventLoopHandler)
    {
        node_embedding_event_loop_handler nativeEventLoopHandler =
            new ((nint cb_data, node_embedding_runtime runtime) =>
            {
                eventLoopHandler(runtime);
            });

        return Import(ref node_embedding_on_wake_up_event_loop)(
            runtime, nativeEventLoopHandler, default);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_event_loop_run_mode,
        nint,
        node_embedding_status>
        node_embedding_run_event_loop;

    public override node_embedding_status RunEmbeddingEventLoop(
        node_embedding_runtime runtime,
        node_embedding_event_loop_run_mode runMode,
        out bool hasMoreWork)
    {
        fixed (bool* hasMoreWork_ptr = &hasMoreWork)
        {
            return Import(ref node_embedding_run_event_loop)(
                runtime, runMode, (nint)hasMoreWork_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_status>
        node_embedding_complete_event_loop;

    public override node_embedding_status CompleteEmbeddingEventLoop(
        node_embedding_runtime runtime)
    {
        return Import(ref node_embedding_complete_event_loop)(runtime);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_node_api_callback,
        nint,
        node_embedding_status>
        node_embedding_run_node_api;

    public override node_embedding_status RunEmbeddingNodeApi(
        node_embedding_runtime runtime,
        Action<node_embedding_runtime, napi_env> nodeApiCallback)
    {
        node_embedding_node_api_callback nativeNodeApiCallback =
            new ((nint cb_data, node_embedding_runtime runtime, napi_env env) =>
            {
                nodeApiCallback(runtime, env);
            });

        return Import(ref node_embedding_run_node_api)(
            runtime, nativeNodeApiCallback, default);
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        nint,
        node_embedding_status>
        node_embedding_open_node_api_scope;

    public override node_embedding_status OpenEmbeddingNodeApiScope(
        node_embedding_runtime runtime,
        out napi_env env)
    {
        fixed (napi_env* env_ptr = &env)
        {
            return Import(ref node_embedding_open_node_api_scope)(runtime, (nint)env_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_status>
        node_embedding_close_node_api_scope;

    public override node_embedding_status CloseEmbeddingNodeApiScope(
        node_embedding_runtime runtime)
    {
        return Import(ref node_embedding_close_node_api_scope)(runtime);
    }

#pragma warning restore IDE1006
}
