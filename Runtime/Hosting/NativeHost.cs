
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static NodeApi.Hosting.HostFxr;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi.Hosting;

internal class NativeHost : IDisposable
{
    private const string ManagedHostAssemblyName = nameof(NodeApi);
    private const string ManagedHostTypeName =
        $"{nameof(NodeApi)}.{nameof(NodeApi.Hosting)}.ManagedHost";

    private hostfxr_handle _hostContextHandle;

    [UnmanagedCallersOnly(
        EntryPoint = nameof(napi_register_module_v1),
        CallConvs = new[] { typeof(CallConvCdecl) })]
    public static napi_value InitializeModule(napi_env env, napi_value exports)
    {
        ////Console.WriteLine("> NativeHost.InitializeModule()");

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

        ////Console.WriteLine("< NativeHost.InitializeModule()");

        return exports;
    }

    public NativeHost(napi_env env, napi_value exports)
    {
        string currentModuleFilePath = GetCurrentModuleFilePath();
        ////Console.WriteLine("Current module path: " + currentModuleFilePath);
        string nodeApiHostDir = Path.GetDirectoryName(currentModuleFilePath)!;

        string runtimeConfigJsonPath = Path.Join(nodeApiHostDir, @"NodeApi.runtimeconfig.json");
        ////Console.WriteLine("CLR config: " + runtimeConfigJsonPath);

        var managedHostAsssemblyPath = Path.Join(nodeApiHostDir, @"NodeApi.dll");
        ////Console.WriteLine("Managed host: " + managedHostAsssemblyPath);

        // Load the library that provides CLR hosting APIs.
        HostFxr.Initialize();

        // https://github.com/vmoroz/napi-cs/blob/dev/NodeApi.Sdk.CLR/Build/nativehost.cpp

        // Initialize the CLR with configuration from runtimeconfig.json.
        hostfxr_status status = hostfxr_initialize_for_runtime_config(
            runtimeConfigJsonPath, initializeParameters: default, out _hostContextHandle);
        CheckStatus(status, "Failed to inialize CLR host.");

        try
        {
            // Get a CLR function that can load an assembly.
            status = hostfxr_get_runtime_delegate(
                _hostContextHandle,
                hostfxr_delegate_type.load_assembly_and_get_function_pointer,
                out load_assembly_and_get_function_pointer loadAssembly);
            CheckStatus(status, "Failed to get CLR load-assembly function.");

            // TODO Get the correct assembly version (and publickeytoken) somehow.
            string managedHostTypeAssemblyQualifiedName =
                $"{ManagedHostTypeName}, {ManagedHostAssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";

            ////Console.WriteLine("Loading managed host type: " + managedHostTypeAssemblyQualifiedName);

            // Load the managed host assembly and get a pointer to its module initialize method.
            status = loadAssembly(
                managedHostAsssemblyPath,
                managedHostTypeAssemblyQualifiedName,
                nameof(InitializeModule),
                delegateType: -1 /* UNMANAGEDCALLERSONLY_METHOD */,
                reserved: default,
                out nint initializeModulePointer);
            CheckStatus(status, "Failed to load managed host assembly.");

            // Invoke the managed host initialize method.
            // (It will define some properties on the exports object passed in.)
            napi_register_module_v1 initializeModule =
                Marshal.GetDelegateForFunctionPointer<napi_register_module_v1>(
                    initializeModulePointer);
            initializeModule(env, exports);
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
        // TODO: Use dladdr() on non-Windows systems to get the current module file path.

        delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value> functionInModule =
            &InitializeModule;

        const uint GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS = 0x00000004;
        if (GetModuleHandleEx(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS,
            (nint)functionInModule,
            out nint moduleHandle) == 0)
        {
            throw new Exception("Failed to get current module handle.");
        }

        StringBuilder filePathBuilder = new(255);
        GetModuleFileName(moduleHandle, filePathBuilder, filePathBuilder.Capacity);
        return filePathBuilder.ToString();
    }

    [DllImport("kernel32", SetLastError = true)]
    private static extern uint GetModuleHandleEx(
        [In] uint flags,
        [In] nint moduleNameOrAddress,
        out nint moduleHandle);

    [DllImport("kernel32", SetLastError = true)]
    private static extern uint GetModuleFileName(
        [In] nint moduleHandle,
        [Out] StringBuilder moduleFileName,
        [In] [MarshalAs(UnmanagedType.U4)] int nSize);
}
