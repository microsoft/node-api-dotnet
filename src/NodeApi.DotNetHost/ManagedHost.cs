// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

#if !NET7_0_OR_GREATER
using NativeLibrary = Microsoft.JavaScript.NodeApi.Runtime.NativeLibrary;
#endif

#if !(NETFRAMEWORK || NETSTANDARD)
using System.Runtime.Loader;
#endif

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Supports loading and invoking managed .NET assemblies in a JavaScript process.
/// </summary>
public sealed class ManagedHost : JSEventEmitter, IDisposable
{
    private const string ResolvingEventName = "resolving";

#if !(NETFRAMEWORK || NETSTANDARD)
    /// <summary>
    /// Each instance of a managed host uses a separate assembly load context, so static data is not
    /// shared across host instances. It is not collectible: JSInterfaceMarshaller emits interface
    /// adapter types with Reflection.Emit, which a collectible load context does not support, so the
    /// context cannot be unloaded at teardown (only its resolve handlers are unsubscribed).
    /// </summary>
    private readonly AssemblyLoadContext _loadContext = new(name: default);
#endif

    private JSRuntimeContext? _context;

    /// <summary>
    /// Component that dynamically exports types from loaded assemblies.
    /// </summary>
    private readonly TypeExporter _typeExporter;

    /// <summary>
    /// Mapping from assembly file paths to loaded assemblies.
    /// </summary>
    private readonly ConcurrentDictionary<string, Assembly?> _loadedAssembliesByPath = new();

    /// <summary>
    /// Mapping from assembly names (not including version or other parts) to
    /// loaded assemblies.
    /// </summary>
    private readonly ConcurrentDictionary<string, Assembly> _loadedAssembliesByName = new();

    /// <summary>
    /// Tracks names of assemblies that have been exported to JS.
    /// </summary>
    private readonly HashSet<string> _exportedAssembliesByName = new();

    /// <summary>
    /// Mapping from assembly file paths to strong references to module exports.
    /// </summary>
    /// <remarks>
    /// Unlike the loaded "assemblies" above, the loaded "modules" do not use
    /// automatic dynamic binding to .NET APIs; instead they include generated or
    /// custom code for exporting specific APIs to JS.
    /// </remarks>
    private readonly Dictionary<string, JSReference> _loadedModules = new();

    /// <summary>
    /// Creates a new instance of a <see cref="ManagedHost" /> that supports loading and
    /// invoking managed .NET assemblies in a JavaScript process.
    /// </summary>
    /// <param name="exports">JS object on which the managed host APIs will be exported.</param>
    public ManagedHost(JSObject exports)
    {
        JSValue addListener(JSCallbackArgs args)
        {
            AddListener(eventName: (string)args[0], listener: args[1]);
            return args.ThisArg;
        }
        JSValue removeListener(JSCallbackArgs args)
        {
            RemoveListener(eventName: (string)args[0], listener: args[1]);
            return args.ThisArg;
        }

        exports.DefineProperties(
            JSPropertyDescriptor.AccessorProperty("runtimeVersion", GetRuntimeVersion),
            JSPropertyDescriptor.AccessorProperty("frameworkMoniker", GetFrameworkMoniker),

            // The require() method loads a .NET assembly that was built to be a Node API module.
            // It uses static binding to the APIs the module specifically exports to JS.
            JSPropertyDescriptor.Function("require", LoadModule),

            // The load() method loads any .NET assembly and enables dynamic invocation of any APIs.
            JSPropertyDescriptor.Function("load", LoadAssembly),

            JSPropertyDescriptor.Function("addListener", addListener),
            JSPropertyDescriptor.Function("removeListener", removeListener),

            JSPropertyDescriptor.Function("runWorker", RunWorker));


        // Create a marshaller instance for the current thread. The marshaller dynamically
        // generates adapter delegates for calls to and from JS, for assemblies that were not
        // pre-built as Node API modules.
        JSMarshaller.Current = new()
        {
            // Currently dynamic invocation does not use automatic camel-casing.
            // However source-generated marshalling (for a .NET node module) does.
            AutoCamelCase = false,
        };

        // The type exporter will define top-level namespace properties on the exports object.
        _typeExporter = new(JSMarshaller.Current, exports)
        {
            // Delay-loading is enabled by default, but can be disabled with this env variable.
            IsDelayLoadEnabled =
                Environment.GetEnvironmentVariable("NODE_API_DELAYLOAD") != "0"
        };

        // System.Runtime and System.Console assembly types are auto-exported on first use.
        _exportedAssembliesByName.Add(typeof(object).Assembly.GetName().Name!);
        if (typeof(Console).Assembly != typeof(object).Assembly)
        {
            _exportedAssembliesByName.Add(typeof(Console).Assembly.GetName().Name!);
        }

        // Subscribe the process-wide resolve handlers last, after all fallible construction: a
        // constructor that throws is never registered for disposal, so leaving them subscribed
        // would root the failed host.
#if NETFRAMEWORK || NETSTANDARD
        AppDomain.CurrentDomain.AssemblyResolve += OnResolvingAssembly;
#else
        _loadContext.Resolving += OnResolvingAssembly;

        // It shouldn't be necessary to handle resolve events in the default load context.
        // But TypeBuilder (used by JSInterfaceMarshaller) seems to require it when a nuget
        // package referenced type is replaced with a system type, as with IAsyncEnumerable.
        AssemblyLoadContext.Default.Resolving += OnResolvingAssembly;
#endif
    }

