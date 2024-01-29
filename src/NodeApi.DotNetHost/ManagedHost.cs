// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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


#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Supports loading and invoking managed .NET assemblies in a JavaScript process.
/// </summary>
public sealed class ManagedHost : JSEventEmitter, IDisposable
{
    private const string ResolvingEventName = "resolving";

#if !NETFRAMEWORK
    /// <summary>
    /// Each instance of a managed host uses a separate assembly load context.
    /// That way, static data is not shared across multiple host instances.
    /// </summary>
    private readonly AssemblyLoadContext _loadContext = new(name: default);
#endif

    /// <summary>
    /// Path to the assembly currently being loaded, or null when not loading.
    /// </summary>
    /// <remarks>
    /// This is used to automatically load dependency assemblies from the same directory as
    /// the initially loaded assembly, if there was no location provided by a resolve handler.
    /// Note since a .NET host cannot be shared by multiple JS threads (workers), only one
    /// assembly can be loaded at a time.
    /// </remarks>
    private string? _loadingPath;

    private JSValueScope? _rootScope;

    /// <summary>
    /// Strong reference to the JS object that is the exports for this module.
    /// </summary>
    /// <remarks>
    /// The exports object has module APIs such as `require()` and `load()`, along with
    /// top-level .NET namespaces like `System` and `Microsoft`.
    /// </remarks>
    private readonly JSReference _exports;

    /// <summary>
    /// Component that dynamically exports types from loaded assemblies.
    /// </summary>
    private readonly TypeExporter _typeExporter;

    /// <summary>
    /// Mapping from top-level namespace names like `System` and `Microsoft` to
    /// namespace objects that track child namespaces and types in the namespace.
    /// </summary>
    /// <remarks>
    /// This is a tree structure because each namespace object has another dictionary
    /// of child namespaces.
    /// </remarks>
    private readonly Dictionary<string, Namespace> _exportedNamespaces = new();

    /// <summary>
    /// Mapping from .NET types to JS type objects for exported types.
    /// </summary>
    /// <remarks>
    /// Note when dynamically loading an assembly, all the types in the assembly
    /// are 
    /// </remarks>
    private readonly Dictionary<Type, JSReference> _exportedTypes = new();

    /// <summary>
    /// Mapping from assembly file paths to loaded assemblies.
    /// </summary>
    private readonly Dictionary<string, Assembly> _loadedAssembliesByPath = new();

    /// <summary>
    /// Mapping from assembly names (not including version or other parts) to
    /// loaded assemblies.
    /// </summary>
    private readonly Dictionary<string, Assembly> _loadedAssembliesByName = new();

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
#if NETFRAMEWORK
        AppDomain.CurrentDomain.AssemblyResolve += OnResolvingAssembly;
#else
        _loadContext.Resolving += OnResolvingAssembly;

        // It shouldn't be necessary to handle resolve events in the default load context.
        // But TypeBuilder (used by JSInterfaceMarshaller) seems to require it when a nuget
        // package referenced type is replaced with a system type, as with IAsyncEnumerable.
        AssemblyLoadContext.Default.Resolving += OnResolvingAssembly;
#endif

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
            JSPropertyDescriptor.Accessor("runtimeVersion", ManagedHost.GetRuntimeVersion),
            JSPropertyDescriptor.Accessor("frameworkMoniker", ManagedHost.GetFrameworkMoniker),

            // The require() method loads a .NET assembly that was built to be a Node API module.
            // It uses static binding to the APIs the module specifically exports to JS.
            JSPropertyDescriptor.Function("require", LoadModule),

            // The load() method loads any .NET assembly and enables dynamic invocation of any APIs.
            JSPropertyDescriptor.Function("load", LoadAssembly),

            JSPropertyDescriptor.Function("addListener", addListener),
            JSPropertyDescriptor.Function("removeListener", removeListener));

        // Create a marshaller instance for the current thread. The marshaller dynamically
        // generates adapter delegates for calls to and from JS, for assemblies that were not
        // pre-built as Node API modules.
        JSMarshaller.Current = new()
        {
            // Currently dynamic invocation does not use automatic camel-casing.
            // However source-generated marshalling (for a .NET node module) does.
            AutoCamelCase = false,
        };

        // Save the exports object, on which top-level namespaces will be defined.
        _exports = new JSReference(exports);

        _typeExporter = new(_exportedTypes);

        // Export the System.Runtime and System.Console assemblies by default.
        LoadAssemblyTypes(typeof(object).Assembly);
        _loadedAssembliesByName.Add(
            typeof(object).Assembly.GetName().Name!, typeof(object).Assembly);

        if (typeof(Console).Assembly != typeof(object).Assembly)
        {
            LoadAssemblyTypes(typeof(Console).Assembly);
            _loadedAssembliesByName.Add(
                typeof(Console).Assembly.GetName().Name!, typeof(Console).Assembly);
        }
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

    /// <summary>
    /// Called by the native host to initialize the managed host module.
    /// Initializes an instance of the managed host and returns the exports object from it.
    /// </summary>
