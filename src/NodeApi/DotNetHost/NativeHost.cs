// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !(NETFRAMEWORK || NETSTANDARD)

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.DotNetHost.HostFxr;
using static Microsoft.JavaScript.NodeApi.DotNetHost.MSCorEE;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// When AOT-compiled, exposes a native entry-point that supports loading the .NET runtime
/// and the Node API managed host.
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
    private JSReference? _exports;

    // Filled in by the managed host during initialization via the registration struct: a GCHandle
    // (owned by the managed runtime) that roots the managed host, and a native callback the native
    // host invokes at environment teardown. Both are default until a managed host is initialized.
    private nint _addonGCHandle;
    private nint _onEnvFinalize;

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

    private static bool s_moduleUnloadPrevented;

    /// <summary>
    /// Pins this native host module in memory so the OS never unloads it.
    /// </summary>
    /// <remarks>
    /// This native host is compiled with NativeAOT, so it embeds a .NET runtime whose
    /// per-thread cleanup is registered with the OS via a <c>pthread_key</c> destructor that
    /// points into this module's own code. Node.js unloads (<c>dlclose</c>) an addon when the
    /// environment that loaded it is torn down. When a <c>worker_threads</c> Worker loads this
    /// module and is then terminated, Node unloads the module while the worker's OS thread is
    /// still alive; the still-registered destructor then points at unmapped memory and the
    /// process crashes with SIGSEGV as the thread exits (glibc <c>__nptl_deallocate_tsd</c>).
    /// Keeping the module mapped for the lifetime of the process keeps that destructor valid.
    /// <para/>
    /// This is scoped to Linux (glibc) and macOS, where the native host can be unloaded before
    /// the worker thread exits; on Windows module/thread teardown does not hit this issue. The
    /// pin is best-effort: any failure is traced but does not block init.
    /// </remarks>
    private static unsafe void PreventModuleUnload()
    {
        if (s_moduleUnloadPrevented)
        {
            return;
        }

        s_moduleUnloadPrevented = true;

        bool isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !isMacOS)
        {
            return;
        }

        try
        {
            // Resolve the file path of this shared library from the address of one of its
            // own functions, then re-open it with RTLD_NODELETE so it is never unmapped.
            nint moduleFunction =
                (nint)(delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value>)
                &InitializeModule;

            nint fileName = default;
            if (DlAddr(moduleFunction, out Dl_info info) != 0)
            {
                fileName = info.dli_fname;
            }

            if (fileName != default)
            {
                // RTLD_NOLOAD resolves the already-loaded module without loading a new copy;
                // RTLD_NODELETE keeps it mapped for the process lifetime. The extra (never
                // released) reference also prevents Node's dlclose from unmapping it.
                const int RTLD_LAZY = 0x0001;
                const int RTLD_NOLOAD_LINUX = 0x0004;
                const int RTLD_NODELETE_LINUX = 0x1000;
                const int RTLD_NOLOAD_MACOS = 0x0010;
                const int RTLD_NODELETE_MACOS = 0x0080;
                int flags = RTLD_LAZY | (isMacOS ?
                    RTLD_NOLOAD_MACOS | RTLD_NODELETE_MACOS :
                    RTLD_NOLOAD_LINUX | RTLD_NODELETE_LINUX);
                nint handle = DlOpen(fileName, flags);
                Trace($"    Pinned native host module ({(handle != default ? "ok" : "no-op")}).");
            }
            else
            {
                Trace("    Could not resolve native host module path to pin it.");
            }
        }
        catch (Exception ex)
        {
            Trace("    Failed to pin native host module: " + ex);
        }
    }

    // dladdr and dlopen are exported by libSystem on macOS. On Linux they are exported by
    // libc.so.6 on glibc >= 2.34, but by libdl.so.2 on older glibc versions.
    private static int DlAddr(nint addr, out Dl_info info)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return DlAddrLibSystem(addr, out info);
        }

        try
        {
            return DlAddrLibc(addr, out info);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return DlAddrLibdl(addr, out info);
        }
    }

    private static nint DlOpen(nint fileName, int flags)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return DlOpenLibSystem(fileName, flags);
        }

        try
        {
            return DlOpenLibc(fileName, flags);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
            return DlOpenLibdl(fileName, flags);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Dl_info
    {
        public nint dli_fname;
        public nint dli_fbase;
        public nint dli_sname;
        public nint dli_saddr;
    }

    [LibraryImport("libc.so.6", EntryPoint = "dladdr")]
    private static partial int DlAddrLibc(nint addr, out Dl_info info);

    [LibraryImport("libdl.so.2", EntryPoint = "dladdr")]
    private static partial int DlAddrLibdl(nint addr, out Dl_info info);

    [LibraryImport("libc.so.6", EntryPoint = "dlopen")]
    private static partial nint DlOpenLibc(nint filename, int flags);

    [LibraryImport("libdl.so.2", EntryPoint = "dlopen")]
    private static partial nint DlOpenLibdl(nint filename, int flags);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dladdr")]
    private static partial int DlAddrLibSystem(nint addr, out Dl_info info);

    [LibraryImport("/usr/lib/libSystem.B.dylib", EntryPoint = "dlopen")]
    private static partial nint DlOpenLibSystem(nint filename, int flags);

    [UnmanagedCallersOnly(
        EntryPoint = nameof(napi_register_module_v1),
        CallConvs = new[] { typeof(CallConvCdecl) })]
    public static napi_value InitializeModule(napi_env env, napi_value exports)
    {
        Trace($"> NativeHost.InitializeModule({env.Handle:X8}, {exports.Handle:X8})");

        // Ensure this native module stays loaded for the lifetime of the process. See
        // PreventModuleUnload() for details on the worker-thread teardown crash this avoids.
        PreventModuleUnload();

        s_jsRuntime ??= new NodejsRuntime();

        // The native host's context occupies the host instance-data slot, so the initialize()/
        // dispose() callbacks (dispatched later with no parent scope) recover it via FromEnv.
        JSRuntimeContext.UseHostContextSlot();

        try
        {
            // Context creation (fallible instance-data registration) and scope creation are inside
            // the try so a failure returns a JS error instead of escaping this unmanaged entry point.
            // The context outlives the scope -- rooted by its instance-data slot, disposed by that
            // slot's finalizer (which disposes the NativeHost).
            JSRuntimeContext context = new(env, s_jsRuntime, new JSInlineSynchronizationContext());
            using JSValueScope hostScope = JSValueScope.CreateRuntimeScope(env, context);

            NativeHost host = new();
            context.SetDisposableAnnotation(host);

            new JSValue(exports, hostScope).DefineProperties(
                // The package index.js will invoke the initialize method with the path to
                // the managed host assembly.
                JSPropertyDescriptor.Function("initialize", host.InitializeManagedHost));
        }
        catch (Exception ex)
        {
            string message = $"Failed to load CLR native host module: {ex}";
            Trace(message);
            // Scope-less throw: context or scope creation may have failed, so no scope exists.
            // Not disposed here: the host-slot context owns the instance-data finalizer that
            // disposes it at env teardown even if partly initialized -- unlike the managed host's
            // module-slot context, whose failure path must dispose it.
            s_jsRuntime.ThrowError(env, code: null, message);
        }

        Trace("< NativeHost.InitializeModule()");

        return exports;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ManagedHostRegistration
    {
        public nint AddonGCHandle;
        public nint OnEnvFinalize;
    }

    private void NotifyManagedHostEnvironmentFinalize()
    {
        if (_onEnvFinalize != default)
        {
            // hostfxr (.NET 5+): the managed host provided a native callback pointer.
            ((delegate* unmanaged[Cdecl]<nint, void>)_onEnvFinalize)(_addonGCHandle);
        }
        else if (_runtimeHost is not null && _addonGCHandle != default && _managedHostPath is not null)
        {
            // .NET Framework: invoke the managed finalize through the default AppDomain. This is a
            // native call into the (still-loaded) CLR, never a JavaScript call.
            try
            {
                _runtimeHost->ExecuteInDefaultAppDomain(
                    _managedHostPath,
                    s_managedHostTypeName,
                    "OnEnvironmentFinalize",
                    ((ulong)_addonGCHandle).ToString("X8"));
            }
            catch (Exception ex)
            {
                Trace("Failed to notify managed host on environment finalize: " + ex);
            }
        }
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
                return _exports.GetValue();
            }
            else
            {
                throw new NotSupportedException(
                    $".NET ({_targetFramework}) is already initialized in the current process. " +
                    "Initializing multiple .NET versions is not supported.");
            }
        }

        JSValue require = args[2];
        JSValue import = args[3];
        Trace($"> NativeHost.InitializeManagedHost({targetFramework}, {managedHostPath})");

        try
        {
            JSValue exports;
            if (!targetFramework.Contains('.') &&
                targetFramework.StartsWith("net", StringComparison.Ordinal) &&
                targetFramework.Length >= 5)
            {
                // .NET Framework
                Version frameworkVersion = new(
                    int.Parse(targetFramework.Substring(3, 1)),
                    int.Parse(targetFramework.Substring(4, 1)),
                    targetFramework.Length == 5 ? 0 :
                        int.Parse(targetFramework.Substring(5, 1)));
                exports = InitializeFrameworkHost(
                    frameworkVersion, managedHostPath, require, import);
            }
            else
            {
                // .NET 5 or later
#if NETFRAMEWORK || NETSTANDARD
                Version dotnetVersion = Version.Parse(targetFramework.Substring(3));
#else
                Version dotnetVersion = Version.Parse(targetFramework.AsSpan(3));
#endif
                exports = InitializeDotNetHost(
                    dotnetVersion, managedHostPath, require, import);
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
    /// <param name="import">Import function passed in by the init script.</param>
    /// <returns>JS exports value from the managed host.</returns>
    private JSValue InitializeFrameworkHost(
        Version minVersion,
        string managedHostPath,
        JSValue require,
        JSValue import)
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
            exportsValue.SetProperty("import", import);

            napi_env env = (napi_env)exportsValue.Scope;
            napi_value exports = (napi_value)exportsValue;

            // The method to be executed must take a single string argument and return a uint.
            // So, encode the parameters, retval pointer, and registration pointer in the argument.
            ManagedHostRegistration registration = default;
            string argument = $"{(ulong)env.Handle:X8},{(ulong)exports.Handle:X8}," +
                $"{(ulong)&exports:X8},{(ulong)&registration:X8}";
            Trace($"    Calling {s_managedHostTypeName}.{nameof(InitializeModule)}({argument})");

            _runtimeHost->ExecuteInDefaultAppDomain(
                managedHostPath,
                s_managedHostTypeName,
                nameof(InitializeModule),
                argument);

            _addonGCHandle = registration.AddonGCHandle;
            _onEnvFinalize = registration.OnEnvFinalize;

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
    /// <param name="import">Import function passed in by the init script.</param>
    /// <returns>JS exports value from the managed host.</returns>
    private JSValue InitializeDotNetHost(
        Version targetVersion,
        string managedHostPath,
        JSValue require,
        JSValue import)
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

        // Create an "exports" object for the managed host module initialization.
        var exports = JSValue.CreateObject();
        exports.SetProperty("require", require);
        exports.SetProperty("import", import);

        // Defer disposing the host context until this callback (and any value scope it is nested in)
        // has closed: dispose() is a native call dispatched through Node-API, so disposing the context
        // while a scope is open would leave that scope to close its napi handle scope on a disposed
        // context as it unwinds. Disposing the host context runs the full host teardown via its
        // disposable annotation -- notifying the managed host and closing the runtime-host channel.
        exports.DefineProperties(new JSPropertyDescriptor(
            "dispose", (_) =>
            {
                JSValueScope.DisposeRuntimeContextWhenIdle(JSValueScope.Current.RuntimeContext);
                return default;
            }));

        // Invoke the managed host initialize method. It defines properties on the exports object
        // and fills in the registration so the native host can keep the managed host alive and
        // notify it when the environment is torn down.
        ManagedHostRegistration registration = default;
        var initializeModule =
            (delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_value>)
            initializeModulePointer;
        exports = initializeModule((napi_env)exports.Scope, (napi_value)exports, (nint)(&registration));

        _addonGCHandle = registration.AddonGCHandle;
        _onEnvFinalize = registration.OnEnvFinalize;
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

        CheckStatus(status, "Failed to initialize CLR host.");

        Trace("< NativeHost.InitializeManagedRuntime()");
        return hostContextHandle;
    }

    public void Dispose()
    {
        // Runs as the host context's disposable annotation when that context is disposed: at env
        // teardown by its instance-data finalizer, or (deferred to scope close) by the JS dispose() hook.
        try
        {
            NotifyManagedHostEnvironmentFinalize();
        }
        finally
        {
            // Clear the registration before the fallible CloseRuntimeHost: the notification frees the
            // addon GCHandle, so a later retry that re-invoked it would pass an already-freed handle
            // across the unmanaged finalizer boundary.
            _addonGCHandle = default;
            _onEnvFinalize = default;
        }

        CloseRuntimeHost();

        // Both dispose paths now run while the host context is disposed, so JSReference.Dispose
        // short-circuits and Node reclaims the napi_ref at env teardown; the assignment drops the
        // managed reference so it can be collected.
        _exports?.Dispose();
        _exports = null;
    }

    private void CloseRuntimeHost()
    {
        // Closes this environment's CLR host: the hostfxr context handle (.NET 5+) or the
        // ICLRRuntimeHost COM reference (.NET Framework). Each environment initializes its own, so
        // this is per-environment teardown (the underlying shared CLR is not unloaded). Invoked at
        // environment teardown and by the optional JS dispose() hook; idempotent.

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

#endif
