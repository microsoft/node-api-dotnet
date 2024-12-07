// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using System.Runtime.InteropServices;
using static JSRuntime;
using static NodejsEmbedding;

/// <summary>
/// Manages a Node.js platform instance, provided by `libnode`.
/// </summary>
/// <remarks>
/// Only one Node.js platform instance can be created per process. Once the platform is disposed,
/// another platform instance cannot be re-initialized. One or more <see cref="NodejsEmbeddingThreadRuntime" />
/// instances may be created using the platform.
/// </remarks>
public sealed class NodejsEmbeddingPlatform : IDisposable
{
    private node_embedding_platform _platform;

    public static implicit operator node_embedding_platform(NodejsEmbeddingPlatform platform)
        => platform._platform;

    /// <summary>
    /// Initializes the Node.js platform.
    /// </summary>
    /// <param name="libnodePath">Path to the `libnode` shared library, including extension.</param>
    /// <param name="settings">Optional platform settings.</param>
    /// <exception cref="InvalidOperationException">A Node.js platform instance has already been
    /// loaded in the current process.</exception>
    public unsafe NodejsEmbeddingPlatform(
        string libnodePath, NodejsEmbeddingPlatformSettings? settings)
    {
        if (Current != null)
        {
            throw new InvalidOperationException(
                "Only one Node.js platform instance per process is allowed.");
        }
        Current = this;
        Initialize(libnodePath);

        if (settings?.OnError != null)
        {
            var handle_error_functor = new node_embedding_handle_error_functor
            {
                data = (nint)GCHandle.Alloc(settings.OnError),
                invoke = new node_embedding_handle_error_callback(s_handleErrorCallback),
                release = new node_embedding_release_data_callback(s_releaseDataCallback),
            };
            JSRuntime.EmbeddingOnError(handle_error_functor).ThrowIfFailed();
        }

        JSRuntime.EmbeddingSetApiVersion(EmbeddingApiVersion, NodeApiVersion).ThrowIfFailed();

        using node_embedding_configure_platform_functor_ref configurePlatformFunctorRef =
            settings ?? new NodejsEmbeddingPlatformSettings();

        JSRuntime.EmbeddingCreatePlatform(
            settings?.Args, configurePlatformFunctorRef, out _platform)
            .ThrowIfFailed();
    }

    internal NodejsEmbeddingPlatform(node_embedding_platform platform)
    {
        _platform = platform;
    }

    /// <summary>
    /// Gets a value indicating whether the current platform has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the Node.js platform instance for the current process, or null if not initialized.
    /// </summary>
    public static NodejsEmbeddingPlatform? Current { get; private set; }

    public static JSRuntime JSRuntime => NodejsEmbedding.JSRuntime;

    /// <summary>
    /// Disposes the platform. After disposal, another platform instance may not be initialized
    /// in the current process.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        JSRuntime.EmbeddingDeletePlatform(_platform);
    }

    /// <summary>
    /// Creates a new Node.js embedding runtime with a dedicated main thread.
    /// </summary>
    /// <param name="baseDir">Optional directory that is used as the base directory when resolving
    /// imported modules, and also as the value of the global `__dirname` property. If unspecified,
    /// importing modules is not enabled and `__dirname` is undefined.</param>
    /// <param name="mainScript">Optional script to run in the environment. (Literal script content,
    /// not a path to a script file.)</param>
    /// <returns>A new <see cref="NodejsEmbeddingThreadRuntime" /> instance.</returns>
    public NodejsEmbeddingThreadRuntime CreateThreadRuntime(
        string? baseDir = null,
        NodejsEmbeddingRuntimeSettings? settings = null)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEmbeddingPlatform));

        return new NodejsEmbeddingThreadRuntime(this, baseDir, settings);
    }

    public unsafe void GetParsedArgs(GetArgsCallback? getArgs, GetArgsCallback? getRuntimeArgs)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEmbeddingPlatform));

        using var getArgsFunctorRef = new node_embedding_get_args_functor_ref(
            getArgs, new node_embedding_get_args_callback(s_getArgsCallback));
        using var getRuntimeArgsFunctorRef = new node_embedding_get_args_functor_ref(
            getRuntimeArgs, new node_embedding_get_args_callback(s_getArgsCallback));

        JSRuntime.EmbeddingPlatformGetParsedArgs(
            _platform, getArgsFunctorRef, getRuntimeArgsFunctorRef).ThrowIfFailed();
    }

    public string[] GetParsedArgs()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEmbeddingPlatform));

        string[]? result = null;
        GetParsedArgs((string[] args) => result = args, null);
        return result ?? [];
    }

    public string[] GetRuntimeParsedArgs()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEmbeddingPlatform));

        string[]? result = null;
        GetParsedArgs(null, (string[] args) => result = args);
        return result ?? [];
    }
}
