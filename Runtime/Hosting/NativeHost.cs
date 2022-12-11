
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static NodeApi.Hosting.HostFxr;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi.Hosting;

internal partial class NativeHost : IDisposable
{
    private const string ManagedHostAssemblyName = nameof(NodeApi);
    private const string ManagedHostTypeName =
        $"{nameof(NodeApi)}.{nameof(NodeApi.Hosting)}.ManagedHost";

    private static readonly bool _enableTracing =
        Environment.GetEnvironmentVariable("NODE_API_DOTNET_TRACE") == "1";

    private hostfxr_handle _hostContextHandle;

    private static void Trace(string msg)
    {
        if (_enableTracing)
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
        Trace("> NativeHost.InitializeModule()");

        ResolveImports();
        try
        {
            using var scope = new JSValueScope(env);

            // Constructing the native host also loads the CLR and initializes the managed host.
            var host = new NativeHost(env, exports);

            // The managed host defined several properties/methods already.
            // Add on a dispose method implemented by the native host that closes the CLR context.
            new JSValue(scope, exports).DefineProperties(new JSPropertyDescriptor(
                nameof(NativeHost.Dispose),
                (_) => { host.Dispose(); return default; }));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load CLR native host module: {ex}");
        }

        Trace("< NativeHost.InitializeModule()");

        return exports;
    }

    private static void ResolveImports()
    {
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

    public NativeHost(napi_env env, napi_value exports)
    {
        string currentModuleFilePath = GetCurrentModuleFilePath();
        Trace("    Current module path: " + currentModuleFilePath);
        string nodeApiHostDir = Path.GetDirectoryName(currentModuleFilePath)!;
        InitializeManagedHost(env, exports, nodeApiHostDir);
    }

    private unsafe void InitializeManagedHost(
        napi_env env,
        napi_value exports,
        string nodeApiHostDir)
    {
        Trace("> NativeHost.InitializeManagedHost()");

        string runtimeConfigPath = Path.Join(nodeApiHostDir, @"NodeApi.runtimeconfig.json");
        Trace("    CLR config: " + runtimeConfigPath);

        string managedHostPath = Path.Join(nodeApiHostDir, @"NodeApi.dll");
        Trace("    Managed host: " + managedHostPath);

        // Load the library that provides CLR hosting APIs.
        HostFxr.Initialize();

        // https://github.com/vmoroz/napi-cs/blob/dev/NodeApi.Sdk.CLR/Build/nativehost.cpp

        // HostFxr APIs use UTF-16 on Windows, UTF-8 elsewhere.
        Encoding encoding = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
            Encoding.Unicode : Encoding.UTF8;

        int runtimeConfigPathCapacity = encoding.GetByteCount(runtimeConfigPath) + 2;
        byte* runtimeConfigPathBytes = stackalloc byte[runtimeConfigPathCapacity];
        encoding.GetBytes(
            runtimeConfigPath, new Span<byte>(runtimeConfigPathBytes, runtimeConfigPathCapacity));

        // Initialize the CLR with configuration from runtimeconfig.json.
        hostfxr_status status = hostfxr_initialize_for_runtime_config(
            runtimeConfigPathBytes, initializeParameters: default, out _hostContextHandle);
        CheckStatus(status, "Failed to inialize CLR host.");

        Trace("    Initialized runtime...");

        try
        {
            // Get a CLR function that can load an assembly.
            status = hostfxr_get_runtime_delegate(
                _hostContextHandle,
                hostfxr_delegate_type.load_assembly_and_get_function_pointer,
                out load_assembly_and_get_function_pointer loadAssembly);
            CheckStatus(status, "Failed to get CLR load-assembly function.");

            Trace("    Got load-assembly function...");

            // TODO Get the correct assembly version (and publickeytoken) somehow.
            string managedHostTypeName = $"{ManagedHostTypeName}, {ManagedHostAssemblyName}" +
                ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

            Trace("    Loading managed host type: " + managedHostTypeName);

            int managedHostPathCapacity = encoding.GetByteCount(managedHostPath) + 2;
            byte* managedHostPathBytes = stackalloc byte[managedHostPathCapacity];
            encoding.GetBytes(
                managedHostPath, new Span<byte>(managedHostPathBytes, managedHostPathCapacity));

            int managedHostTypeNameCapacity = encoding.GetByteCount(managedHostTypeName) + 2;
            byte* managedHostTypeNameBytes = stackalloc byte[managedHostTypeNameCapacity];
            encoding.GetBytes(
                managedHostTypeName,
                new Span<byte>(managedHostTypeNameBytes, managedHostTypeNameCapacity));

            int methodNameCapacity = encoding.GetByteCount(nameof(InitializeModule)) + 2;
            byte* methodNameBytes = stackalloc byte[methodNameCapacity];
            encoding.GetBytes(
                nameof(InitializeModule), new Span<byte>(methodNameBytes, methodNameCapacity));

            // Load the managed host assembly and get a pointer to its module initialize method.
            status = loadAssembly(
                managedHostPathBytes,
                managedHostTypeNameBytes,
                methodNameBytes,
                delegateType: -1 /* UNMANAGEDCALLERSONLY_METHOD */,
                reserved: default,
                out nint initializeModulePointer);
            CheckStatus(status, "Failed to load managed host assembly.");

            Trace("    Invoking managed host method: " + nameof(InitializeModule));

            // Invoke the managed host initialize method.
            // (It will define some properties on the exports object passed in.)
            napi_register_module_v1 initializeModule =
                Marshal.GetDelegateForFunctionPointer<napi_register_module_v1>(
                    initializeModulePointer);
            initializeModule(env, exports);

            Trace("< NativeHost.InitializeManagedHost()");
        }
        catch
        {
            hostfxr_close(_hostContextHandle);
            _hostContextHandle = default;
            throw;
        }
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
            // Technically the dladdr() output argument returns a struct with several fields,
            // but the first field is a pointer to the file path, that is all that's needed.
            if (dladdr(functionInModule, out sbyte* filePathChars) == 0)
            {
                throw new Exception("Failed to get current module file path.");
            }

            Trace("    Reading current module file path result.");

            // Find the length of the null-terminated C-string.
            int filePathLength = 0;
            while (filePathChars[filePathLength] != 0)
            {
                filePathLength++;
            }

            Trace("< NativeHost.GetCurrentModuleFilePath()");
            return new string(filePathChars, 0, filePathLength, Encoding.UTF8);
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
    private static unsafe partial int dladdr(void* addr, out sbyte* filePath);
}
