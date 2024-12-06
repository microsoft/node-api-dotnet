// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi.Runtime;

// Imports embedding APIs from libnode.
public unsafe partial class NodejsRuntime
{
#pragma warning disable IDE1006 // Naming: missing prefix '_'

    private delegate* unmanaged[Cdecl]<node_embedding_handle_error_functor, node_embedding_status>
        node_embedding_on_error;

    private delegate* unmanaged[Cdecl]<int, int, node_embedding_status>
        node_embedding_set_api_version;

    private delegate* unmanaged[Cdecl]<
        int,
        nint,
        node_embedding_configure_platform_functor_ref,
        node_embedding_configure_runtime_functor_ref,
        node_embedding_status> node_embedding_run_main;

    private delegate* unmanaged[Cdecl]<
        int,
        nint,
        node_embedding_configure_platform_functor_ref,
        nint,
        node_embedding_status> node_embedding_create_platform;

    private delegate* unmanaged[Cdecl]<node_embedding_platform, node_embedding_status>
        node_embedding_delete_platform;

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform_config,
        node_embedding_platform_flags,
        node_embedding_status> node_embedding_platform_set_flags;

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_get_args_functor_ref,
        node_embedding_get_args_functor_ref,
        node_embedding_status> node_embedding_platform_get_parsed_args;

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_configure_runtime_functor_ref,
        node_embedding_status> node_embedding_run_runtime;

    private delegate* unmanaged[Cdecl]<
        node_embedding_platform,
        node_embedding_configure_runtime_functor_ref,
        nint,
        node_embedding_status> node_embedding_create_runtime;

    private delegate* unmanaged[Cdecl]<node_embedding_runtime, node_embedding_status>
        node_embedding_delete_runtime;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        node_embedding_runtime_flags,
        node_embedding_status> node_embedding_runtime_set_flags;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        int,
        nint,
        int,
        nint,
        node_embedding_status> node_embedding_runtime_set_args;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        node_embedding_preload_functor,
        node_embedding_status> node_embedding_runtime_on_preload;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        node_embedding_start_execution_functor,
        node_embedding_handle_result_functor,
        node_embedding_status> node_embedding_runtime_on_start_execution;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        nint,
        node_embedding_initialize_module_functor,
        int,
        node_embedding_status> node_embedding_runtime_add_module;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime_config,
        node_embedding_post_task_functor,
        node_embedding_status> node_embedding_runtime_set_task_runner;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_event_loop_run_mode,
        nint,
        node_embedding_status> node_embedding_run_event_loop;

    private delegate* unmanaged[Cdecl]<node_embedding_runtime, node_embedding_status>
        node_embedding_complete_event_loop;

    private delegate* unmanaged[Cdecl]<node_embedding_runtime, node_embedding_status>
        node_embedding_terminate_event_loop;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_run_node_api_functor_ref,
        node_embedding_status> node_embedding_run_node_api;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        nint,
        nint,
        node_embedding_status> node_embedding_open_node_api_scope;

    private delegate* unmanaged[Cdecl]<
        node_embedding_runtime,
        node_embedding_node_api_scope,
        node_embedding_status> node_embedding_close_node_api_scope;

    public override node_embedding_status
    EmbeddingOnError(node_embedding_handle_error_functor error_handler)
    {
        return Import(ref node_embedding_on_error)(error_handler);
    }

    public override node_embedding_status EmbeddingSetApiVersion(
        int embedding_api_version,
        int node_api_version)
    {
        return Import(ref node_embedding_set_api_version)(
            embedding_api_version, node_api_version);
    }

    public override node_embedding_status EmbeddingRunMain(
        ReadOnlySpan<string> args,
        node_embedding_configure_platform_functor_ref configure_platform,
        node_embedding_configure_runtime_functor_ref configure_runtime)
    {
        using Utf8StringArray utf8Args = new(args);
        nint argsPtr = utf8Args.Pin();
        return Import(ref node_embedding_run_main)(
            args.Length, argsPtr, configure_platform, configure_runtime);
    }

    public override node_embedding_status EmbeddingCreatePlatform(
        ReadOnlySpan<string> args,
        node_embedding_configure_platform_functor_ref configure_platform,
        out node_embedding_platform result)
    {
        using Utf8StringArray utf8Args = new(args);
        fixed (nint* argsPtr = &utf8Args.Pin())
        fixed (node_embedding_platform* result_ptr = &result)
        {
            return Import(ref node_embedding_create_platform)(
                args.Length, (nint)argsPtr, configure_platform, (nint)result_ptr);
        }
    }

    public override node_embedding_status
        EmbeddingDeletePlatform(node_embedding_platform platform)
    {
        return Import(ref node_embedding_delete_platform)(platform);
    }

    public override node_embedding_status EmbeddingPlatformSetFlags(
        node_embedding_platform_config platform_config,
        node_embedding_platform_flags flags)
    {
        return Import(ref node_embedding_platform_set_flags)(platform_config, flags);
    }

    public override node_embedding_status EmbeddingPlatformGetParsedArgs(
        node_embedding_platform platform,
        node_embedding_get_args_functor_ref get_args,
        node_embedding_get_args_functor_ref get_runtime_args)
    {
        return Import(ref node_embedding_platform_get_parsed_args)(
            platform, get_args, get_runtime_args);
    }

