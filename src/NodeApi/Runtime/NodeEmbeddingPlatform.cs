// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using static NodeEmbedding;
using static NodejsRuntime;

/// <summary>
/// Manages a Node.js platform instance, provided by `libnode`.
/// </summary>
/// <remarks>
/// Only one Node.js platform instance can be created per process. Once the platform is disposed,
/// another platform instance cannot be re-initialized. One or more
/// <see cref="NodeEmbeddingThreadRuntime" /> instances may be created using the platform.
/// </remarks>
public sealed class NodeEmbeddingPlatform : IDisposable
{
    private node_embedding_platform _platform;

    public static explicit operator node_embedding_platform(NodeEmbeddingPlatform platform)
        => platform._platform;

    /// <summary>
    /// Initializes the Node.js platform.
    /// </summary>
    /// <param name="libnodePath">Path to the `libnode` shared library, including extension.</param>
    /// <param name="settings">Optional platform settings.</param>
    /// <exception cref="InvalidOperationException">A Node.js platform instance has already been
    /// loaded in the current process.</exception>
    public NodeEmbeddingPlatform(string libNodePath, NodeEmbeddingPlatformSettings? settings)
    {
        if (Current != null)
        {
            throw new InvalidOperationException(
                "Only one Node.js platform instance per process is allowed.");
        }
        Current = this;
        Initialize(libNodePath);

        using FunctorRef<node_embedding_platform_configure_callback> functorRef =
            CreatePlatformConfigureFunctorRef(settings?.CreateConfigurePlatformCallback());
        JSRuntime.EmbeddingCreatePlatform(
            settings?.Args ?? new string[] { "node" },
            functorRef.Callback,
            functorRef.Data,
            out _platform)
            .ThrowIfFailed();
    }

    public node_embedding_platform Handle => _platform;

    /// <summary>
    /// Gets a value indicating whether the current platform has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Gets the Node.js platform instance for the current process, or null if not initialized.
    /// </summary>
    public static NodeEmbeddingPlatform? Current { get; private set; }

    public static JSRuntime JSRuntime => NodeEmbedding.JSRuntime;

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
    /// <returns>A new <see cref="NodeEmbeddingThreadRuntime" /> instance.</returns>
    public NodeEmbeddingThreadRuntime CreateThreadRuntime(
        string? baseDir = null,
        NodeEmbeddingRuntimeSettings? settings = null)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingPlatform));

        return new NodeEmbeddingThreadRuntime(this, baseDir, settings);
    }

    public unsafe string[] GetParsedArgs()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingPlatform));

        int argc = 0;
        nint argv = 0;
        JSRuntime.EmbeddingPlatformGetParsedArgs(
            _platform, (nint)(&argc), (nint)(&argv), 0, 0).ThrowIfFailed();
        return Utf8StringArray.ToStringArray(argv, argc);
    }

    public unsafe string[] GetRuntimeParsedArgs()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodeEmbeddingPlatform));

        int argc = 0;
        nint argv = 0;
        JSRuntime.EmbeddingPlatformGetParsedArgs(
            _platform, 0, 0, (nint)(&argc), (nint)(&argv)).ThrowIfFailed();
        return Utf8StringArray.ToStringArray(argv, argc);
    }
}
