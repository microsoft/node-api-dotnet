// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !NETFRAMEWORK

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.DotNetHost.HostFxr;
using static Microsoft.JavaScript.NodeApi.DotNetHost.MSCorEE;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// When AOT-compiled, exposes a native entry-point that supports loading the .NET runtime
/// and the Node API <see cref="ManagedHost" />.
/// </summary>
internal unsafe partial class NativeHost : IDisposable
{
    private static readonly string s_managedHostTypeName =
        typeof(NativeHost).Namespace + ".ManagedHost";

    private static JSRuntime? s_jsRuntime;
    private string? _targetFramework;
    private string? _managedHostPath;
    private ICLRRuntimeHost* _runtimeHost;
    private hostfxr_handle _hostContextHandle;
    private readonly JSValueScope _hostScope;
    private JSReference? _exports;

    public static bool IsTracingEnabled { get; } =
        Environment.GetEnvironmentVariable("NODE_API_TRACE_HOST") == "1";

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

        s_jsRuntime ??= new NodejsRuntime();

        // The native host JSValueScope is not disposed after a successful initialization. It
        // becomes the parent of callback scopes, allowing the JS runtime instance to be inherited.
        JSValueScope hostScope = new(JSValueScopeType.NoContext, env, s_jsRuntime);
        try
        {
            NativeHost host = new(hostScope);

            // Do not use JSModuleBuilder here because it relies on having a current context.
            // But the context will be set by the managed host.
            new JSValue(exports, hostScope).DefineProperties(
                // The package index.js will invoke the initialize method with the path to
                // the managed host assembly.
                JSPropertyDescriptor.Function("initialize", host.InitializeManagedHost));
        }
        catch (Exception ex)
        {
            string message = $"Failed to load CLR native host module: {ex}";
            Trace(message);
            s_jsRuntime.Throw(env, (napi_value)JSValue.CreateError(null, (JSValue)message));
            hostScope.Dispose();
        }

        Trace("< NativeHost.InitializeModule()");