    public static bool IsTracingEnabled { get; } =
        Debugger.IsAttached ||
        Environment.GetEnvironmentVariable("NODE_API_TRACE_HOST") == "1";

    public static void Trace(string msg)
    {
        if (IsTracingEnabled)
        {
            Debug.WriteLine(msg);
            Console.WriteLine(msg);
            Console.Out.Flush();
        }
    }

    [Conditional("DEBUG")]
    public static void TraceDebug(string msg) => Trace(msg);

    /// <summary>
    /// Called by the native host to initialize the managed host module.
    /// Initializes an instance of the managed host and returns the exports object from it.
    /// </summary>
#if NETFRAMEWORK || NETSTANDARD
    public static unsafe int InitializeModule(string argument)
    {
        Trace($"> ManagedHost.InitializeModule({argument})");

        // MSCOREE only supports passing a string argument to the activated assembly,
        // so handle values need to be parsed from the string.
        string[] args = argument.Split(',');
        napi_env env = new((nint)ulong.Parse(args[0], NumberStyles.HexNumber));
        napi_value exports = new((nint)ulong.Parse(args[1], NumberStyles.HexNumber));
        napi_value* pResult = (napi_value*)(nint)ulong.Parse(args[2], NumberStyles.HexNumber);
        ManagedHostRegistration* registration = args.Length > 3 ?
            (ManagedHostRegistration*)(nint)ulong.Parse(args[3], NumberStyles.HexNumber) : null;
#else
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static unsafe napi_value InitializeModule(
        napi_env env, napi_value exports, nint registrationPtr)
    {
        ManagedHostRegistration* registration = (ManagedHostRegistration*)registrationPtr;
        Trace($"> ManagedHost.InitializeModule({env.Handle:X8})");
        Trace($"    .NET Runtime version: {Environment.Version}");
#endif

        DebugHelper.AttachDebugger("NODE_API_DEBUG_RUNTIME");

        JSRuntime runtime = new NodejsRuntime();

        if (Debugger.IsAttached ||
            Environment.GetEnvironmentVariable("NODE_API_TRACE_RUNTIME") != null)
        {
            TraceSource trace = new(typeof(JSValue).Namespace!);
            trace.Switch.Level = SourceLevels.All;
            trace.Listeners.Add(new JSConsoleTraceListener());
            runtime = new TracingJSRuntime(runtime, trace);
        }

        // The managed host registers its context in the environment instance-data block (at the
        // module slot). When hosted, the native host owns that block and its finalizer signals
        // environment teardown, so the managed context is a non-owner: it writes its own slot but
        // does not claim the finalizer, and is disposed via the registration notification below.
        bool hosted = registration != null;
        JSRuntimeContext? context = null;

        try
        {
            // Context creation (fallible instance-data registration) and scope creation are inside
            // the try so a failure returns a JS error instead of escaping this unmanaged entry point.
            context = new(env, runtime);
            using JSValueScope scope = JSValueScope.CreateRuntimeScope(env, context);

            JSObject exportsObject = (JSObject)new JSValue(exports, scope);

            // Save the require() and import() functions that were passed in by the init script.
            JSValue requireFunction = exportsObject["require"];
            if (requireFunction.IsFunction())
            {
                JSRuntimeContext.Current.RequireFunction = (JSFunction)requireFunction;
            }

            JSValue importFunction = exportsObject["import"];
            if (importFunction.IsFunction())
            {
                JSRuntimeContext.Current.ImportFunction = (JSFunction)importFunction;
            }

            ManagedHost host = new(exportsObject)
            {
                _context = context
            };

            // Dispose the host with its environment: as a disposable annotation on the context, the
            // host's full Dispose (which unsubscribes the process-wide resolve handlers) runs when
            // the context is disposed at environment teardown. Mirrors the native host.
            context.SetDisposableAnnotation(host);

            if (hosted)
            {
                // Root the managed host for the environment lifetime and give the native host a
                // native callback to invoke at teardown (never a JS call -- see OnEnvironmentFinalize).
                registration->AddonGCHandle = (nint)GCHandle.Alloc(host);
#if !(NETFRAMEWORK || NETSTANDARD)
                registration->OnEnvFinalize =
                    (nint)(delegate* unmanaged[Cdecl]<nint, void>)&OnEnvironmentFinalize;
#endif
            }

            Trace("< ManagedHost.InitializeModule()");
        }
        catch (Exception ex)
        {
            Trace($"Failed to load CLR managed host module: {ex}");
            try
            {
                // Throw via the runtime directly: context or scope creation may have failed, and the
                // disposed context below would make a scope-bound JSError's lazy stack getter unusable.
                runtime.ThrowError(env, code: null, ex.ToString());
            }
            finally
            {
                // The module-slot context does not own the instance-data finalizer, so a failed init
                // must dispose it here; tolerate construction not having completed. Swallow a teardown
                // failure -- JSRuntimeContext.Dispose rethrows its first cleanup error, which must not
                // escape this UnmanagedCallersOnly entry point.
                try
                {
                    context?.Dispose();
                }
                catch (Exception disposeError)
                {
                    Trace($"Failed to dispose context after failed module init: {disposeError}");
                }
            }
        }

#if NETFRAMEWORK || NETSTANDARD
        *pResult = exports;
        return 0;
#else
        return exports;
#endif
    }

#if !(NETFRAMEWORK || NETSTANDARD)
    /// <summary>
    /// Called natively by the native host when the environment is being torn down. Runs during
    /// environment finalization where calling into JavaScript is forbidden, so it touches only
    /// managed state.
    /// </summary>
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static void OnEnvironmentFinalize(nint addon) => OnEnvironmentFinalizeCore(addon);
#else
    /// <summary>
    /// Called by the native host (through the default AppDomain) when the environment is being
    /// torn down. Runs during environment finalization where calling into JavaScript is forbidden,
    /// so it touches only managed state.
    /// </summary>
    public static int OnEnvironmentFinalize(string argument)
    {
        OnEnvironmentFinalizeCore((nint)ulong.Parse(argument, NumberStyles.HexNumber));
        return 0;
    }
#endif

    private static void OnEnvironmentFinalizeCore(nint addon)
    {
        if (addon == default)
        {
            return;
        }

        GCHandle handle = GCHandle.FromIntPtr(addon);
        try
        {
            (handle.Target as ManagedHost)?.DisposeOnEnvironmentFinalize();
        }
        catch (Exception ex)
        {
            Trace($"Failed to dispose managed host on environment finalize: {ex}");
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Disposes the managed host in response to environment teardown. No JavaScript may be called
    /// here; disposing the context marks it disposed (so any late cross-thread post becomes a
    /// no-op), disposes the host (a disposable annotation on the context) so its process-wide
    /// resolve handlers are unsubscribed, and frees the context's GC handles. The context's
    /// references are reclaimed by Node as the environment is torn down.
    /// </summary>
    private void DisposeOnEnvironmentFinalize()
    {
        JSRuntimeContext? context = _context;
        _context = null;
        if (context is not null)
        {
            // Defer disposal until any open value scope closes -- the notification can arrive while a
            // managed callback scope is on the stack (managed calling JS calling dispose) -- mirroring
            // the native host's dispose() hook. With no scope open it disposes immediately.
            JSValueScope.DisposeRuntimeContextWhenIdle(context);
        }
    }

    /// <summary>
    /// Resolve references to Node API and other assemblies that loaded assemblies depend on.
    /// </summary>
    private Assembly? OnResolvingAssembly(
#if NETFRAMEWORK || NETSTANDARD
        object sender,
        ResolveEventArgs args)
    {
        AssemblyName assemblyInfo = new(args.Name);
#else
        AssemblyLoadContext loadContext,
        AssemblyName assemblyInfo)
    {
#endif
        string assemblyName = assemblyInfo.Name!;
        string assemblyVersion = assemblyInfo.Version?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(assemblyName))
        {
            return null;
        }

        if (assemblyName == typeof(JSValue).Assembly.GetName().Name)
        {
            return typeof(JSValue).Assembly;
        }
        else if (assemblyName == typeof(ManagedHost).Assembly.GetName().Name)
        {
            return typeof(ManagedHost).Assembly;
        }

        Trace($"> ManagedHost.OnResolvingAssembly({assemblyName}, {assemblyVersion})");

        // Try to load the named assembly from .NET system directories.
        Assembly? assembly;
        try
        {
            assembly = LoadAssembly(assemblyName, allowNativeLibrary: false);
        }
        catch (FileNotFoundException)
        {
            // The assembly was not found in the system directories.
            // Emit a resolving event to allow listeners to load the assembly.
            // Resolve listeners may call load(assemblyFilePath) to load the requested assembly.
            Emit(
                ResolvingEventName,
                assemblyName,
                assemblyVersion!,
                new JSFunction(ResolveAssembly));
            _loadedAssembliesByName.TryGetValue(assemblyName, out assembly);
        }

        if (assembly == null)
        {
            // The dependency assembly was not resolved by an event-handler.
            // Look for it in the same directory as any already-loaded assemblies.
            foreach (string? loadedAssemblyFile in
                _loadedModules.Keys.Concat(_loadedAssembliesByPath.Keys))
            {
                string assemblyDirectory =
                    Path.GetDirectoryName(loadedAssemblyFile) ?? string.Empty;
                if (!string.IsNullOrEmpty(assemblyDirectory))
                {
                    string adjacentPath = Path.Combine(assemblyDirectory, assemblyName + ".dll");
                    try
                    {
                        assembly = LoadAssembly(adjacentPath, allowNativeLibrary: false);
                        break;
                    }
                    catch (FileNotFoundException)
                    {
                        Trace($"  ManagedHost.OnResolvingAssembly(" +
                            $"{assemblyName}) not found at {adjacentPath}");
                    }
                }
            }
        }

        if (assembly != null)
        {
            Trace($"< ManagedHost.OnResolvingAssembly({assemblyName}) => {assembly.Location}");
            return assembly;
        }
        else
        {
            Trace($"< ManagedHost.OnResolvingAssembly({assemblyName}) => not resolved");
            return default;
        }
    }

    public static JSValue GetRuntimeVersion(JSCallbackArgs _)
    {
        return Environment.Version.ToString();
    }

    public static JSValue GetFrameworkMoniker(JSCallbackArgs _)
    {
        Version runtimeVersion = Environment.Version;

        // For .NET 4 the minor version may be higher, but net472 is the only TFM supported.
        string tfm = runtimeVersion.Major == 4 ? "net472" :
            $"net{runtimeVersion.Major}.{runtimeVersion.Minor}";

        return tfm;
    }

    /// <summary>
    /// Loads a .NET assembly that was built to be a Node API module, using static binding to
    /// the APIs the module specifically exports to JS.
    /// </summary>
    public JSValue LoadModule(JSCallbackArgs args)
    {
        string assemblyFilePath = System.IO.Path.GetFullPath((string)args[0]);
        Trace($"> ManagedHost.LoadModule({assemblyFilePath})");

        if (!assemblyFilePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            assemblyFilePath += ".dll";
        }

        if (_loadedModules.TryGetValue(assemblyFilePath, out JSReference? exportsRef))
        {
            Trace("< ManagedHost.LoadModule() => already loaded");
            return exportsRef.GetValue();
        }

        Assembly assembly;

#if NETFRAMEWORK || NETSTANDARD
        // TODO: Load module assemblies in separate appdomains.
        assembly = Assembly.LoadFrom(assemblyFilePath);
#else
        assembly = _loadContext.LoadFromAssemblyPath(assemblyFilePath);
#endif

        MethodInfo? initializeMethod = null;

        // First look for an auto-generated module initializer.
        Type? moduleClass = assembly.GetType(
            typeof(JSValue).Namespace + ".Generated.Module", throwOnError: false);
        if (moduleClass != null && moduleClass.IsClass && moduleClass.IsPublic &&
            moduleClass.IsAbstract && moduleClass.IsSealed)
        {
            initializeMethod = GetInitializeMethod(moduleClass, "Initialize");
        }

        if (initializeMethod == null)
        {
            // A generated module initialize method was not found. Search for a
            // ModuleInitialize method on any public static class in the assembly.
            // (Static classes appear as abstract and sealed via reflection.)
            foreach (TypeInfo? publicStaticClass in assembly.DefinedTypes
                .Where((t) => t.IsClass && t.IsPublic && t.IsAbstract && t.IsSealed))
            {
                initializeMethod = GetInitializeMethod(publicStaticClass, "InitializeModule");
                if (initializeMethod != null)
                {
                    break;
                }
            }

            if (initializeMethod == null)
            {
                throw new Exception(
                    "Failed to load module. A module initialize method was not found.");
            }
        }

        JSValueScope scope = JSValueScope.Current;
        JSValue exports = JSValue.CreateObject();

        var result = (napi_value?)initializeMethod.Invoke(
            null, new object[] { (napi_env)scope, (napi_value)exports });

        if (result != null && result.Value != default)
        {
            exports = new JSValue(result.Value, scope);
        }

        if (exports.IsObject())
        {
            exportsRef = new JSReference(exports);
            _loadedModules.Add(assemblyFilePath, exportsRef);
        }

        Trace("< ManagedHost.LoadModule() => newly loaded");

        return exports;
    }

    private static MethodInfo? GetInitializeMethod(Type moduleClass, string methodName)
    {
        MethodInfo? initializeMethod = moduleClass.GetMethod(
            methodName, BindingFlags.Public | BindingFlags.Static);

        if (initializeMethod != null)
        {
            ParameterInfo[] parameters = initializeMethod.GetParameters();
            if (parameters.Length == 2 &&
                parameters[0].ParameterType == typeof(napi_env) ||
                parameters[1].ParameterType == typeof(napi_value) ||
                initializeMethod.ReturnType == typeof(napi_value))
            {
                return initializeMethod;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads an arbitrary .NET assembly that isn't necessarily designed as a JS module,
    /// enabling dynamic invocation of any APIs in the assembly.
    /// </summary>
    /// <returns>A JS object that represents the loaded assembly; each property of the object
    /// is a public type.</returns>
    /// <remarks>
    /// Also supports loading native libraries, to make them available for assemblies to
    /// resolve using DllImport.
    /// </remarks>
    public JSValue LoadAssembly(JSCallbackArgs args)
    {
        string assemblyNameOrFilePath = (string)args[0];

        if (!_loadedAssembliesByPath.TryGetValue(assemblyNameOrFilePath, out Assembly? assembly) &&
            !_loadedAssembliesByName.TryGetValue(assemblyNameOrFilePath, out assembly))
        {
            assembly = LoadAssembly(assemblyNameOrFilePath, allowNativeLibrary: true);
        }

        if (assembly != null && !_exportedAssembliesByName.Contains(assembly.GetName().Name!))
        {
            _typeExporter.ExportAssemblyTypes(assembly);
            _exportedAssembliesByName.Add(assembly.GetName().Name!);
        }

        return default;
    }

    /// <summary>
    /// Callback from the 'resolving' event which completes the resolve operation by loading an
    /// assembly from a file path specified by the event listener.
    /// </summary>
    private JSValue ResolveAssembly(JSCallbackArgs args)
    {
        string assemblyFilePath = (string)args[0];
        LoadAssembly(assemblyFilePath, allowNativeLibrary: false);
        return default;
    }

    private Assembly? LoadAssembly(string assemblyNameOrFilePath, bool allowNativeLibrary)
    {
        Trace($"> ManagedHost.LoadAssembly({assemblyNameOrFilePath})");

        string assemblyFilePath = assemblyNameOrFilePath;
        if (string.IsNullOrEmpty(Path.GetDirectoryName(assemblyNameOrFilePath)) &&
            !assemblyNameOrFilePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            // Load the system assembly from the .NET directory.
            assemblyFilePath = Path.Combine(
                Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                assemblyFilePath + ".dll");

            // Also support loading ASP.NET system assemblies.
            string assemblyFilePath2 = assemblyFilePath.Replace(
                    "Microsoft.NETCore.App", "Microsoft.AspNetCore.App");
            if (File.Exists(assemblyFilePath2))
            {
                assemblyFilePath = assemblyFilePath2;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Also support loading Windows-specific system assemblies.
                string assemblyFilePath3 = assemblyFilePath.Replace(
                    "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App");
                if (File.Exists(assemblyFilePath3))
                {
                    assemblyFilePath = assemblyFilePath3;
                }
            }
        }
        else if (!Path.IsPathRooted(assemblyFilePath))
        {
            throw new ArgumentException(
                "Assembly argument must be either an absolute path to an assembly DLL file " +
                "or the name of a system assembly (without path or DLL extension).");
        }

        Assembly? assembly = _loadedAssembliesByPath.GetOrAdd(assemblyFilePath, _ =>
        {
            try
            {
#if NETFRAMEWORK || NETSTANDARD
                // TODO: Load assemblies in a separate appdomain.
                return Assembly.LoadFrom(assemblyFilePath);
#else
                return _loadContext.LoadFromAssemblyPath(assemblyFilePath);
#endif
            }
            catch (BadImageFormatException)
            {
                if (!allowNativeLibrary)
                {
                    throw;
                }

                // This might be a native DLL, not a managed assembly.
                // Load the native library, which enables it to be auto-resolved by
                // any later DllImport operations for the same library name.
                NativeLibrary.Load(assemblyFilePath);
                return null;
            }
            catch (FileNotFoundException fnfex)
            {
                throw new FileNotFoundException(
                    $"Assembly file not found: {assemblyNameOrFilePath}", fnfex);
            }
        });

        if (assembly != null)
        {
            _loadedAssembliesByName.GetOrAdd(assembly.GetName().Name!, assembly);
        }

        string version = assembly?.GetName().Version?.ToString() ?? "(native library)";
        Trace($"< ManagedHost.LoadAssembly() => {assemblyFilePath} {version}");
        return assembly;
    }

    private JSValue RunWorker(JSCallbackArgs args)
    {
        nint callbackHandleValue = (nint)args[0].ToBigInteger();
        Trace($"> ManagedHost.RunWorker({callbackHandleValue})");

        GCHandle callbackHandle = GCHandle.FromIntPtr(callbackHandleValue);
        Action callback = (Action)callbackHandle.Target!;
        callbackHandle.Free();

        try
        {
            // Worker data and argv are available to the callback as NodejsWorker static properties.
            callback();
            return JSValue.Undefined;
        }
        finally
        {
            Trace($"< ManagedHost.RunWorker({callbackHandleValue})");
        }
    }

    private bool _isDisposed;

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            if (disposing)
            {
                try
                {
                    // The context disposes this host (a disposable annotation) at teardown, so this
                    // re-entrant dispose is a guarded no-op; on an explicit dispose it runs the
                    // context teardown, which can throw.
                    _context?.Dispose();
                    _context = null;
                }
                finally
                {
                    // Unsubscribe the process-wide resolve handlers even if context disposal threw,
                    // so a torn-down environment's host is not left rooted (a retry no-ops on
                    // _isDisposed).
#if NETFRAMEWORK || NETSTANDARD
                    AppDomain.CurrentDomain.AssemblyResolve -= OnResolvingAssembly;
#else
                    AssemblyLoadContext.Default.Resolving -= OnResolvingAssembly;
                    _loadContext.Resolving -= OnResolvingAssembly;

                    // A non-collectible load context cannot be unloaded; only unload one created collectible.
                    if (_loadContext.IsCollectible)
                    {
                        _loadContext.Unload();
                    }
#endif
                }
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

#if NETSTANDARD

    private class ConsoleTraceListener : TraceListener
    {
        public override void Write(string? message) => Console.Write(message);
        public override void WriteLine(string? message) => Console.WriteLine(message);
    }

#endif

    private class JSConsoleTraceListener : ConsoleTraceListener
    {
        public override void TraceEvent(
            TraceEventCache? eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string? message)
        => TraceEvent(eventCache, source, eventType, id, message, null);

        public override void TraceEvent(
            TraceEventCache? eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string? format,
            params object?[]? args)
        => WriteLine(string.Format(format ?? string.Empty, args ?? []));
    }
}
