// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static NodeEmbedding;
using static NodejsRuntime;

public class NodeEmbeddingRuntimeSettings
{
    public int? NodeApiVersion { get; set; }
    public NodeEmbeddingRuntimeFlags? RuntimeFlags { get; set; }
    public string[]? Args { get; set; }
    public string[]? RuntimeArgs { get; set; }
    public PreloadCallback? OnPreload { get; set; }
    public string? MainScript { get; set; }
    public LoadingCallback? OnLoading { get; set; }
    public LoadedCallback? OnLoaded { get; set; }
    public IEnumerable<NodeEmbeddingModuleInfo>? Modules { get; set; }
    public PostTaskCallback? OnPostTask { get; set; }
    public ConfigureRuntimeCallback? ConfigureRuntime { get; set; }

    public unsafe ConfigureRuntimeCallback CreateConfigureRuntimeCallback()
        => new((platform, config) =>
        {
            if (NodeApiVersion != null)
            {
                NodeEmbedding.JSRuntime.EmbeddingRuntimeConfigSetNodeApiVersion(
                    config, NodeApiVersion.Value)
                    .ThrowIfFailed();
            }
            if (RuntimeFlags != null)
            {
                NodeEmbedding.JSRuntime.EmbeddingRuntimeConfigSetFlags(config, RuntimeFlags.Value)
                    .ThrowIfFailed();
            }
            if (Args != null || RuntimeArgs != null)
            {
                NodeEmbedding.JSRuntime.EmbeddingRuntimeConfigSetArgs(config, Args, RuntimeArgs)
                    .ThrowIfFailed();
            }

            if (OnPreload != null)
            {
                Functor<node_embedding_runtime_preload_callback> functor =
                    CreateRuntimePreloadFunctor(OnPreload);
                NodeEmbedding.JSRuntime.EmbeddingRuntimeConfigOnPreload(
                    config, functor.Callback, functor.Data, functor.DataRelease)
                    .ThrowIfFailed();
            }

            if (MainScript != null)
            {
                JSValue onLoading(NodeEmbeddingRuntime runtime,
                                  JSValue process,
                                  JSValue require,
                                  JSValue runCommonJS)
                    => runCommonJS.Call(JSValue.Null, (JSValue)MainScript);

                Functor<node_embedding_runtime_loading_callback> functor =
                    CreateRuntimeLoadingFunctor(onLoading);
                NodeEmbedding.JSRuntime.EmbeddingRuntimeConfigOnLoading(
                    config, functor.Callback, functor.Data, functor.DataRelease)
                    .ThrowIfFailed();
            }
            else if (OnLoading != null)
            {
                Functor<node_embedding_runtime_loading_callback> functor =
                    CreateRuntimeLoadingFunctor(OnLoading);
                NodeEmbedding.JSRuntime.EmbeddingRuntimeConfigOnLoading(
                    config, functor.Callback, functor.Data, functor.DataRelease)
                    .ThrowIfFailed();
            }

            if (OnLoaded != null)
            {
                Functor<node_embedding_runtime_loaded_callback> functor =
                    CreateRuntimeLoadedFunctor(OnLoaded);
                NodeEmbedding.JSRuntime.EmbeddingRuntimeConfigOnLoaded(
                    config, functor.Callback, functor.Data, functor.DataRelease)
                    .ThrowIfFailed();
            }

            if (Modules != null)
            {
                foreach (NodeEmbeddingModuleInfo module in Modules)
                {
                    Functor<node_embedding_module_initialize_callback> functor =
                        CreateModuleInitializeFunctor(module.OnInitialize);
                    NodeEmbedding.JSRuntime.EmbeddingRuntimeConfigAddModule(
                        config,
                        module.Name.AsSpan(),
                        functor.Callback,
                        functor.Data,
                        functor.DataRelease,
                        module.NodeApiVersion ?? 0)
                        .ThrowIfFailed();
                }
            }

            if (OnPostTask != null)
            {
                Functor<node_embedding_task_post_callback> functor =
                    CreateTaskPostFunctor(OnPostTask);
                NodeEmbedding.JSRuntime.EmbeddingRuntimeConfigSetTaskRunner(
                    config,
                    new node_embedding_task_post_callback(s_taskPostCallback),
                    (nint)GCHandle.Alloc(OnPostTask),
                    new node_embedding_data_release_callback(s_releaseDataCallback))
                    .ThrowIfFailed();
            }
            ConfigureRuntime?.Invoke(platform, config);
        });
}
