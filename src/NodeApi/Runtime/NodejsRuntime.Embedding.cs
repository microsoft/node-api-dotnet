// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using System.Runtime.InteropServices;

// Imports embedding APIs from libnode.
public unsafe partial class NodejsRuntime
{
#pragma warning disable IDE1006 // Naming: missing prefix '_'

    //==============================================================================================
    // Data types
    //==============================================================================================

    public record struct node_embedding_platform(nint Handle);
    public record struct node_embedding_runtime(nint Handle);
    public record struct node_embedding_platform_config(nint Handle);
    public record struct node_embedding_runtime_config(nint Handle);
    public record struct node_embedding_node_api_scope(nint Handle);

    public enum NodeEmbeddingStatus : int
    {
        OK = 0,
        GenericError = 1,
        NullArg = 2,
        BadArg = 3,
        OutOfMemory = 4,
        // This value is added to the exit code in cases when Node.js API returns
        // an error exit code.
        ErrorExitCode = 512,
    }

    [Flags]
    public enum NodeEmbeddingPlatformFlags : int
    {
        None = 0,
        // Enable stdio inheritance, which is disabled by default.
        // This flag is also implied by
        // node_embedding_platform_flags_no_stdio_initialization.
        EnableStdioInheritance = 1 << 0,
        // Disable reading the NODE_OPTIONS environment variable.
        DisableNodeOptionsEnv = 1 << 1,
        // Do not parse CLI options.
        DisableCliOptions = 1 << 2,
        // Do not initialize ICU.
        NoIcu = 1 << 3,
        // Do not modify stdio file descriptor or TTY state.
        NoStdioInitialization = 1 << 4,
        // Do not register Node.js-specific signal handlers
        // and reset other signal handlers to default state.
        NoDefaultSignalHandling = 1 << 5,
        // Do not initialize OpenSSL config.
        NoInitOpenSsl = 1 << 8,
        // Do not initialize Node.js debugging based on environment variables.
        NoParseGlobalDebugVariables = 1 << 9,
        // Do not adjust OS resource limits for this process.
        NoAdjustResourceLimits = 1 << 10,
        // Do not map code segments into large pages for this process.
        NoUseLargePages = 1 << 11,
        // Skip printing output for --help, --version, --v8-options.
        NoPrintHelpOrVersionOutput = 1 << 12,
        // Initialize the process for predictable snapshot generation.
        GeneratePredictableSnapshot = 1 << 14,
    }

    // The flags for the Node.js runtime initialization.
    // They match the internal EnvironmentFlags::Flags enum.
    [Flags]
    public enum NodeEmbeddingRuntimeFlags : int
    {
        None = 0,
        // Use the default behavior for Node.js instances.
        DefaultFlags = 1 << 0,
        // Controls whether this Environment is allowed to affect per-process state
        // (e.g. cwd, process title, uid, etc.).
        // This is set when using default.
        OwnsProcessState = 1 << 1,
        // Set if this Environment instance is associated with the global inspector
        // handling code (i.e. listening on SIGUSR1).
        // This is set when using default.
        OwnsInspector = 1 << 2,
        // Set if Node.js should not run its own esm loader. This is needed by some
        // embedders, because it's possible for the Node.js esm loader to conflict
        // with another one in an embedder environment, e.g. Blink's in Chromium.
        NoRegisterEsmLoader = 1 << 3,
        // Set this flag to make Node.js track "raw" file descriptors, i.e. managed
        // by fs.open() and fs.close(), and close them during
        // node_embedding_delete_runtime().
        TrackUmanagedFDs = 1 << 4,
        // Set this flag to force hiding console windows when spawning child
        // processes. This is usually used when embedding Node.js in GUI programs on
        // Windows.
        HideConsoleWindows = 1 << 5,
        // Set this flag to disable loading native addons via `process.dlopen`.
        // This environment flag is especially important for worker threads
        // so that a worker thread can't load a native addon even if `execArgv`
        // is overwritten and `--no-addons` is not specified but was specified
        // for this Environment instance.
        NoNativeAddons = 1 << 6,
        // Set this flag to disable searching modules from global paths like
        // $HOME/.node_modules and $NODE_PATH. This is used by standalone apps that
        // do not expect to have their behaviors changed because of globally
        // installed modules.
        NoGlobalSearchPaths = 1 << 7,
        // Do not export browser globals like setTimeout, console, etc.
        NoBrowserGlobals = 1 << 8,
        // Controls whether or not the Environment should call V8Inspector::create().
        // This control is needed by embedders who may not want to initialize the V8
        // inspector in situations where one has already been created,
        // e.g. Blink's in Chromium.
        NoCreateInspector = 1 << 9,
        // Controls whether or not the InspectorAgent for this Environment should
        // call StartDebugSignalHandler. This control is needed by embedders who may
        // not want to allow other processes to start the V8 inspector.
        NoStartDebugSignalHandler = 1 << 10,
        // Controls whether the InspectorAgent created for this Environment waits for
        // Inspector frontend events during the Environment creation. It's used to
        // call node::Stop(env) on a Worker thread that is waiting for the events.
        NoWaitForInspectorFrontend = 1 << 11
    }

