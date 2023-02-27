
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static NodeApi.Hosting.HostFxr;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi.Hosting;

/// <summary>
/// When AOT-compiled, exposes a native entry-point that supports loading the .NET runtime
/// and the Node API <see cref="ManagedHost" />.
/// </summary>
internal partial class NativeHost : IDisposable
{
    private static Version MinimumDotnetVersion { get; } = new(7, 0, 0);

    private const string ManagedHostAssemblyName = nameof(NodeApi);
    private const string ManagedHostTypeName =
        $"{nameof(NodeApi)}.{nameof(NodeApi.Hosting)}.ManagedHost";

    private readonly string _nodeApiHostDir;
    private hostfxr_handle _hostContextHandle;

    public static bool IsTracingEnabled { get; } =
        Environment.GetEnvironmentVariable("NODE_API_DOTNET_TRACE") == "1";

    public static void Trace(string msg)
    {
        if (IsTracingEnabled)
        {
            Console.WriteLine(msg);
            Console.Out.Flush();
        }
    }

    [UnmanagedCallersOnly(
        EntryPoint = nameof(napi_register_module_v1),
        CallConvs = new[] { typeof(CallConvCdecl) })]
    public static napi_value InitializeModule(napi_env env, napi_value exports)
    {
        Trace($"> NativeHost.InitializeModule({env.Handle:X8}, {exports.Handle:X8})");

        ResolveImports();
        try
        {
            using var scope = new JSValueScope(JSValueScopeType.RootNoContext, env);

            var host = new NativeHost();

            // Define a dispose method implemented by the native host that closes the CLR context.
            // The managed host proxy will pass through dispoose calls to this callback.
            new JSValue(exports, scope).DefineProperties(new JSPropertyDescriptor(
                "dispose", (_) => { host.Dispose(); return default; }));

            try
            {
                exports = host.InitializeManagedHost(env, exports);
            }
            catch
            {
                host.Dispose();
                throw;
            }

        }
        catch (Exception ex)
        {
            Trace($"Failed to load CLR native host module: {ex}");
            JSError.ThrowError(ex);
        }

        Trace("< NativeHost.InitializeModule()");

        return exports;
    }

    private static bool s_dllImportResolverInitialized = false;

    private static void ResolveImports()
    {
        if (s_dllImportResolverInitialized)
        {
            return;
        }

        s_dllImportResolverInitialized = true;

        NativeLibrary.SetDllImportResolver(
            typeof(HostFxr).Assembly,
            (libraryName, _, _) =>
            {
                return libraryName switch
                {
                    nameof(HostFxr) => HostFxr.Handle,
                    nameof(NodeApi) => NativeLibrary.GetMainProgramHandle(),
                    _ => default,
                };
            });
    }

    public NativeHost()
    {
        string currentModuleFilePath = GetCurrentModuleFilePath();
        Trace("    Current module path: " + currentModuleFilePath);

        _nodeApiHostDir = Path.GetDirectoryName(currentModuleFilePath)!;

        _hostContextHandle = InitializeManagedRuntime();
    }

