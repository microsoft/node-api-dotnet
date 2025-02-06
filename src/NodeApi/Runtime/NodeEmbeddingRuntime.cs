// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static NodeEmbedding;
using static NodejsRuntime;

/// <summary>
/// A Node.js runtime.
/// </summary>
/// <remarks>
/// Multiple Node.js environments may be created (concurrently) in the same process.
/// </remarks>
public sealed class NodeEmbeddingRuntime : IDisposable
{
    private node_embedding_runtime _runtime;
    private static readonly
        Dictionary<node_embedding_runtime, NodeEmbeddingRuntime> s_embeddedRuntimes = new();

    public static explicit operator node_embedding_runtime(NodeEmbeddingRuntime runtime)
        => runtime._runtime;

    public static JSRuntime JSRuntime => NodeEmbedding.JSRuntime;

    public unsafe NodeEmbeddingRuntime(
        NodeEmbeddingPlatform platform, NodeEmbeddingRuntimeSettings? settings = null)
    {
        using FunctorRef<node_embedding_runtime_configure_callback> functorRef =
            CreateRuntimeConfigureFunctorRef(settings?.CreateConfigureRuntimeCallback());
        JSRuntime.EmbeddingCreateRuntime(
            platform.Handle,
            functorRef.Callback,
            functorRef.Data,
            out _runtime)
            .ThrowIfFailed();
    }

    private NodeEmbeddingRuntime(node_embedding_runtime runtime)
    {
        _runtime = runtime;
        lock (s_embeddedRuntimes) { s_embeddedRuntimes.Add(runtime, this); }
    }

    public node_embedding_runtime Handle => _runtime;

    public static NodeEmbeddingRuntime? FromHandle(node_embedding_runtime runtime)
    {
        lock (s_embeddedRuntimes)
        {
            if (s_embeddedRuntimes.TryGetValue(
                runtime, out NodeEmbeddingRuntime? embeddingRuntime))
            {
                return embeddingRuntime;
            }
            return null;
        }
    }

    public static NodeEmbeddingRuntime GetOrCreate(node_embedding_runtime runtime)
    {
        NodeEmbeddingRuntime? embeddingRuntime = FromHandle(runtime);
        embeddingRuntime ??= new NodeEmbeddingRuntime(runtime);
        return embeddingRuntime;
    }

    public static unsafe void Run(NodeEmbeddingPlatform platform,
        NodeEmbeddingRuntimeSettings? settings = null)
    {
        ConfigureRuntimeCallback? configureRuntime = settings?.CreateConfigureRuntimeCallback();
        nint callbackData = configureRuntime != null
            ? (nint)GCHandle.Alloc(configureRuntime)
            : default;
        try
        {
            JSRuntime.EmbeddingRunRuntime(
                platform.Handle,
                new node_embedding_runtime_configure_callback(s_runtimeConfigureCallback),
                callbackData)
                .ThrowIfFailed();
        }
        finally
        {
            if (callbackData != default) GCHandle.FromIntPtr(callbackData).Free();
        }
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

        lock (s_embeddedRuntimes) { s_embeddedRuntimes.Remove(_runtime); }
        JSRuntime.EmbeddingDeleteRuntime(_runtime).ThrowIfFailed();
    }

    public unsafe void RunEventLoop()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        JSRuntime.EmbeddingRuntimeRunEventLoop(_runtime).ThrowIfFailed();
    }

    public unsafe void TerminateEventLoop()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        JSRuntime.EmbeddingRuntimeTerminateEventLoop(_runtime).ThrowIfFailed();
    }

    public unsafe bool RunEventLoopOnce()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        JSRuntime.EmbeddingRuntimeRunOnceEventLoop(_runtime, out bool result).ThrowIfFailed();
        return result;
    }

    public unsafe bool RunEventLoopNoWait()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        JSRuntime.EmbeddingRuntimeRunNoWaitEventLoop(_runtime, out bool result).ThrowIfFailed();
        return result;
    }

    public unsafe void RunNodeApi(RunNodeApiCallback runNodeApi)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        nint callbackData = (nint)GCHandle.Alloc(runNodeApi);
        try
        {
            JSRuntime.EmbeddingRuntimeRunNodeApi(
                _runtime,
                new node_embedding_node_api_run_callback(s_nodeApiRunCallback),
                callbackData)
                .ThrowIfFailed();
        }
        finally
        {
            if (callbackData != default) GCHandle.FromIntPtr(callbackData).Free();
        }
    }
}
