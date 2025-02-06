// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
#if UNMANAGED_DELEGATES
using System.Runtime.CompilerServices;
#endif
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
    private bool _mustDeleteRuntime; // Only delete runtime if it was created by calling Create.

    public static JSRuntime JSRuntime => NodeEmbedding.JSRuntime;

    public static unsafe NodeEmbeddingRuntime Create(
        NodeEmbeddingPlatform platform, NodeEmbeddingRuntimeSettings? settings = null)
    {
        using FunctorRef<node_embedding_runtime_configure_callback> functorRef =
            CreateRuntimeConfigureFunctorRef(settings?.CreateConfigureRuntimeCallback());
        JSRuntime.EmbeddingCreateRuntime(
            platform.Handle,
            functorRef.Callback,
            functorRef.Data,
            out node_embedding_runtime runtime)
            .ThrowIfFailed();
        NodeEmbeddingRuntime result = FromHandle(runtime);
        result._mustDeleteRuntime = true;
        return result;
    }

    private NodeEmbeddingRuntime(node_embedding_runtime runtime)
    {
        Handle = runtime;
    }

    public node_embedding_runtime Handle { get; }

    public static unsafe NodeEmbeddingRuntime FromHandle(node_embedding_runtime runtime)
    {
        JSRuntime.EmbeddingRuntimeGetUserData(runtime, out nint userData).ThrowIfFailed();
        if (userData != default)
        {
            return (NodeEmbeddingRuntime)GCHandle.FromIntPtr(userData).Target!;
        }

        NodeEmbeddingRuntime result = new NodeEmbeddingRuntime(runtime);
        JSRuntime.EmbeddingRuntimeSetUserData(
            runtime,
            (nint)GCHandle.Alloc(result),
            new node_embedding_data_release_callback(s_releaseRuntimeCallback))
            .ThrowIfFailed();
        return result;
    }

    public static unsafe void Run(
        NodeEmbeddingPlatform platform, NodeEmbeddingRuntimeSettings? settings = null)
    {
        using FunctorRef<node_embedding_runtime_configure_callback> functorRef =
            CreateRuntimeConfigureFunctorRef(settings?.CreateConfigureRuntimeCallback());
        JSRuntime.EmbeddingRunRuntime(platform.Handle, functorRef.Callback, functorRef.Data)
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
        if (IsDisposed || !_mustDeleteRuntime) return;
        IsDisposed = true;

        JSRuntime.EmbeddingDeleteRuntime(Handle).ThrowIfFailed();
    }

    public unsafe void RunEventLoop()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        JSRuntime.EmbeddingRuntimeRunEventLoop(Handle).ThrowIfFailed();
    }

    public unsafe void TerminateEventLoop()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        JSRuntime.EmbeddingRuntimeTerminateEventLoop(Handle).ThrowIfFailed();
    }

    public unsafe bool RunEventLoopOnce()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        JSRuntime.EmbeddingRuntimeRunOnceEventLoop(Handle, out bool result).ThrowIfFailed();
        return result;
    }

    public unsafe bool RunEventLoopNoWait()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        JSRuntime.EmbeddingRuntimeRunNoWaitEventLoop(Handle, out bool result).ThrowIfFailed();
        return result;
    }

    public unsafe void RunNodeApi(RunNodeApiCallback runNodeApi)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingRuntime));

        using FunctorRef<node_embedding_node_api_run_callback> functorRef =
            CreateNodeApiRunFunctorRef(runNodeApi);
        JSRuntime.EmbeddingRuntimeRunNodeApi(Handle, functorRef.Callback, functorRef.Data)
            .ThrowIfFailed();
    }

#if UNMANAGED_DELEGATES
    private static readonly unsafe delegate* unmanaged[Cdecl]<nint, NodeEmbeddingStatus>
        s_releaseRuntimeCallback = &ReleaseRuntimeCallbackAdapter;
#else
    private static readonly node_embedding_data_release_callback.Delegate
        s_releaseRuntimeCallback = ReleaseRuntimeCallbackAdapter;
#endif

#if UNMANAGED_DELEGATES
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
#endif
    private static unsafe NodeEmbeddingStatus ReleaseRuntimeCallbackAdapter(nint data)
    {
        if (data != default)
        {
            try
            {
                GCHandle gcHandle = GCHandle.FromIntPtr(data);
                NodeEmbeddingRuntime runtime = (NodeEmbeddingRuntime)gcHandle.Target!;
                gcHandle.Free();
                runtime._mustDeleteRuntime = false;
            }
            catch (Exception ex)
            {
                JSRuntime.EmbeddingSetLastErrorMessage(ex.Message.AsSpan());
                return NodeEmbeddingStatus.GenericError;
            }
        }
        return NodeEmbeddingStatus.OK;
    }
}
