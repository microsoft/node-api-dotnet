// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.Runtime;

using static JSRuntime;

/// <summary>
/// Manages a Node.js platform instance, provided by `libnode`.
/// </summary>
/// <remarks>
/// Only one Node.js platform instance can be created per process. Once the platform is disposed,
/// another platform instance cannot be re-initialized. One or more <see cref="NodejsEnvironment" />
/// instances may be created using the platform.
/// </remarks>
public sealed class NodejsPlatform : IDisposable
{
    private readonly napi_platform _platform;

    public static implicit operator napi_platform(NodejsPlatform platform) => platform._platform;

    /// <summary>
    /// Initializes the Node.js platform.
    /// </summary>
    /// <param name="libnode">
    /// Name of the `libnode` shared library.
    /// Has to be a full file path when using .NET Framework.
    /// </param>
    /// <param name="args">Optional platform arguments.</param>
    /// <exception cref="InvalidOperationException">A Node.js platform instance has already been
    /// loaded in the current process.</exception>
    public NodejsPlatform(
        string libnode,
        string[]? args = null)
    {
        if (Current != null)
        {
            throw new InvalidOperationException(
                "Only one Node.js platform instance per process is allowed.");
        }

        var entryAssembly = Assembly.GetAssembly(typeof(NodejsPlatform));

        nint libnodeHandle =
            entryAssembly != null
                ? NativeLibrary.Load(
                    libnode,
                    entryAssembly!,
                    DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories
                )
                : NativeLibrary.Load(libnode);

        Runtime = new NodejsRuntime(libnodeHandle);

        Runtime.CreatePlatform(args, (error) => Console.WriteLine(error), out _platform)
            .ThrowIfFailed();
        Current = this;
    }

    /// <summary>
    /// Gets the Node.js platform instance for the current process, or null if not initialized.
    /// </summary>
    public static NodejsPlatform? Current { get; private set; }

    public JSRuntime Runtime { get; }

    /// <summary>
    /// Gets a value indicating whether the current platform has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Disposes the platform. After disposal, another platform instance may not be initialized
    /// in the current process.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        Runtime.DestroyPlatform(_platform);
    }

    /// <summary>
    /// Creates a new Node.js environment with a dedicated main thread.
    /// </summary>
    /// <param name="baseDir">Optional directory that is used as the base directory when resolving
    /// imported modules, and also as the value of the global `__dirname` property. If unspecified,
    /// importing modules is not enabled and `__dirname` is undefined.</param>
    /// <param name="mainScript">Optional script to run in the environment. (Literal script content,
    /// not a path to a script file.)</param>
    /// <returns>A new <see cref="NodejsEnvironment" /> instance.</returns>
    public NodejsEnvironment CreateEnvironment(
        string? baseDir = null,
        string? mainScript = null)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsPlatform));

        return new NodejsEnvironment(this, baseDir, mainScript);
    }
}