        return exports;
    }

    private NativeHost(JSValueScope hostScope)
    {
        _hostScope = hostScope;
    }

    /// <summary>
    /// Receives host initialization parameters from JavaScript and loads the .NET
    /// runtime and managed host.
    /// </summary>
    /// <returns>JS exports value from the managed host.</returns>
    private JSValue InitializeManagedHost(JSCallbackArgs args)
    {
        string targetFramework = (string)args[0];
        string managedHostPath = (string)args[1];

        if (_hostContextHandle != default || _runtimeHost is not null)
        {
            // .NET is already loaded for this host.
            if (targetFramework == _targetFramework && managedHostPath == _managedHostPath &&
                _exports is not null)
            {
                // The same version of .NET and same managed host were requested again.
                // Just return the same exports object that was initialized the first time.
                // Normally this shouldn't happen because the host package initialization
                // script would only be loaded once by require(). But certain situations like
                // drive letter or path casing inconsistencies can cause it to be loaded twice.
                return _exports.GetValue()!.Value;
            }
            else
            {
                throw new NotSupportedException(
                    $".NET ({_targetFramework}) is already initialized in the current process. " +
                    "Initializing multiple .NET versions is not supported.");
            }
        }

        JSValue require = args[2];
        Trace($"> NativeHost.InitializeManagedHost({targetFramework}, {managedHostPath})");

        try
        {
            JSValue exports;
            if (!targetFramework.Contains('.') && targetFramework.StartsWith("net") &&
                targetFramework.Length >= 5)
            {
                // .NET Framework
                Version frameworkVersion = new(
                    int.Parse(targetFramework.Substring(3, 1)),
                    int.Parse(targetFramework.Substring(4, 1)),
                    targetFramework.Length == 5 ? 0 :
                        int.Parse(targetFramework.Substring(5, 1)));
                exports = InitializeFrameworkHost(frameworkVersion, managedHostPath, require);
            }
            else
            {
                // .NET 5 or later
#if NETFRAMEWORK
                Version dotnetVersion = Version.Parse(targetFramework.Substring(3));
#else
                Version dotnetVersion = Version.Parse(targetFramework.AsSpan(3));
#endif
                exports = InitializeDotNetHost(dotnetVersion, managedHostPath, require);
            }

            // Save init parameters and result in case of re-init.
            _targetFramework = targetFramework;
            _managedHostPath = managedHostPath;
            _exports = new JSReference(exports);
            return exports;
        }
        catch (Exception ex)
        {
            Trace("Failed to initialize managed host: " + ex);
            throw;
        }
        finally
        {
            Trace("< NativeHost.InitializeManagedHost()");
        }
    }

    /// <summary>
    /// Initializes the .NET Framework 4.x runtime using MSCOREE.
    /// </summary>
    /// <param name="minVersion">Minimum requested .NET version.</param>
    /// <param name="managedHostPath">Path to the managed host assembly file.</param>
    /// <param name="require">Require function passed in by the init script.</param>
    /// <returns>JS exports value from the managed host.</returns>
    private JSValue InitializeFrameworkHost(
        Version minVersion,
        string managedHostPath,
        JSValue require)
    {
        Trace("    Initializing .NET Framework " + minVersion);

        ICLRMetaHostPolicy* hostPolicy = CLRCreateInstance<ICLRMetaHostPolicy>(
            CLSID_CLRMetaHostPolicy, IID_ICLRMetaHostPolicy);
        Trace("    Created CLR meta host policy.");

        ICLRRuntimeInfo* runtimeInfo = null;
        try
        {
            CLRMetaHostPolicyFlags policyFlags = CLRMetaHostPolicyFlags.ApplyUpgradePolicy;
            runtimeInfo = hostPolicy->GetRequestedRuntime(
                policyFlags, managedHostPath, out string runtimeVersion);
            Trace("    Runtime version: " + runtimeVersion);

            _runtimeHost = runtimeInfo->GetInterface<ICLRRuntimeHost>(
                CLSID_CLRRuntimeHost, IID_ICLRRuntimeHost);
            Trace("    Created runtime host.");

            _runtimeHost->Start();
            Trace("    Started runtime.");

            // Create an "exports" object for the managed host module initialization.
            JSValue exportsValue = JSValue.CreateObject();
            exportsValue.SetProperty("require", require);

            napi_env env = (napi_env)exportsValue.Scope;
            napi_value exports = (napi_value)exportsValue;

            // The method to be executed must take a single string argument and return a uint.
            // So, encode the parameters and retval pointer in the argument string.
            string argument = $"{(ulong)env.Handle:X8},{(ulong)exports.Handle:X8},{(ulong)&exports:X8}";
            Trace($"    Calling {s_managedHostTypeName}.{nameof(InitializeModule)}({argument})");

            _runtimeHost->ExecuteInDefaultAppDomain(
                managedHostPath,
                s_managedHostTypeName,
                nameof(InitializeModule),
                argument);

            exportsValue = exports;
            return exportsValue;
        }
        catch (Exception)
        {
            if (_runtimeHost is not null)
            {
                _runtimeHost->Release();
                _runtimeHost = null;
            }
            throw;
        }
        finally
        {
            if (runtimeInfo != null) runtimeInfo->Release();
        }
    }

    /// <summary>
    /// Initializes the .NET runtime using HostFxr.
    /// </summary>
    /// <param name="targetVersion">Requested .NET version.</param>
    /// <param name="managedHostPath">Path to the managed host assembly file.</param>
    /// <param name="require">Require function passed in by the init script.</param>
    /// <returns>JS exports value from the managed host.</returns>
    private JSValue InitializeDotNetHost(
        Version targetVersion,
        string managedHostPath,
        JSValue require)
    {
        Trace("    Initializing .NET " + targetVersion);

        string managedHostAssemblyName = Path.GetFileNameWithoutExtension(managedHostPath);
        string nodeApiAssemblyName = managedHostAssemblyName.Substring(
            0, managedHostAssemblyName.LastIndexOf('.'));

        string runtimeConfigPath = Path.Join(
            Path.GetDirectoryName(managedHostPath), nodeApiAssemblyName + ".runtimeconfig.json");
        _hostContextHandle = InitializeManagedRuntime(targetVersion, runtimeConfigPath);

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
                &initializeModulePointer);
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
        exports.SetProperty("require", require);

        // Define a dispose method implemented by the native host that closes the CLR context.
        // The managed host proxy will pass through dispose calls to this callback.
        exports.DefineProperties(new JSPropertyDescriptor(
            "dispose", (_) => { Dispose(); return default; }));

        exports = initializeModule((napi_env)exports.Scope, (napi_value)exports);
        return exports;
    }

    private hostfxr_handle InitializeManagedRuntime(
        Version targetVersion,
        string runtimeConfigPath)
    {
        Trace($"> NativeHost.InitializeManagedRuntime({runtimeConfigPath})");

        // Load the library that provides CLR hosting APIs.
        HostFxr.Initialize(targetVersion, allowPrerelease: true);

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

    public void Dispose()
    {
        // Close the CLR host context handle, if it's still open.
        if (_hostContextHandle != default)
        {
            hostfxr_status status = hostfxr_close(_hostContextHandle);
            _hostContextHandle = default;
            CheckStatus(status, "Failed to dispose CLR host.");
        }

        // Release the .NET Framework runtime host object, if it is held.
        if (_runtimeHost is not null)
        {
            _runtimeHost->Release();
            _runtimeHost = null;
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

#endif // NETFRAMEWORK
