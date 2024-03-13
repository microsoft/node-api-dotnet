// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !NET7_0_OR_GREATER

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
#if !NETFRAMEWORK
using SysNativeLibrary = System.Runtime.InteropServices.NativeLibrary;
#endif

namespace Microsoft.JavaScript.NodeApi.Runtime;

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
    /// Loads a native library using the high-level API.
    /// </summary>
    /// <param name="libraryName">The name of the native library to be loaded.</param>
    /// <param name="assembly">The assembly loading the native library.</param>
    /// <param name="searchPath">The search path.</param>
    /// <returns>The OS handle for the loaded native library.</returns>
    public static nint Load(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
#if NETFRAMEWORK
        string libraryPath = FindLibrary(libraryName, assembly, searchPath)
            ?? throw new ArgumentNullException(nameof(libraryName));

        return LoadLibrary(libraryPath);
#else
        return SysNativeLibrary.Load(libraryName, assembly, searchPath);
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

    public static bool TryGetExport(nint handle, string name, out nint procAddress)
    {
#if NETFRAMEWORK
        procAddress = GetProcAddress(handle, name);
        return procAddress != default;
#else
        return SysNativeLibrary.TryGetExport(handle, name, out procAddress);
#endif
    }

    /// <summary>
    /// Searches various well-known paths for a library and returns the first result.
    /// </summary>
    /// <param name="libraryName">Name of the library to search for.</param>
    /// <param name="assembly">Assembly to search relative from.</param>
    /// <param name="searchPath">The search path.</param>
    /// <returns>Library path if found, otherwise false.</returns>
    private static string? FindLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (Path.IsPathRooted(libraryName) && File.Exists(libraryName))
        {
            return Path.GetFullPath(libraryName);
        }

        string[] tryLibraryNames;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            tryLibraryNames =
            [
                libraryName,
                $"{libraryName}.dll"
            ];
        }
        else
        {
            string libraryExtension = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "dylib"
                : "so";

            tryLibraryNames =
            [
                libraryName,
                $"lib{libraryName}",
                $"{libraryName}.{libraryExtension}",
                $"lib{libraryName}.{libraryExtension}"
            ];
        }

        string?[] tryDirectories =
        [
            searchPath == null || (searchPath & DllImportSearchPath.AssemblyDirectory) > 0
                ? Path.GetDirectoryName(assembly.Location)
                : null,

            searchPath == null || (searchPath & DllImportSearchPath.SafeDirectories) > 0
                ? Environment.SystemDirectory
                : null,
        ];

        foreach (string? tryDirectory in tryDirectories)
        {
            if (tryDirectory == null)
            {
                continue;
            }

            foreach (string tryLibraryName in tryLibraryNames)
            {
                string tryLibraryPath = Path.Combine(tryDirectory, tryLibraryName);

                if (File.Exists(tryLibraryPath))
                {
                    return tryLibraryPath;
                }
            }
        }

        return null;
    }

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments

    [DllImport("kernel32")]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("kernel32")]
    private static extern nint LoadLibrary(string moduleName);

    [DllImport("kernel32")]
    private static extern nint GetProcAddress(nint hModule, string procName);

    private static nint dlopen(nint fileName, int flags)
    {
        // Some Linux distros / versions have libdl version 2 only.
        // Mac OS only has the unversioned library.
        try
        {
            return dlopen2(fileName, flags);
        }
        catch (DllNotFoundException)
        {
            return dlopen1(fileName, flags);
        }
    }

    [DllImport("libdl", EntryPoint = "dlopen")]
    private static extern nint dlopen1(nint fileName, int flags);

    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern nint dlopen2(nint fileName, int flags);

    private const int RTLD_LAZY = 1;

#pragma warning restore CA2101
}

#endif // !NET7_0_OR_GREATER
