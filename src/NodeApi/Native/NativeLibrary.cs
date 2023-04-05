// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !NET7_0_OR_GREATER

using System;
using System.Runtime.InteropServices;
#if !NETFRAMEWORK
using SysNativeLibrary = System.Runtime.InteropServices.NativeLibrary;
#endif

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Provides APIs for managing native libraries.
/// </summary>
/// <remarks>
/// The System.Runtime.InteropServices.NativeLibrary class is not available in .NET Framework,
/// and is missing some methods before .NET 7. This fills in those APIs.
/// </remarks>
public static class NativeLibrary
{
    /// <summary>
    /// Gets a handle that can be used with <see cref="GetExport"/> to resolve exports from the
    /// entry point module.
    /// </summary>
    public static nint GetMainProgramHandle()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetModuleHandle(default);
        }
        else
        {
            return dlopen(default, RTLD_LAZY);
        }
    }

    /// <summary>
    /// Loads a native library using default flags.
    /// </summary>
    /// <param name="libraryName">The name of the native library to be loaded.</param>
    /// <returns>The OS handle for the loaded native library.</returns>
    public static nint Load(string libraryName)
    {
#if NETFRAMEWORK
        return LoadLibrary(libraryName);
#else
        return SysNativeLibrary.Load(libraryName);
#endif
    }

    /// <summary>
    /// Gets the address of an exported symbol.
    /// </summary>
    /// <param name="handle">The native library OS handle.</param>
    /// <param name="name">The name of the exported symbol.</param>
    /// <returns>The address of the symbol.</returns>
    public static nint GetExport(nint handle, string name)
    {
#if NETFRAMEWORK
        return GetProcAddress(handle, name);
#else
        return SysNativeLibrary.GetExport(handle, name);
#endif
    }

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments

    [DllImport("kernel32")]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("kernel32")]
    private static extern nint LoadLibrary(string moduleName);

    [DllImport("kernel32")]
    private static extern nint GetProcAddress(nint hModule, string procName);

    [DllImport("libdl")]
    private static extern nint dlopen(nint fileName, int flags);

    private const int RTLD_LAZY = 1;

#pragma warning restore CA2101
}

#endif // !NET7_0_OR_GREATER
