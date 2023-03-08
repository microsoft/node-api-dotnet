
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Microsoft.JavaScript.NodeApi.DotNetHost.HostFxr;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// When AOT-compiled, exposes a native entry-point that supports loading the .NET runtime
/// and the Node API <see cref="ManagedHost" />.
/// </summary>
internal partial class NativeHost : IDisposable
{
    private static Version MinimumDotnetVersion { get; } = new(7, 0, 0);

    private static readonly string s_managedHostTypeName =
        typeof(NativeHost).Namespace + ".ManagedHost";

    private hostfxr_handle _hostContextHandle;

    public static bool IsTracingEnabled { get; } =
        Environment.GetEnvironmentVariable("TRACE_NODE_API_HOST") == "1";

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

        using JSValueScope scope = new(JSValueScopeType.RootNoContext, env);
        try
        {
            JSNativeApi.Interop.Initialize(NativeLibrary.GetMainProgramHandle());

            NativeHost host = new();

            // Do not use JSModuleBuilder here because it relies on having a current context.
            // But the context will be set by the managed host.
            new JSValue(exports, scope).DefineProperties(
                // The package index.js will invoke the initialize method with the path to
                // the managed host assembly.
                JSPropertyDescriptor.Function("initialize", host.InitializeManagedHost));
        }
        catch (Exception ex)
        {
            string message = $"Failed to load CLR native host module: {ex}";
            Trace(message);
            napi_throw(env, (napi_value)JSValue.CreateError(null, (JSValue)message));
        }

        Trace("< NativeHost.InitializeModule()");

        return exports;
    }

    public NativeHost()
    {
    }

    private unsafe hostfxr_handle InitializeManagedRuntime(string runtimeConfigPath)
    {
        Trace($"> NativeHost.InitializeManagedRuntime({runtimeConfigPath})");

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

    private unsafe JSValue InitializeManagedHost(JSCallbackArgs args)
    {
        string managedHostPath = (string)args[0];
        Trace($"> NativeHost.InitializeManagedHost({managedHostPath})");

        string managedHostAssemblyName = Path.GetFileNameWithoutExtension(managedHostPath);
        string nodeApiAssemblyName = managedHostAssemblyName.Substring(
            0, managedHostAssemblyName.LastIndexOf('.'));

        string runtimeConfigPath = Path.Join(
            Path.GetDirectoryName(managedHostPath), nodeApiAssemblyName + ".runtimeconfig.json");
        _hostContextHandle = InitializeManagedRuntime(runtimeConfigPath);

        // Get a CLR function that can load an assembly.
        Trace("    Getting runtime load-assembly delegate...");
        hostfxr_status status = hostfxr_get_runtime_delegate(
            _hostContextHandle,
            hostfxr_delegate_type.load_assembly_and_get_function_pointer,
            out load_assembly_and_get_function_pointer loadAssembly);
        CheckStatus(status, "Failed to get CLR load-assembly function.");

        // TODO Get the correct assembly version (and publickeytoken) somehow.
        string managedHostTypeName = $"{s_managedHostTypeName}, {managedHostAssemblyName}" +
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

        // Create an "exports" object for the managed host module initialization.
        var exports = JSValue.CreateObject();

        // Define a dispose method implemented by the native host that closes the CLR context.
        // The managed host proxy will pass through dispose calls to this callback.
        exports.DefineProperties(new JSPropertyDescriptor(
            "dispose", (_) => { Dispose(); return default; }));

        exports = initializeModule((napi_env)exports.Scope, (napi_value)exports);

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
}
