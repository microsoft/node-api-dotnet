// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using System.Collections.Generic;
using static JSRuntime;
using static NodejsEmbedding;

/// <summary>
/// A Node.js runtime.
/// </summary>
/// <remarks>
/// Multiple Node.js environments may be created (concurrently) in the same process.
/// </remarks>
public sealed class NodejsEmbeddingRuntime : IDisposable
{
    private node_embedding_runtime _runtime;
    private static readonly
        Dictionary<node_embedding_runtime, NodejsEmbeddingRuntime> _embeddedRuntimes = new();

    public static implicit operator node_embedding_runtime(NodejsEmbeddingRuntime runtime)
        => runtime._runtime;

    public struct Module
    {
        public string Name { get; set; }
        public InitializeModuleCallback OnInitialize { get; set; }
        public int? NodeApiVersion { get; set; }
    }

    public static JSRuntime JSRuntime => NodejsEmbedding.JSRuntime;

    public NodejsEmbeddingRuntime(
        NodejsEmbeddingPlatform platform, NodejsEmbeddingRuntimeSettings? settings = null)
    {
        JSRuntime.EmbeddingCreateRuntime(
            platform, settings ?? new NodejsEmbeddingRuntimeSettings(), out _runtime)
            .ThrowIfFailed();
    }

    private NodejsEmbeddingRuntime(node_embedding_runtime runtime)
    {
        _runtime = runtime;
        lock (_embeddedRuntimes) { _embeddedRuntimes.Add(runtime, this); }
    }

    public static NodejsEmbeddingRuntime? FromHandle(node_embedding_runtime runtime)
    {
        lock (_embeddedRuntimes)
        {
            if (_embeddedRuntimes.TryGetValue(
                runtime, out NodejsEmbeddingRuntime? embeddingRuntime))
            {
                return embeddingRuntime;
            }
            return null;
        }
    }

    public static NodejsEmbeddingRuntime GetOrCreate(node_embedding_runtime runtime)
    {
        NodejsEmbeddingRuntime? embeddingRuntime = FromHandle(runtime);
        if (embeddingRuntime == null)
        {
            embeddingRuntime = new NodejsEmbeddingRuntime(runtime);
        }
        return embeddingRuntime;
    }

    public static void Run(NodejsEmbeddingPlatform platform,
        NodejsEmbeddingRuntimeSettings? settings = null)
    {
        JSRuntime.EmbeddingRunRuntime(platform, settings ?? new NodejsEmbeddingRuntimeSettings())
            .ThrowIfFailed();
    }

    /// <summary>
    /// Gets a value indicating whether the Node.js environment is disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Disposes the Node.js environment, causing its main thread to exit.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        lock (_embeddedRuntimes) { _embeddedRuntimes.Remove(_runtime); }
        JSRuntime.EmbeddingDeleteRuntime(_runtime).ThrowIfFailed();
    }

    public unsafe bool RunEventLoop(node_embedding_event_loop_run_mode runMode)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEmbeddingRuntime));

        return JSRuntime.EmbeddingRunEventLoop(_runtime, runMode, out bool hasMoreWork)
            .ThrowIfFailed(hasMoreWork);
    }

    public unsafe void CompleteEventLoop()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEmbeddingRuntime));

        JSRuntime.EmbeddingCompleteEventLoop(_runtime).ThrowIfFailed();
    }

    public unsafe void TerminateEventLoop()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEmbeddingRuntime));

        JSRuntime.EmbeddingTerminateEventLoop(_runtime).ThrowIfFailed();
    }

    public unsafe void RunNodeApi(RunNodeApiCallback runNodeApi)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEmbeddingRuntime));

        using var runNodeApiFunctorRef = new node_embedding_run_node_api_functor_ref(
            runNodeApi, new node_embedding_run_node_api_callback(s_runNodeApiCallback));
        JSRuntime.EmbeddingRunNodeApi(_runtime, runNodeApiFunctorRef).ThrowIfFailed();
    }
}
