// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !NET7_0_OR_GREATER

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
#if !(NETFRAMEWORK || NETSTANDARD)
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
#if NETFRAMEWORK || NETSTANDARD
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint handle = LoadLibrary(libraryName);
            if (handle == 0)
                throw new DllNotFoundException();

            return handle;
        }
        else
        {
            nint handle = dlopen(libraryName, RTLD_LAZY);
            if (handle == 0)
                throw new DllNotFoundException();

            return handle;
        }
#else
        return SysNativeLibrary.Load(libraryName);
#endif
    }

    /// <summary>
    /// Provides a simple API for loading a native library and returns a value that indicates whether the operation succeeded.
    /// </summary>
    /// <param name="libraryName">The name of the native library to be loaded.</param>
    /// <param name="handle">When the method returns, the OS handle of the loaded native library.</param>
    /// <returns><c>true</c> if the native library was loaded successfully; otherwise, <c>false</c>.</returns>
    public static bool TryLoad(string libraryName, out nint handle)
    {
        if (libraryName is null)
            throw new ArgumentNullException(nameof(libraryName));

#if NETFRAMEWORK || NETSTANDARD
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            handle = LoadLibrary(libraryName);
            return handle != 0;
        }
        else
        {
            handle = dlopen(libraryName, RTLD_LAZY);
            return handle != 0;
        }
#else
        return SysNativeLibrary.TryLoad(libraryName);
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
        if (handle == 0)
            throw new ArgumentNullException(nameof(handle));
        if (name is null)
            throw new ArgumentNullException(nameof(name));

#if NETFRAMEWORK || NETSTANDARD
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint procAddress = GetProcAddress(handle, name);
            if (procAddress == 0)
                throw new EntryPointNotFoundException();

            return procAddress;
        }
        else
        {
            dlerror();
            nint procAddress = dlsym(handle, name);
            if (dlerror() != 0)
                throw new EntryPointNotFoundException();

            return procAddress;
        }
#else
        return SysNativeLibrary.GetExport(handle, name);
#endif
    }

    public static bool TryGetExport(nint handle, string name, out nint procAddress)
    {
#if NETFRAMEWORK || NETSTANDARD
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            procAddress = GetProcAddress(handle, name);
            return procAddress != 0;
        }
        else
        {
            dlerror();
            procAddress = dlsym(handle, name);
            return dlerror() != 0;
        }
#else
        return SysNativeLibrary.TryGetExport(handle, name, out procAddress);
#endif
    }

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments

    [DllImport("kernel32")]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("kernel32")]
    private static extern nint LoadLibrary(string moduleName);

    [DllImport("kernel32")]
    private static extern nint GetProcAddress(nint hModule, string procName);

    private static nint dlerror()
    {
        // Some Linux distros / versions have libdl version 2 only.
        // Mac OS only has the unversioned library.
        try
        {
            return dlerror2();
        }
        catch (DllNotFoundException)
        {
            return dlerror1();
        }
    }

    [DllImport("libdl", EntryPoint = "dlerror")]
    private static extern nint dlerror1();

    [DllImport("libdl.so.2", EntryPoint = "dlerror")]
    private static extern nint dlerror2();

    private static nint dlopen(string fileName, int flags)
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
    private static extern nint dlopen1(string fileName, int flags);

    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern nint dlopen2(string fileName, int flags);

    private static nint dlsym(nint handle, string name)
    {
        // Some Linux distros / versions have libdl version 2 only.
        // Mac OS only has the unversioned library.
        try
        {
            return dlsym2(handle, name);
        }
        catch (DllNotFoundException)
        {
            return dlsym1(handle, name);
        }
    }

    [DllImport("libdl", EntryPoint = "dlsym")]
    private static extern nint dlsym1(nint fileName, string flags);

    [DllImport("libdl.so.2", EntryPoint = "dlsym")]
    private static extern nint dlsym2(nint fileName, string flags);

    private const int RTLD_LAZY = 1;

#pragma warning restore CA2101
}

#endif // !NET7_0_OR_GREATER
