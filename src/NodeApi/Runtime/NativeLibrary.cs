// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !NETCOREAPP3_0_OR_GREATER

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

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
    /// <param name="libraryPath">The name of the native library to be loaded.</param>
    /// <returns>The OS handle for the loaded native library.</returns>
    public static nint Load(string libraryPath)
    {
        return LoadFromPath(libraryPath, throwOnError: true);
    }

    /// <summary>
    /// Provides a simple API for loading a native library and returns a value that indicates whether the operation succeeded.
    /// </summary>
    /// <param name="libraryPath">The name of the native library to be loaded.</param>
    /// <param name="handle">When the method returns, the OS handle of the loaded native library.</param>
    /// <returns><c>true</c> if the native library was loaded successfully; otherwise, <c>false</c>.</returns>
    public static bool TryLoad(string libraryPath, out nint handle)
    {
        handle = LoadFromPath(libraryPath, throwOnError: false);
        return handle != 0;
    }

    static nint LoadFromPath(string libraryPath, bool throwOnError)
    {
        if (libraryPath is null)
            throw new ArgumentNullException(nameof(libraryPath));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint handle = LoadLibrary(libraryPath);
            if (handle == 0 && throwOnError)
                throw new DllNotFoundException(new Win32Exception(Marshal.GetLastWin32Error()).Message);

            return handle;
        }
        else
        {
            dlerror();
            nint handle = dlopen(libraryPath, RTLD_LAZY);
            nint error = dlerror();
            if (error != 0)
            {
                if (throwOnError)
                    throw new DllNotFoundException(Marshal.PtrToStringAuto(error));

                handle = 0;
            }

            return handle;
        }
    }

    /// <summary>
    /// Gets the address of an exported symbol.
    /// </summary>
    /// <param name="handle">The native library OS handle.</param>
    /// <param name="name">The name of the exported symbol.</param>
    /// <returns>The address of the symbol.</returns>
    public static nint GetExport(nint handle, string name)
    {
        return GetSymbol(handle, name, throwOnError: true);
    }

    public static bool TryGetExport(nint handle, string name, out nint procAddress)
    {
        procAddress = GetSymbol(handle, name, throwOnError: false);
        return procAddress != 0;
    }

    static nint GetSymbol(nint handle, string name, bool throwOnError)
    {
        if (handle == 0)
            throw new ArgumentNullException(nameof(handle));
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint procAddress = GetProcAddress(handle, name);
            if (procAddress == 0 && throwOnError)
                throw new EntryPointNotFoundException(new Win32Exception(Marshal.GetLastWin32Error()).Message);

            return procAddress;
        }
        else
        {
            dlerror();
            nint procAddress = dlsym(handle, name);
            nint error = dlerror();
            if (error != 0)
            {
                if (throwOnError)
                    throw new EntryPointNotFoundException(Marshal.PtrToStringAuto(error));

                procAddress = 0;
            }

            return procAddress;
        }
    }

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments

    [DllImport("kernel32")]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("kernel32", SetLastError = true)]
    private static extern nint LoadLibrary(string moduleName);

    [DllImport("kernel32", SetLastError = true)]
    private static extern nint GetProcAddress(nint hModule, string procName);

    private static nint dlerror()
    {
        // some operating systems have dlerror in libc, some in libdl, some in libdl.so.2
        // attempt in that order
        try
        {
            return dlerror0();
        }
        catch (DllNotFoundException)
        {
            try
            {
                return dlerror1();
            }
            catch (DllNotFoundException)
            {
                return dlerror2();
            }
        }
    }

    [DllImport("c", EntryPoint = "dlerror")]
    private static extern nint dlerror0();

    [DllImport("dl", EntryPoint = "dlerror")]
    private static extern nint dlerror1();

    [DllImport("libdl.so.2", EntryPoint = "dlerror")]
    private static extern nint dlerror2();

    private static nint dlopen(string? fileName, int flags)
    {
        // some operating systems have dlopen in libc, some in libdl, some in libdl.so.2
        // attempt in that order
        try
        {
            return dlopen0(fileName, flags);
        }
        catch (DllNotFoundException)
        {
            try
            {
                return dlopen1(fileName, flags);
            }
            catch (DllNotFoundException)
            {
                return dlopen2(fileName, flags);
            }
        }
    }

    [DllImport("c", EntryPoint = "dlopen")]
    private static extern nint dlopen0(string? fileName, int flags);

    [DllImport("dl", EntryPoint = "dlopen")]
    private static extern nint dlopen1(string? fileName, int flags);

    [DllImport("libdl.so.2", EntryPoint = "dlopen")]
    private static extern nint dlopen2(string? fileName, int flags);

    private static nint dlsym(nint handle, string symbol)
    {
        // some operating systems have dlsym in libc, some in libdl, some in libdl.so.2
        // attempt in that order
        try
        {
            return dlsym0(handle, symbol);
        }
        catch (DllNotFoundException)
        {
            try
            {
                return dlsym1(handle, symbol);
            }
            catch (DllNotFoundException)
            {
                return dlsym2(handle, symbol);
            }
        }
    }

    [DllImport("c", EntryPoint = "dlsym")]
    private static extern nint dlsym0(nint handle, string symbol);

    [DllImport("dl", EntryPoint = "dlsym")]
    private static extern nint dlsym1(nint handle, string symbol);

    [DllImport("libdl.so.2", EntryPoint = "dlsym")]
    private static extern nint dlsym2(nint handle, string symbol);

    private const int RTLD_LAZY = 1;

#pragma warning restore CA2101
}

#endif // !NET7_0_OR_GREATER