#if NETFRAMEWORK
    public static unsafe int InitializeModule(string argument)
    {
        Trace($"> ManagedHost.InitializeModule({argument})");

        // MSCOREE only supports passing a string argument to the activated assembly,
        // so handle values need to be parsed from the string.
        string[] args = argument.Split(',');
        napi_env env = new((nint)ulong.Parse(args[0], NumberStyles.HexNumber));
        napi_value exports = new((nint)ulong.Parse(args[1], NumberStyles.HexNumber));
        napi_value* pResult = (napi_value*)(nint)ulong.Parse(args[2], NumberStyles.HexNumber);
#else
    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static napi_value InitializeModule(napi_env env, napi_value exports)
    {
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

        JSValueScope scope = new(JSValueScopeType.Root, env, runtime);

        try
        {
            JSObject exportsObject = (JSObject)new JSValue(exports, scope);

            // Save the require() function that was passed in by the init script.
            JSValue require = exportsObject["require"];
            if (require.IsFunction())
            {
                JSRuntimeContext.Current.Require = require;
            }

            ManagedHost host = new(exportsObject)
            {
                _rootScope = scope
            };

            Trace("< ManagedHost.InitializeModule()");
        }
        catch (Exception ex)
        {
            Trace($"Failed to load CLR managed host module: {ex}");
            JSError.ThrowError(ex);
        }

#if NETFRAMEWORK
        *pResult = exports;
        return 0;
#else
        return exports;
#endif
    }

    /// <summary>
    /// Resolve references to Node API and other assemblies that loaded assemblies depend on.
    /// </summary>
    private Assembly? OnResolvingAssembly(
#if NETFRAMEWORK
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

        Trace($"    Resolving assembly: {assemblyName} {assemblyVersion}");
        Emit(ResolvingEventName, assemblyName, assemblyVersion!);

        // Resolve listeners may call load(assemblyFilePath) to load the requested assembly.
        // The version of the loaded assembly might not match the requested version.
        if (_loadedAssembliesByName.TryGetValue(assemblyName, out Assembly? assembly))
        {
            Trace($"        Resolved at: {assembly.Location}");
            return assembly;
        }

        if (!string.IsNullOrEmpty(_loadingPath))
        {
            // The dependency assembly was not resolved by an event-handler.
            // Look for it in the same directory as the initially loaded assembly.
            string adjacentPath = Path.Combine(
                Path.GetDirectoryName(_loadingPath) ?? string.Empty,
                assemblyName + ".dll");
            try
            {
                assembly = LoadAssembly(adjacentPath);
            }
            catch (FileNotFoundException)
            {
                Trace($"    Assembly not found at: {adjacentPath}");
                return default;
            }

            Trace($"        Resolved at: {assembly.Location}");
            return assembly;
        }

        Trace($"    Assembly not resolved: {assemblyName}");
        return default;
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
            return exportsRef.GetValue()!.Value;
        }

        Assembly assembly;
        string? previousLoadingPath = _loadingPath;
        try
        {
            _loadingPath = assemblyFilePath;

#if NETFRAMEWORK
            // TODO: Load module assemblies in separate appdomains.
            assembly = Assembly.LoadFrom(assemblyFilePath);
#else
            assembly = _loadContext.LoadFromAssemblyPath(assemblyFilePath);
#endif
        }
        finally
        {
            _loadingPath = previousLoadingPath;
        }

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

        if (!_loadedAssembliesByPath.ContainsKey(assemblyNameOrFilePath) &&
            !_loadedAssembliesByName.ContainsKey(assemblyNameOrFilePath))
        {
            LoadAssembly(assemblyNameOrFilePath, allowNativeLibrary: true);
        }

        return default;
    }

    private Assembly LoadAssembly(string assemblyNameOrFilePath, bool allowNativeLibrary = false)
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

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Also support loading Windows-specific system assemblies.
                string assemblyFilePath2 = assemblyFilePath.Replace(
                    "Microsoft.NETCore.App", "Microsoft.WindowsDesktop.App");
                if (File.Exists(assemblyFilePath2))
                {
                    assemblyFilePath = assemblyFilePath2;
                }
            }
        }
        else if (!Path.IsPathRooted(assemblyFilePath))
        {
            throw new ArgumentException(
                "Assembly argument must be either an absolute path to an assembly DLL file " +
                "or the name of a system assembly (without path or DLL extension).");
        }

        Assembly assembly;
        string? previousLoadingPath = _loadingPath;
        try
        {
            _loadingPath = assemblyFilePath;

#if NETFRAMEWORK
            // TODO: Load assemblies in a separate appdomain.
            assembly = Assembly.LoadFrom(assemblyFilePath);
#else
            assembly = _loadContext.LoadFromAssemblyPath(assemblyFilePath);
#endif

            LoadAssemblyTypes(assembly);
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

            Trace("< ManagedHost.LoadAssembly() => loaded native library");
            return null!;
        }
        finally
        {
            _loadingPath = previousLoadingPath;
        }

        _loadedAssembliesByPath.Add(assemblyFilePath, assembly);
        _loadedAssembliesByName.Add(assembly.GetName().Name!, assembly);

        Trace("< ManagedHost.LoadAssembly() => newly loaded");
        return assembly;
    }

    private void LoadAssemblyTypes(Assembly assembly)
    {
        Trace($"> ManagedHost.LoadAssemblyTypes({assembly.GetName().Name})");
        int count = 0;

        foreach (Type type in assembly.GetTypes())
        {
            if (!type.IsPublic)
            {
                // This also skips nested types which are NestedPublic but not Public.
                continue;
            }

            string[] namespaceParts = type.Namespace?.Split('.') ?? [];
            if (namespaceParts.Length == 0)
            {
                Trace($"    Skipping un-namespaced type: {type.Name}");
                continue;
            }

            // Delay-loading is enabled by default, but can be disabled with this env variable.
            bool deferMembers = Environment.GetEnvironmentVariable("NODE_API_DELAYLOAD") != "0";

            if (!_exportedNamespaces.TryGetValue(namespaceParts[0], out Namespace? parentNamespace))
            {
                parentNamespace = new Namespace(
                    namespaceParts[0], (type) => _typeExporter.TryExportType(type, deferMembers));
                _exports.GetValue()!.Value.SetProperty(namespaceParts[0], parentNamespace.Value);
                _exportedNamespaces.Add(namespaceParts[0], parentNamespace);
            }

            for (int i = 1; i < namespaceParts.Length; i++)
            {
                if (!parentNamespace.Namespaces.TryGetValue(
                    namespaceParts[i], out Namespace? childNamespace))
                {
                    childNamespace = new Namespace(
                        parentNamespace.Name + '.' + namespaceParts[i],
                        (type) => _typeExporter.TryExportType(type, deferMembers));
                    parentNamespace.Namespaces.Add(namespaceParts[i], childNamespace);
                }

                parentNamespace = childNamespace;
            }

            string typeName = type.Name;
            if (type.IsGenericTypeDefinition)
            {
#if NETFRAMEWORK
                typeName = typeName.Substring(0, typeName.IndexOf('`')) + '$';
#else
                typeName = string.Concat(typeName.AsSpan(0, typeName.IndexOf('`')), "$");
#endif
                if (!parentNamespace.Types.ContainsKey(typeName))
                {
                    // Multiple generic types may have the same name but with
                    // different numbers of type args. They are only exported once.
                    parentNamespace.Types.Add(typeName, type);
                    Trace($"    {parentNamespace}.{typeName}");
                    count++;
                }
            }
            else
            {
                parentNamespace.Types.Add(typeName, type);
                Trace($"    {parentNamespace}.{typeName}");
                count++;
            }
        }

        Trace($"< ManagedHost.LoadAssemblyTypes({assembly.GetName().Name}) => {count} types");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _rootScope?.Dispose();
            _rootScope = null;

#if NETFRAMEWORK
            AppDomain.CurrentDomain.AssemblyResolve -= OnResolvingAssembly;
#else
            AssemblyLoadContext.Default.Resolving -= OnResolvingAssembly;
            _loadContext.Resolving -= OnResolvingAssembly;
            _loadContext.Unload();
#endif
        }

        base.Dispose(disposing);
    }

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
