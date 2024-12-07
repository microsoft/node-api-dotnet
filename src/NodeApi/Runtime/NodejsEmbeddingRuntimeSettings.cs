// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static JSRuntime;
using static NodejsEmbedding;

public sealed class NodejsEmbeddingRuntimeSettings
{
    public node_embedding_runtime_flags? RuntimeFlags { get; set; }
    public string[]? Args { get; set; }
    public string[]? RuntimeArgs { get; set; }
    public PreloadCallback? OnPreload { get; set; }
    public StartExecutionCallback? StartExecution { get; set; }
    public string? MainScript { get; set; }
    public HandleResultCallback? HandleStartExecutionResult { get; set; }
    public IEnumerable<NodejsEmbeddingModuleInfo>? Modules { get; set; }
    public PostTaskCallback? OnPostTask { get; set; }
    public ConfigureRuntimeCallback? ConfigureRuntime { get; set; }

    public static JSRuntime JSRuntime => NodejsEmbedding.JSRuntime;

    public static unsafe implicit operator node_embedding_configure_runtime_functor_ref(
        NodejsEmbeddingRuntimeSettings? settings)
    {
        var confgureRuntime = new ConfigureRuntimeCallback((platform, config) =>
        {
            if (settings?.RuntimeFlags != null)
            {
                JSRuntime.EmbeddingRuntimeSetFlags(config, settings.RuntimeFlags.Value)
                    .ThrowIfFailed();
            }
            if (settings?.Args != null || settings?.RuntimeArgs != null)
            {
                JSRuntime.EmbeddingRuntimeSetArgs(config, settings.Args, settings.RuntimeArgs)
                    .ThrowIfFailed();
            }
            if (settings?.OnPreload != null)
            {
                var preloadFunctor = new node_embedding_preload_functor
                {
                    data = (nint)GCHandle.Alloc(settings.OnPreload),
                    invoke = new node_embedding_preload_callback(s_preloadCallback),
                    release = new node_embedding_release_data_callback(s_releaseDataCallback),
                };
                JSRuntime.EmbeddingRuntimeOnPreload(config, preloadFunctor).ThrowIfFailed();
            }
            if (settings?.StartExecution != null
                || settings?.MainScript != null
                || settings?.HandleStartExecutionResult != null)
            {
                StartExecutionCallback? startExecutionCallback =
                    settings?.MainScript != null
                    ? (NodejsEmbeddingRuntime runtime, JSValue process, JSValue require, JSValue runCommonJS)
                        => runCommonJS.Call(JSValue.Null, (JSValue)settings.MainScript)
                    : settings?.StartExecution;
                node_embedding_start_execution_functor startExecutionFunctor =
                    startExecutionCallback != null
                    ? new node_embedding_start_execution_functor
                    {
                        data = (nint)GCHandle.Alloc(startExecutionCallback),
                        invoke = new node_embedding_start_execution_callback(
                            s_startExecutionCallback),
                        release = new node_embedding_release_data_callback(s_releaseDataCallback),
                    } : default;
                node_embedding_handle_result_functor handleStartExecutionResultFunctor =
                    settings?.HandleStartExecutionResult != null
                    ? new node_embedding_handle_result_functor
                    {
                        data = (nint)GCHandle.Alloc(settings.HandleStartExecutionResult),
                        invoke = new node_embedding_handle_result_callback(s_handleResultCallback),
                        release = new node_embedding_release_data_callback(s_releaseDataCallback),
                    } : default;
                JSRuntime.EmbeddingRuntimeOnStartExecution(
                    config, startExecutionFunctor, handleStartExecutionResultFunctor)
                    .ThrowIfFailed();
            }
            if (settings?.Modules != null)
            {
                foreach (NodejsEmbeddingModuleInfo module in settings.Modules)
                {
                    var moduleFunctor = new node_embedding_initialize_module_functor
                    {
                        data = (nint)GCHandle.Alloc(module.OnInitialize
                            ?? throw new ArgumentException("Module initialization is missing")),
                        invoke = new node_embedding_initialize_module_callback(
                            s_initializeModuleCallback),
                        release = new node_embedding_release_data_callback(
                            s_releaseDataCallback),
                    };

                    JSRuntime.EmbeddingRuntimeAddModule(
                        config,
                        module.Name ?? throw new ArgumentException("Module name is missing"),
                        moduleFunctor,
                        module.NodeApiVersion ?? NodeApiVersion)
                        .ThrowIfFailed();
                }
            }
            if (settings?.OnPostTask != null)
            {
                var postTaskFunctor = new node_embedding_post_task_functor
                {
                    data = (nint)GCHandle.Alloc(settings.OnPostTask),
                    invoke = new node_embedding_post_task_callback(s_postTaskCallback),
                    release = new node_embedding_release_data_callback(s_releaseDataCallback),
                };
                JSRuntime.EmbeddingRuntimeSetTaskRunner(config, postTaskFunctor).ThrowIfFailed();
            }
            settings?.ConfigureRuntime?.Invoke(platform, config);
        });

        return new node_embedding_configure_runtime_functor_ref(
            confgureRuntime,
            new node_embedding_configure_runtime_callback(s_configureRuntimeCallback));
    }
}
