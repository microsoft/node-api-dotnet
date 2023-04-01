// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !NETFRAMEWORK

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// P/Invoke declarations and supporting code for the CLR hosting APIs defined in
/// https://github.com/dotnet/runtime/blob/main/src/native/corehost/hostfxr.h
/// </summary>
internal static partial class HostFxr
{
    public static nint Handle { get; private set; }

    public static void Initialize(Version minVersion)
    {
        if (Handle == default)
        {
            NativeHost.Trace("> HostFxr.Initialize()");

            string hostfxrPath = GetHostFxrPath(minVersion);
            NativeHost.Trace("    HostFxr path: " + hostfxrPath);

            if (!File.Exists(hostfxrPath))
            {
                NativeHost.Trace("    HostFxr not found!");
                throw new FileNotFoundException(
                    ".NET runtime host library not found.", hostfxrPath);
            }

            Handle = NativeLibrary.Load(hostfxrPath);

            NativeHost.Trace("< HostFxr.Initialize()");
        }
    }

    // HostFxr APIs use UTF-16 on Windows, UTF-8 elsewhere.
    public static Encoding Encoding { get; } =
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Encoding.Unicode : Encoding.UTF8;

    public static unsafe void Encode(string str, byte* bytes, int capacity)
    {
        var span = new Span<byte>(bytes, capacity);
        int encodedCount = HostFxr.Encoding.GetBytes(str, span);
        span.Slice(encodedCount, capacity - encodedCount).Clear();
    }