    public override node_embedding_status EmbeddingRunRuntime(
        node_embedding_platform platform,
        node_embedding_configure_runtime_functor_ref configure_runtime)
    {
        return Import(ref node_embedding_run_runtime)(platform, configure_runtime);
    }

    public override node_embedding_status EmbeddingCreateRuntime(
        node_embedding_platform platform,
        node_embedding_configure_runtime_functor_ref configure_runtime,
        out node_embedding_runtime result)
    {
        fixed (node_embedding_runtime* result_ptr = &result)
        {
            return Import(ref node_embedding_create_runtime)(
                platform, configure_runtime, (nint)result_ptr);
        }
    }

    public override node_embedding_status
        EmbeddingDeleteRuntime(node_embedding_runtime runtime)
    {
        return Import(ref node_embedding_delete_runtime)(runtime);
    }

    public override node_embedding_status EmbeddingRuntimeSetFlags(
        node_embedding_runtime_config runtime_config,
        node_embedding_runtime_flags flags)
    {
        return Import(ref node_embedding_runtime_set_flags)(runtime_config, flags);
    }

    public override node_embedding_status EmbeddingRuntimeSetArgs(
        node_embedding_runtime_config runtime_config,
        ReadOnlySpan<string> args,
        ReadOnlySpan<string> runtime_args)
    {
        using Utf8StringArray utf8Args = new(args);
        nint argsPtr = utf8Args.Pin();
        using Utf8StringArray utf8RuntimeArgs = new(runtime_args);
        nint runtimeArgsPtr = utf8RuntimeArgs.Pin();
        return Import(ref node_embedding_runtime_set_args)(
            runtime_config, args.Length, argsPtr, runtime_args.Length, runtimeArgsPtr);
    }

    public override node_embedding_status EmbeddingRuntimeOnPreload(
        node_embedding_runtime_config runtime_config,
        node_embedding_preload_functor run_preload)
    {
        return Import(ref node_embedding_runtime_on_preload)(runtime_config, run_preload);
    }

    public override node_embedding_status EmbeddingRuntimeOnStartExecution(
        node_embedding_runtime_config runtime_config,
        node_embedding_start_execution_functor start_execution,
        node_embedding_handle_result_functor handle_result)
    {
        return Import(ref node_embedding_runtime_on_start_execution)(
            runtime_config, start_execution, handle_result);
    }

    public override node_embedding_status EmbeddingRuntimeAddModule(
        node_embedding_runtime_config runtime_config,
        string moduleName,
        node_embedding_initialize_module_functor init_module,
        int module_node_api_version)
    {
        using (PooledBuffer moduleNameBuffer = PooledBuffer.FromStringUtf8(moduleName))
            fixed (byte* moduleNamePtr = &moduleNameBuffer.Pin())
                return Import(ref node_embedding_runtime_add_module)(
                   runtime_config, (nint)moduleNamePtr, init_module, module_node_api_version);
    }

    public override node_embedding_status EmbeddingRuntimeSetTaskRunner(
        node_embedding_runtime_config runtime_config,
        node_embedding_post_task_functor post_task)
    {
        return Import(ref node_embedding_runtime_set_task_runner)(runtime_config, post_task);
    }

    public override node_embedding_status EmbeddingRunEventLoop(
        node_embedding_runtime runtime,
        node_embedding_event_loop_run_mode run_mode,
        out bool has_more_work)
    {
        c_bool resultBool = default;
        c_bool* result_ptr = &resultBool;
        node_embedding_status status = Import(ref node_embedding_run_event_loop)(
            runtime, run_mode, (nint)result_ptr);
        has_more_work = (bool)resultBool;
        return status;
    }

    public override node_embedding_status EmbeddingCompleteEventLoop(node_embedding_runtime runtime)
    {
        return Import(ref node_embedding_complete_event_loop)(runtime);
    }

    public override node_embedding_status
        EmbeddingTerminateEventLoop(node_embedding_runtime runtime)
    {
        return Import(ref node_embedding_terminate_event_loop)(runtime);
    }

    public override node_embedding_status EmbeddingRunNodeApi(
        node_embedding_runtime runtime,
        node_embedding_run_node_api_functor_ref run_node_api)
    {
        return Import(ref node_embedding_run_node_api)(runtime, run_node_api);
    }

    public override node_embedding_status EmbeddingOpenNodeApiScope(
        node_embedding_runtime runtime,
        out node_embedding_node_api_scope node_api_scope,
        out napi_env env)
    {
        fixed (node_embedding_node_api_scope* scopePtr = &node_api_scope)
        fixed (napi_env* envPtr = &env)
        {
            return Import(ref node_embedding_open_node_api_scope)(
                runtime, (nint)scopePtr, (nint)envPtr);
        }
    }

    public override node_embedding_status EmbeddingCloseNodeApiScope(
        node_embedding_runtime runtime,
        node_embedding_node_api_scope node_api_scope)
    {
        return Import(ref node_embedding_close_node_api_scope)(runtime, node_api_scope);
    }

#pragma warning restore IDE1006
}
