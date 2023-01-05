
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NodeApi.Hosting;

/// <summary>
/// P/Invoke declarations and supporting code for the CLR hosting APIs defined in
/// https://github.com/dotnet/runtime/blob/main/src/native/corehost/hostfxr.h
/// </summary>
internal static partial class HostFxr
{
    public static nint Handle { get; private set; }

    public static void Initialize()
    {
        if (Handle == default)
        {
            NativeHost.Trace("> HostFxr.Initialize()");

            string hostfxrPath = GetHostFxrPath();
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

    public static string GetHostFxrPath()
    {
        // TODO: Port logic to find hostfxr path from
        // https://github.com/dotnet/runtime/blob/main/src/native/corehost/nethost/nethost.cpp

        const string dotnetVersion = "7.0.0";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string hostfxrPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                $@"dotnet\host\fxr\{dotnetVersion}\hostfxr.dll");
            return hostfxrPath;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return $"/usr/share/dotnet/host/fxr/{dotnetVersion}/libhostfxr.so";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return $"/usr/local/share/dotnet/host/fxr/{dotnetVersion}/libhostfxr.dylib";
        }
        else
        {
            throw new PlatformNotSupportedException();
        }
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
        public nuint size;
        public byte* host_path;
        public byte* dotnet_root;
    }

    // Note this is CORECLR_DELEGATE_CALLTYPE, which is stdcall on Windows.
    // See https://github.com/dotnet/runtime/blob/main/src/native/corehost/coreclr_delegates.h
    // The returned function pointer must be converted to a specific delegate via
    // Marshal.GetDelegateForFunctionPointer().
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public unsafe delegate hostfxr_status load_assembly_and_get_function_pointer(
        byte* assemblyPath, // UTF-16 on Windows, UTF-8 elsewhere
        byte* typeName,     // UTF-16 on Windows, UTF-8 elsewhere
        byte* methodName,   // UTF-16 on Windows, UTF-8 elsewhere
        nint delegateType,
        nint reserved,
        out nint functionPointer);

#pragma warning disable SYSLIB1054 // Use LibraryImport instead of DllImport

    [DllImport(nameof(HostFxr), CallingConvention = CallingConvention.Cdecl)]
    public static extern unsafe hostfxr_status hostfxr_initialize_for_runtime_config(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string runtimeConfigPath, // UTF-16 on Windows, UTF-8 elsewhere
        hostfxr_initialize_parameters* initializeParameters,
        out hostfxr_handle hostContextHandle);

    [DllImport(nameof(HostFxr), CallingConvention = CallingConvention.Cdecl)]
    public static extern hostfxr_status hostfxr_get_runtime_delegate(
        hostfxr_handle hostContextHandle,
        hostfxr_delegate_type delegateType,
        [MarshalAs(UnmanagedType.FunctionPtr)] out load_assembly_and_get_function_pointer function);

    [DllImport(nameof(HostFxr), CallingConvention = CallingConvention.Cdecl)]
    public static extern hostfxr_status hostfxr_close(hostfxr_handle hostContextHandle);

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