    public static string GetHostFxrPath(Version minVersion)
    {
        // TODO: Port more of the logic to find hostfxr path from
        // https://github.com/dotnet/runtime/blob/main/src/native/corehost/nethost/nethost.cpp
        // (Select the correct architecture.)

        string defaultRoot;
        string libraryName;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            defaultRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "dotnet");
            libraryName = "hostfxr.dll";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            defaultRoot = "/usr/share/dotnet";
            libraryName = "libhostfxr.so";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            defaultRoot = "/usr/local/share/dotnet";
            libraryName = "libhostfxr.dylib";
        }
        else
        {
            throw new PlatformNotSupportedException();
        }

        string? dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (string.IsNullOrEmpty(dotnetRoot))
        {
            dotnetRoot = defaultRoot;
        }

        if (!Directory.Exists(dotnetRoot))
        {
            throw new DirectoryNotFoundException(".NET installation not found at " + dotnetRoot);
        }

        string fxrDir = Path.Combine(dotnetRoot, "host", "fxr");
        if (!Directory.Exists(fxrDir))
        {
            throw new DirectoryNotFoundException(".NET HostFXR not found at " + fxrDir);
        }

        string[] versionDirs = Directory.GetDirectories(fxrDir);
        Array.Sort(versionDirs);
        for (int i = versionDirs.Length - 1; i >= 0; i--)
        {
            if (!Version.TryParse(Path.GetFileName(versionDirs[i]), out Version? version))
            {
                continue;
            }

            if (version >= minVersion)
            {
                string hostfxrPath = Path.Combine(versionDirs[i], libraryName);
                return hostfxrPath;
            }
            else
            {
                throw new Exception(
                    $"The latest .NET version found ({version}) " +
                    $"does not meet the minimum requirement ({minVersion}).");
            }
        }

        throw new Exception(".NET HostFXR directory does not contain any versions: " + fxrDir);
    }

    public record struct hostfxr_handle(nint Handle);

    public enum hostfxr_delegate_type
    {
        com_activation,
        load_in_memory_assembly,
        winrt_activation,
        com_register,
        com_unregister,
        load_assembly_and_get_function_pointer,
        get_function_pointer,
    }

    public unsafe struct hostfxr_initialize_parameters
    {
        // Not used.
        /*
        public nuint size;
        public byte* host_path;
        public byte* dotnet_root;
        */
    }

    // The returned function pointer must be converted to a specific delegate via
    // Marshal.GetDelegateForFunctionPointer().
    public unsafe delegate hostfxr_status load_assembly_and_get_function_pointer(
        byte* assemblyPath, // UTF-16 on Windows, UTF-8 elsewhere
        byte* typeName,     // UTF-16 on Windows, UTF-8 elsewhere
        byte* methodName,   // UTF-16 on Windows, UTF-8 elsewhere
        nint delegateType,
        nint reserved,
        nint* outFunctionPointer);

    public static unsafe hostfxr_status hostfxr_initialize_for_runtime_config(
        byte* runtimeConfigPath, // UTF-16 on Windows, UTF-8 elsewhere
        hostfxr_initialize_parameters* initializeParameters,
        out hostfxr_handle hostContextHandle)
    {
        nint funcHandle = NativeLibrary.GetExport(
            Handle, nameof(hostfxr_initialize_for_runtime_config));
        var funcDelegate = (delegate* unmanaged[Cdecl]< // HOSTFXR_CALLTYPE = cdecl
                byte*, hostfxr_initialize_parameters*, hostfxr_handle*, hostfxr_status>)funcHandle;
        hostfxr_handle outContextHandle;
        hostfxr_status status = funcDelegate(
            runtimeConfigPath, initializeParameters, &outContextHandle);
        hostContextHandle = outContextHandle;
        return status;
    }

    public static unsafe hostfxr_status hostfxr_get_runtime_delegate(
        hostfxr_handle hostContextHandle,
        hostfxr_delegate_type delegateType,
        out load_assembly_and_get_function_pointer function)
    {
        nint funcHandle = NativeLibrary.GetExport(Handle, nameof(hostfxr_get_runtime_delegate));
        var funcDelegate = (delegate* unmanaged[Cdecl]< // HOSTFXR_CALLTYPE = cdecl
                hostfxr_handle, hostfxr_delegate_type, nint*, hostfxr_status>)funcHandle;
        nint outFunction;
        hostfxr_status status = funcDelegate(hostContextHandle, delegateType, &outFunction);

        // Wrap the unmanaged delegate with a managed delegate.
        // Note this is CORECLR_DELEGATE_CALLTYPE, which is stdcall on Windows.
        // See https://github.com/dotnet/runtime/blob/main/src/native/corehost/coreclr_delegates.h
        var outFunctionDelegate = (delegate* unmanaged[Stdcall]<
            byte*, byte*, byte*, nint, nint, nint*, hostfxr_status>)outFunction;
        function = status != hostfxr_status.Success ? default! :
            (assemblyPath, typeName, methodName, delegateType, reserved, outFunctionPointer)
                => outFunctionDelegate
            (assemblyPath, typeName, methodName, delegateType, reserved, outFunctionPointer);
        return status;
    }

    public static unsafe hostfxr_status hostfxr_close(hostfxr_handle hostContextHandle)
    {
        nint funcHandle = NativeLibrary.GetExport(Handle, nameof(hostfxr_close));
        var funcDelegate = (delegate* unmanaged[Cdecl]< // HOSTFXR_CALLTYPE = cdecl
            hostfxr_handle, hostfxr_status>)funcHandle;
        return funcDelegate(hostContextHandle);
    }

    public enum hostfxr_status : uint
    {
        // Success
        Success = 0,
        Success_HostAlreadyInitialized = 0x00000001,
        Success_DifferentRuntimeProperties = 0x00000002,

        // Failure
        InvalidArgFailure = 0x80008081,
        CoreHostLibLoadFailure = 0x80008082,
        CoreHostLibMissingFailure = 0x80008083,
        CoreHostEntryPointFailure = 0x80008084,
        CoreHostCurHostFindFailure = 0x80008085,
        // unused                           = 0x80008086,
        CoreClrResolveFailure = 0x80008087,
        CoreClrBindFailure = 0x80008088,
        CoreClrInitFailure = 0x80008089,
        CoreClrExeFailure = 0x8000808a,
        ResolverInitFailure = 0x8000808b,
        ResolverResolveFailure = 0x8000808c,
        LibHostCurExeFindFailure = 0x8000808d,
        LibHostInitFailure = 0x8000808e,
        // unused                           = 0x8000808f,
        LibHostExecModeFailure = 0x80008090,
        LibHostSdkFindFailure = 0x80008091,
        LibHostInvalidArgs = 0x80008092,
        InvalidConfigFile = 0x80008093,
        AppArgNotRunnable = 0x80008094,
        AppHostExeNotBoundFailure = 0x80008095,
        FrameworkMissingFailure = 0x80008096,
        HostApiFailed = 0x80008097,
        HostApiBufferTooSmall = 0x80008098,
        LibHostUnknownCommand = 0x80008099,
        LibHostAppRootFindFailure = 0x8000809a,
        SdkResolverResolveFailure = 0x8000809b,
        FrameworkCompatFailure = 0x8000809c,
        FrameworkCompatRetry = 0x8000809d,
        // unused                           = 0x8000809e,
        BundleExtractionFailure = 0x8000809f,
        BundleExtractionIOError = 0x800080a0,
        LibHostDuplicateProperty = 0x800080a1,
        HostApiUnsupportedVersion = 0x800080a2,
        HostInvalidState = 0x800080a3,
        HostPropertyNotFound = 0x800080a4,
        CoreHostIncompatibleConfig = 0x800080a5,
        HostApiUnsupportedScenario = 0x800080a6,
        HostFeatureDisabled = 0x800080a7,
    }
}

#endif // NETFRAMEWORK