    private unsafe hostfxr_handle InitializeManagedRuntime()
    {
        Trace("> NativeHost.InitializeManagedRuntime()");

        string runtimeConfigPath = Path.Join(_nodeApiHostDir, @"NodeApi.runtimeconfig.json");
        Trace("    Runtime config: " + runtimeConfigPath);

        string hostfxrPath = HostFxr.GetHostFxrPath(MinimumDotnetVersion);
        string dotnetRoot = Path.GetDirectoryName(
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(hostfxrPath))))!;
        Trace("    .NET root: " + dotnetRoot);

        // Load the library that provides CLR hosting APIs.
        HostFxr.Initialize(MinimumDotnetVersion);

        int runtimeConfigPathCapacity = HostFxr.Encoding.GetByteCount(runtimeConfigPath) + 2;

        hostfxr_status status;
        hostfxr_handle hostContextHandle;
        fixed (byte* runtimeConfigPathBytes = new byte[runtimeConfigPathCapacity])
        {
            Encode(runtimeConfigPath, runtimeConfigPathBytes, runtimeConfigPathCapacity);

            // Initialize the CLR with configuration from runtimeconfig.json.
            Trace("    Initializing runtime...");

            status = hostfxr_initialize_for_runtime_config(
                runtimeConfigPathBytes, initializeParameters: null, out hostContextHandle);
        }

        CheckStatus(status, "Failed to inialize CLR host.");

        Trace("< NativeHost.InitializeManagedRuntime()");
        return hostContextHandle;
    }

    private unsafe napi_value InitializeManagedHost(napi_env env, napi_value exports)
    {
        Trace($"> NativeHost.InitializeManagedHost({env.Handle:X8}, {exports.Handle:X8})");

        string managedHostPath = Path.Join(_nodeApiHostDir, @"NodeApi.dll");
        Trace("    Managed host: " + managedHostPath);

        // Get a CLR function that can load an assembly.
        Trace("    Getting runtime load-assembly delegate...");
        hostfxr_status status = hostfxr_get_runtime_delegate(
            _hostContextHandle,
            hostfxr_delegate_type.load_assembly_and_get_function_pointer,
            out load_assembly_and_get_function_pointer loadAssembly);
        CheckStatus(status, "Failed to get CLR load-assembly function.");

        // TODO Get the correct assembly version (and publickeytoken) somehow.
        string managedHostTypeName = $"{ManagedHostTypeName}, {ManagedHostAssemblyName}" +
            ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
        Trace("    Loading managed host type: " + managedHostTypeName);

        int managedHostPathCapacity = HostFxr.Encoding.GetByteCount(managedHostPath) + 2;
        int managedHostTypeNameCapacity = HostFxr.Encoding.GetByteCount(managedHostTypeName) + 2;
        int methodNameCapacity = HostFxr.Encoding.GetByteCount(nameof(InitializeModule)) + 2;

        nint initializeModulePointer;
        fixed (byte*
            managedHostPathBytes = new byte[managedHostPathCapacity],
            methodNameBytes = new byte[methodNameCapacity],
            managedHostTypeNameBytes = new byte[managedHostTypeNameCapacity])
        {
            Encode(managedHostPath, managedHostPathBytes, managedHostPathCapacity);
            Encode(
                managedHostTypeName,
                managedHostTypeNameBytes,
                managedHostTypeNameCapacity);
            Encode(nameof(InitializeModule), methodNameBytes, methodNameCapacity);

            // Load the managed host assembly and get a pointer to its module initialize method.
            status = loadAssembly(
                managedHostPathBytes,
                managedHostTypeNameBytes,
                methodNameBytes,
                delegateType: -1 /* UNMANAGEDCALLERSONLY_METHOD */,
                reserved: default,
                out initializeModulePointer);
        }

        CheckStatus(status, "Failed to load managed host assembly.");

        Trace("    Invoking managed host method: " + nameof(InitializeModule));

        // Invoke the managed host initialize method.
        // (It will define some properties on the exports object passed in.)
        napi_register_module_v1 initializeModule =
            Marshal.GetDelegateForFunctionPointer<napi_register_module_v1>(
                initializeModulePointer);
        exports = initializeModule(env, exports);

        Trace("< NativeHost.InitializeManagedHost()");
        return exports;
    }

    public void Dispose()
    {
        // Close the CLR host context handle, if it's still open.
        if (_hostContextHandle != default)
        {
            hostfxr_status status = hostfxr_close(_hostContextHandle);
            _hostContextHandle = default;
            CheckStatus(status, "Failed to dispose CLR host.");
        }
    }

    private static void CheckStatus(hostfxr_status status, string message)
    {
        if (status != hostfxr_status.Success &&
            status != hostfxr_status.Success_HostAlreadyInitialized)
        {
            throw new Exception(Enum.IsDefined(status) ?
                $"{message} Status: {status}" : $"{message} HRESULT: 0x{(uint)status:x8}");
        }
    }

    private static unsafe string GetCurrentModuleFilePath()
    {
        Trace("> NativeHost.GetCurrentModuleFilePath()");

        // Unfortunately Assembly.Location/Codebase doesn't work for an AOT compiled library.

        delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value> functionInModule =
            &InitializeModule;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const uint GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;
            if (GetModuleHandleExW(
                GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
                functionInModule,
                out nint moduleHandle) == 0)
            {
                throw new Exception("Failed to get current module handle.");
            }

            uint filePathCapacity = 255;
            char* filePathChars = stackalloc char[(int)filePathCapacity];
            uint filePathLength = GetModuleFileNameW(
                moduleHandle, filePathChars, filePathCapacity);
            if (filePathLength == 0)
            {
                throw new Exception("Failed to get current module file path.");
            }

            return new string(filePathChars);
        }
        else
        {
            if (dladdr(functionInModule, out DLInfo dlinfo) == 0)
            {
                throw new Exception("Failed to get current module file path.");
            }

            // Find the length of the null-terminated C-string.
            int filePathLength = 0;
            while (dlinfo.fileName[filePathLength] != 0)
            {
                filePathLength++;
            }

            Trace("< NativeHost.GetCurrentModuleFilePath()");
            return new string(dlinfo.fileName, 0, filePathLength, System.Text.Encoding.UTF8);
        }
    }

    [LibraryImport("kernel32", SetLastError = true)]
    private static unsafe partial uint GetModuleHandleExW(
        uint flags,
        void* moduleNameOrAddress,
        out nint moduleHandle);

    [LibraryImport("kernel32", SetLastError = true)]
    private static unsafe partial uint GetModuleFileNameW(
        nint moduleHandle,
        char* moduleFileName,
        uint nSize);

    [LibraryImport(nameof(NodeApi))] // Not really a node API, but a function in the main process module.
    private static unsafe partial int dladdr(void* addr, out DLInfo dlinfo);

    private unsafe struct DLInfo
    {
        public sbyte* fileName;
        public void* baseAddress;
        public sbyte* symbolName;
        public void* symbolAddress;
    }
}