    //==============================================================================================
    // Callbacks
    //==============================================================================================

    public record struct node_embedding_data_release_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_data_release_callback(
            delegate* unmanaged[Cdecl]<nint, NodeEmbeddingStatus> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NodeEmbeddingStatus Delegate(nint data);

        public node_embedding_data_release_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_platform_configure_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_platform_configure_callback(delegate* unmanaged[Cdecl]<
            nint, node_embedding_platform_config, NodeEmbeddingStatus> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NodeEmbeddingStatus Delegate(
            nint cb_data,
            node_embedding_platform_config platform_config);

        public node_embedding_platform_configure_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_runtime_configure_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_runtime_configure_callback(delegate* unmanaged[Cdecl]<
            nint,
            node_embedding_platform,
            node_embedding_runtime_config,
            NodeEmbeddingStatus> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NodeEmbeddingStatus Delegate(
            nint cb_data,
            node_embedding_platform platform,
            node_embedding_runtime_config runtime_config);

        public node_embedding_runtime_configure_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_runtime_preload_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_runtime_preload_callback(delegate* unmanaged[Cdecl]<
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

        public node_embedding_runtime_preload_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_runtime_loading_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_runtime_loading_callback(delegate* unmanaged[Cdecl]<
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

        public node_embedding_runtime_loading_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_runtime_loaded_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_runtime_loaded_callback(delegate* unmanaged[Cdecl]<
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
            napi_value load_result);

        public node_embedding_runtime_loaded_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_module_initialize_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_module_initialize_callback(delegate* unmanaged[Cdecl]<
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

        public node_embedding_module_initialize_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_task_run_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_task_run_callback(
            delegate* unmanaged[Cdecl]<nint, NodeEmbeddingStatus> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NodeEmbeddingStatus Delegate(nint cb_data);

        public node_embedding_task_run_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_task_post_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_task_post_callback(delegate* unmanaged[Cdecl]<
            nint,
            node_embedding_task_run_callback,
            nint,
            node_embedding_data_release_callback,
            nint,
            NodeEmbeddingStatus> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate NodeEmbeddingStatus Delegate(
            nint cb_data,
            node_embedding_task_run_callback run_task,
            nint task_data,
            node_embedding_data_release_callback release_task_data,
            nint is_posted);

        public node_embedding_task_post_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct node_embedding_node_api_run_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public node_embedding_node_api_run_callback(delegate* unmanaged[Cdecl]<
            nint,
            napi_env,
            void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(
            nint cb_data,
            napi_env env);

        public node_embedding_node_api_run_callback(Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    //==============================================================================================
    // Functions
    //==============================================================================================

    //----------------------------------------------------------------------------------------------
    // Error handling functions.
    //----------------------------------------------------------------------------------------------

    private delegate* unmanaged[Cdecl]<nint> node_embedding_last_error_message_get;

    private delegate* unmanaged[Cdecl]<nint, void> node_embedding_last_error_message_set;

    //----------------------------------------------------------------------------------------------
    // Node.js global platform functions.
    //----------------------------------------------------------------------------------------------

    private delegate* unmanaged[Cdecl]<
        int,
        int,
        nint,
        node_embedding_platform_configure_callback,
        nint,
        node_embedding_runtime_configure_callback,
        nint,
        NodeEmbeddingStatus> node_embedding_main_run;

    private delegate* unmanaged[Cdecl]<
        int,
        int,
        nint,
        node_embedding_platform_configure_callback,
        nint,
        nint,
        NodeEmbeddingStatus> node_embedding_platform_create;

    private delegate* unmanaged[Cdecl]<node_embedding_platform, NodeEmbeddingStatus>
        node_embedding_platform_delete;

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform_config,
        NodeEmbeddingPlatformFlags,
        NodeEmbeddingStatus> node_embedding_platform_config_set_flags;

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        nint,
        nint,
        nint,
        nint,
        NodeEmbeddingStatus> node_embedding_platform_get_parsed_args;

    //----------------------------------------------------------------------------------------------
    // Node.js runtime functions.
    //----------------------------------------------------------------------------------------------

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_runtime_configure_callback,
        nint,
        NodeEmbeddingStatus> node_embedding_runtime_run;

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_runtime_configure_callback,
        nint,
        nint,
        NodeEmbeddingStatus> node_embedding_runtime_create;

    private delegate* unmanaged[Cdecl]<node_embedding_runtime, NodeEmbeddingStatus>
        node_embedding_runtime_delete;

    private delegate* unmanaged[Cdecl]<node_embedding_runtime_config, int, NodeEmbeddingStatus>
        node_embedding_runtime_config_set_node_api_version;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        NodeEmbeddingRuntimeFlags,
        NodeEmbeddingStatus> node_embedding_runtime_config_set_flags;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        int,
        nint,
        int,
        nint,
        NodeEmbeddingStatus> node_embedding_runtime_config_set_args;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        node_embedding_runtime_preload_callback,
        nint,
        node_embedding_data_release_callback,
        NodeEmbeddingStatus> node_embedding_runtime_config_on_preload;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        node_embedding_runtime_loading_callback,
        nint,
        node_embedding_data_release_callback,
        NodeEmbeddingStatus> node_embedding_runtime_config_on_loading;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        node_embedding_runtime_loaded_callback,
        nint,
        node_embedding_data_release_callback,
        NodeEmbeddingStatus> node_embedding_runtime_config_on_loaded;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        nint,
        node_embedding_module_initialize_callback,
        nint,
        node_embedding_data_release_callback,
        int,
        NodeEmbeddingStatus> node_embedding_runtime_config_add_module;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        nint,
        node_embedding_data_release_callback,
        NodeEmbeddingStatus> node_embedding_runtime_user_data_set;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        nint,
        NodeEmbeddingStatus> node_embedding_runtime_user_data_get;

    //----------------------------------------------------------------------------------------------
    // Node.js runtime functions for the event loop.
    //----------------------------------------------------------------------------------------------

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        node_embedding_task_post_callback,
        nint,
        node_embedding_data_release_callback,
        NodeEmbeddingStatus> node_embedding_runtime_config_set_task_runner;

    private delegate* unmanaged[Cdecl]<node_embedding_runtime, NodeEmbeddingStatus>
        node_embedding_runtime_event_loop_run;

    private delegate* unmanaged[Cdecl]<node_embedding_runtime, NodeEmbeddingStatus>
        node_embedding_runtime_event_loop_terminate;

    private delegate* unmanaged[Cdecl]<node_embedding_runtime, nint, NodeEmbeddingStatus>
        node_embedding_runtime_event_loop_run_once;

    private delegate* unmanaged[Cdecl]<node_embedding_runtime, nint, NodeEmbeddingStatus>
        node_embedding_runtime_event_loop_run_no_wait;

    //----------------------------------------------------------------------------------------------
    // Node.js runtime functions for the Node-API interop.
    //----------------------------------------------------------------------------------------------

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_node_api_run_callback,
        nint,
        NodeEmbeddingStatus> node_embedding_runtime_node_api_run;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        nint,
        nint,
        NodeEmbeddingStatus> node_embedding_runtime_node_api_scope_open;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_node_api_scope,
        NodeEmbeddingStatus> node_embedding_runtime_node_api_scope_close;

    //==============================================================================================
    // C# function wrappers
    //==============================================================================================

    public override string EmbeddingGetLastErrorMessage()
    {
        nint messagePtr = Import(ref node_embedding_last_error_message_get)();
        return PtrToStringUTF8((byte*)messagePtr) ?? string.Empty;
    }

    public override void EmbeddingSetLastErrorMessage(ReadOnlySpan<char> message)
    {
        using PooledBuffer messageBuffer = PooledBuffer.FromSpanUtf8(message);
        fixed (byte* messagePtr = messageBuffer)
            Import(ref node_embedding_last_error_message_set)((nint)messagePtr);
    }

    public override NodeEmbeddingStatus EmbeddingRunMain(
        ReadOnlySpan<string> args,
        node_embedding_platform_configure_callback configure_platform,
        nint configure_platform_data,
        node_embedding_runtime_configure_callback configure_runtime,
        nint configure_runtime_data)
    {
        using Utf8StringArray utf8Args = new(args);
        fixed (nint* argsPtr = utf8Args)
            return Import(ref node_embedding_main_run)(
                NodeEmbedding.EmbeddingApiVersion,
                args.Length,
                (nint)argsPtr,
                configure_platform,
                configure_platform_data,
                configure_runtime,
                configure_runtime_data);
    }

    public override NodeEmbeddingStatus EmbeddingCreatePlatform(
        ReadOnlySpan<string> args,
        node_embedding_platform_configure_callback configure_platform,
        nint configure_platform_data,
        out node_embedding_platform result)
    {
        using Utf8StringArray utf8Args = new(args);
        fixed (nint* argsPtr = utf8Args)
        fixed (node_embedding_platform* result_ptr = &result)
        {
            return Import(ref node_embedding_platform_create)(
                NodeEmbedding.EmbeddingApiVersion,
                args.Length,
                (nint)argsPtr,
                configure_platform,
                configure_platform_data,
                (nint)result_ptr);
        }
    }

    public override NodeEmbeddingStatus EmbeddingDeletePlatform(node_embedding_platform platform)
    {
        return Import(ref node_embedding_platform_delete)(platform);
    }

    public override NodeEmbeddingStatus EmbeddingPlatformConfigSetFlags(
        node_embedding_platform_config platform_config, NodeEmbeddingPlatformFlags flags)
    {
        return Import(ref node_embedding_platform_config_set_flags)(platform_config, flags);
    }

    public override NodeEmbeddingStatus EmbeddingPlatformGetParsedArgs(
        node_embedding_platform platform,
        nint args_count,
        nint args,
        nint runtime_args_count,
        nint runtime_args)
    {
        return Import(ref node_embedding_platform_get_parsed_args)(
            platform, args_count, args, runtime_args_count, runtime_args);
    }

    public override NodeEmbeddingStatus EmbeddingRunRuntime(
        node_embedding_platform platform,
        node_embedding_runtime_configure_callback configure_runtime,
        nint configure_runtime_data)
    {
        return Import(ref node_embedding_runtime_run)(
            platform, configure_runtime, configure_runtime_data);
    }

    public override NodeEmbeddingStatus EmbeddingCreateRuntime(
        node_embedding_platform platform,
        node_embedding_runtime_configure_callback configure_runtime,
        nint configure_runtime_data,
        out node_embedding_runtime result)
    {
        fixed (node_embedding_runtime* result_ptr = &result)
        {
            return Import(ref node_embedding_runtime_create)(
                platform, configure_runtime, configure_runtime_data, (nint)result_ptr);
        }
    }

    public override NodeEmbeddingStatus
        EmbeddingDeleteRuntime(node_embedding_runtime runtime)
    {
        return Import(ref node_embedding_runtime_delete)(runtime);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeConfigSetNodeApiVersion(
        node_embedding_runtime_config runtime_config, int node_api_version)
    {
        return Import(ref node_embedding_runtime_config_set_node_api_version)(
            runtime_config, node_api_version);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeConfigSetFlags(
        node_embedding_runtime_config runtime_config, NodeEmbeddingRuntimeFlags flags)
    {
        return Import(ref node_embedding_runtime_config_set_flags)(runtime_config, flags);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeConfigSetArgs(
        node_embedding_runtime_config runtime_config,
        ReadOnlySpan<string> args,
        ReadOnlySpan<string> runtime_args)
    {
        using Utf8StringArray utf8Args = new(args);
        using Utf8StringArray utf8RuntimeArgs = new(runtime_args);
        fixed (nint* argsPtr = utf8Args)
        fixed (nint* runtimeArgsPtr = utf8RuntimeArgs)
        {
            return Import(ref node_embedding_runtime_config_set_args)(
                runtime_config,
                args.Length,
                (nint)argsPtr,
                runtime_args.Length,
                (nint)runtimeArgsPtr);
        }
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeConfigOnPreload(
        node_embedding_runtime_config runtime_config,
        node_embedding_runtime_preload_callback preload,
        nint preload_data,
        node_embedding_data_release_callback release_preload_data)
    {
        return Import(ref node_embedding_runtime_config_on_preload)(
            runtime_config, preload, preload_data, release_preload_data);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeConfigOnLoading(
        node_embedding_runtime_config runtime_config,
        node_embedding_runtime_loading_callback run_load,
        nint load_data,
        node_embedding_data_release_callback release_load_data)
    {
        return Import(ref node_embedding_runtime_config_on_loading)(
            runtime_config, run_load, load_data, release_load_data);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeConfigOnLoaded(
        node_embedding_runtime_config runtime_config,
        node_embedding_runtime_loaded_callback handle_loaded,
        nint handle_loaded_data,
        node_embedding_data_release_callback release_handle_loaded_data)
    {
        return Import(ref node_embedding_runtime_config_on_loaded)(
            runtime_config, handle_loaded, handle_loaded_data, release_handle_loaded_data);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeConfigAddModule(
        node_embedding_runtime_config runtime_config,
        ReadOnlySpan<char> module_name,
        node_embedding_module_initialize_callback init_module,
        nint init_module_data,
        node_embedding_data_release_callback release_init_module_data,
        int module_node_api_version)
    {
        PooledBuffer moduleNameBuffer = PooledBuffer.FromSpanUtf8(module_name);
        fixed (byte* moduleNamePtr = moduleNameBuffer)
            return Import(ref node_embedding_runtime_config_add_module)(
                runtime_config,
                (nint)moduleNamePtr,
                init_module,
                init_module_data,
                release_init_module_data,
                module_node_api_version);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeSetUserData(
        node_embedding_runtime runtime,
        nint user_data,
        node_embedding_data_release_callback release_user_data)
    {
        return Import(ref node_embedding_runtime_user_data_set)(
            runtime, user_data, release_user_data);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeGetUserData(
        node_embedding_runtime runtime, out nint user_data)
    {
        fixed (nint* userDataPtr = &user_data)
            return Import(ref node_embedding_runtime_user_data_get)(runtime, (nint)userDataPtr);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeConfigSetTaskRunner(
        node_embedding_runtime_config runtime_config,
        node_embedding_task_post_callback post_task,
        nint post_task_data,
        node_embedding_data_release_callback release_post_task_data)
    {
        return Import(ref node_embedding_runtime_config_set_task_runner)(
            runtime_config, post_task, post_task_data, release_post_task_data);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeRunEventLoop(node_embedding_runtime runtime)
    {
        return Import(ref node_embedding_runtime_event_loop_run)(runtime);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeTerminateEventLoop(
        node_embedding_runtime runtime)
    {
        return Import(ref node_embedding_runtime_event_loop_terminate)(runtime);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeRunOnceEventLoop(
        node_embedding_runtime runtime, out bool hasMoreWork)
    {
        fixed (bool* hasMoreWorkPtr = &hasMoreWork)
            return Import(ref node_embedding_runtime_event_loop_run_once)(
                runtime, (nint)hasMoreWorkPtr);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeRunNoWaitEventLoop(
        node_embedding_runtime runtime, out bool hasMoreWork)
    {
        fixed (bool* hasMoreWorkPtr = &hasMoreWork)
            return Import(ref node_embedding_runtime_event_loop_run_no_wait)(
                runtime, (nint)hasMoreWorkPtr);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeRunNodeApi(
        node_embedding_runtime runtime,
        node_embedding_node_api_run_callback run_node_api,
        nint run_node_api_data)
    {
        return Import(ref node_embedding_runtime_node_api_run)(
            runtime, run_node_api, run_node_api_data);
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeOpenNodeApiScope(
        node_embedding_runtime runtime,
        out node_embedding_node_api_scope node_api_scope,
        out napi_env env)
    {
        fixed (node_embedding_node_api_scope* scopePtr = &node_api_scope)
        fixed (napi_env* envPtr = &env)
        {
            return Import(ref node_embedding_runtime_node_api_scope_open)(
                runtime, (nint)scopePtr, (nint)envPtr);
        }
    }

    public override NodeEmbeddingStatus EmbeddingRuntimeCloseNodeApiScope(
        node_embedding_runtime runtime,
        node_embedding_node_api_scope node_api_scope)
    {
        return Import(ref node_embedding_runtime_node_api_scope_close)(runtime, node_api_scope);
    }

#pragma warning restore IDE1006
}
