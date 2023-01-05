
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
            Console.Error.Flush();
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
        Trace("    Runtime config: " + runtimeConfigPath);

        string managedHostPath = Path.Join(nodeApiHostDir, @"NodeApi.dll");
        Trace("    Managed host: " + managedHostPath);

        string hostfxrPath = HostFxr.GetHostFxrPath();
        string dotnetRoot = Path.GetDirectoryName(
            Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(hostfxrPath)))) !;
        Trace("    .NET root: " + dotnetRoot);

        // Load the library that provides CLR hosting APIs.
        HostFxr.Initialize();

        // HostFxr APIs use UTF-16 on Windows, UTF-8 elsewhere.
        Encoding encoding = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ?
            Encoding.Unicode : Encoding.UTF8;

        Trace("    Encoding runtime path parameters");
        int runtimeConfigPathCapacity = encoding.GetByteCount(runtimeConfigPath) + 2;
        int hostfxrPathCapacity = encoding.GetByteCount(hostfxrPath) + 2;
        int dotnetRootCapacity = encoding.GetByteCount(dotnetRoot) + 2;

        hostfxr_status status;
        fixed (byte*
            runtimeConfigPathBytes = new byte[runtimeConfigPathCapacity],
            hostfxrPathBytes = new byte[hostfxrPathCapacity],
            dotnetRootBytes = new byte[dotnetRootCapacity])
        {
            Encode(encoding, runtimeConfigPath, runtimeConfigPathBytes, runtimeConfigPathCapacity);
            Encode(encoding, hostfxrPath, hostfxrPathBytes, hostfxrPathCapacity);
            Encode(encoding, dotnetRoot, dotnetRootBytes, dotnetRootCapacity);

            var initializeParameters = new hostfxr_initialize_parameters
            {
                size = (nuint)sizeof(hostfxr_initialize_parameters),
                host_path = hostfxrPathBytes,
                dotnet_root = dotnetRootBytes,
            };

            // Initialize the CLR with configuration from runtimeconfig.json.
            Trace("    Initializing runtime...");
            status = hostfxr_initialize_for_runtime_config(
                runtimeConfigPathBytes, &initializeParameters, out _hostContextHandle);
        }

        CheckStatus(status, "Failed to inialize CLR host.");

        try
        {
            // Get a CLR function that can load an assembly.
            Trace("    Getting runtime load-assembly delegate...");
            status = hostfxr_get_runtime_delegate(
                _hostContextHandle,
                hostfxr_delegate_type.load_assembly_and_get_function_pointer,
                out load_assembly_and_get_function_pointer loadAssembly);
            CheckStatus(status, "Failed to get CLR load-assembly function.");

            // TODO Get the correct assembly version (and publickeytoken) somehow.
            string managedHostTypeName = $"{ManagedHostTypeName}, {ManagedHostAssemblyName}" +
                ", Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            Trace("    Loading managed host type: " + managedHostTypeName);

            int managedHostPathCapacity = encoding.GetByteCount(managedHostPath) + 2;
            int managedHostTypeNameCapacity = encoding.GetByteCount(managedHostTypeName) + 2;
            int methodNameCapacity = encoding.GetByteCount(nameof(InitializeModule)) + 2;

            nint initializeModulePointer;
            fixed (byte*
                managedHostPathBytes = new byte[managedHostPathCapacity],
                methodNameBytes = new byte[methodNameCapacity],
                managedHostTypeNameBytes = new byte[managedHostTypeNameCapacity])
            {
                Encode(encoding, managedHostPath, managedHostPathBytes, managedHostPathCapacity);
                Encode(
                    encoding,
                    managedHostTypeName,
                    managedHostTypeNameBytes,
                    managedHostTypeNameCapacity);
                Encode(encoding, nameof(InitializeModule), methodNameBytes, methodNameCapacity);

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

    private static unsafe void Encode(Encoding encoding, string str, byte* bytes, int capacity)
    {
        var span = new Span<byte>(bytes, capacity);
        int encodedCount = encoding.GetBytes(str, span);
        span.Slice(encodedCount, capacity - encodedCount).Clear();
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
