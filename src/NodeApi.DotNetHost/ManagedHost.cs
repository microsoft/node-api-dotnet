
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Supports loading and invoking managed .NET assemblies in a JavaScript process.
/// </summary>
public sealed class ManagedHost : IDisposable
{
    /// <summary>
    /// Each instance of a managed host uses a separate assembly load context.
    /// That way, static data is not shared across multiple host instances.
    /// </summary>
    private readonly AssemblyLoadContext _loadContext = new(name: default);

    /// <summary>
    /// The marshaler dynamically generates adapter delegates for calls to & from JS,
    /// for assemblies that were not pre-built as Node API modules.
    /// </summary>
    private readonly JSMarshaler _marshaler = new();

    private readonly Dictionary<string, JSReference> _loadedModules = new();
    private readonly Dictionary<string, AssemblyExporter> _loadedAssemblies = new();
    private readonly AssemblyExporter _systemAssembly;

    private ManagedHost(JSObject exports)
    {
        _loadContext.Resolving += OnResolvingAssembly;

        exports.DefineProperties(
            // The require() method loads a .NET assembly that was built to be a Node API module.
            // It uses static binding to the APIs the module specifically exports to JS.
            JSPropertyDescriptor.ForValue("require", JSValue.CreateFunction("require", LoadModule)),

            // The load() method loads any .NET assembly and enables dynamic invocation of any APIs.
            JSPropertyDescriptor.ForValue("load", JSValue.CreateFunction("load", LoadAssembly)));

        // Export the .NET core library assembly by default, along with additional methods above.
        _systemAssembly = new AssemblyExporter(typeof(object).Assembly, _marshaler, exports);
    }

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

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static napi_value InitializeModule(napi_env env, napi_value exports)
    {
        Trace($"> ManagedHost.InitializeModule({env.Handle:X8})");

#if DEBUG
        if (Environment.GetEnvironmentVariable("DEBUG_NODE_API_RUNTIME") != null)
        {
            System.Diagnostics.Debugger.Launch();
        }
#endif

        using JSValueScope scope = new(JSValueScopeType.Root, env);

        try
        {
            ManagedHost host = new((JSObject)new JSValue(exports, scope));
            exports = (napi_value)host._systemAssembly.AssemblyObject;

            Trace("< ManagedHost.InitializeModule()");
            return exports;
        }
        catch (Exception ex)
        {
            Trace($"Failed to load CLR managed host module: {ex}");
            JSError.ThrowError(ex);
            return exports;
        }
    }

    /// <summary>
    /// Ensure references to Node API assemblies can be resolved when loading other
    /// assemblies.
    /// </summary>
    private Assembly? OnResolvingAssembly(
        AssemblyLoadContext loadContext,
        AssemblyName assemblyName)
    {
        if (assemblyName.Name == typeof(JSValue).Assembly.GetName().Name)
        {
            return typeof(JSValue).Assembly;
        }
        else if (assemblyName.Name == typeof(ManagedHost).Assembly.GetName().Name)
        {
            return typeof(ManagedHost).Assembly;
        }

        return null;
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

        Assembly assembly = _loadContext.LoadFromAssemblyPath(assemblyFilePath);

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

        JSValue exports = JSValue.CreateObject();

        var result = (napi_value?)initializeMethod.Invoke(
            null, new object[] { (napi_env)JSValueScope.Current, (napi_value)exports });

        if (result != null && result.Value != default)
        {
            exports = new JSValue(result.Value);
        }

        if (exports.IsObject())
        {
            exportsRef = JSNativeApi.CreateReference(exports);
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
    public JSValue LoadAssembly(JSCallbackArgs args)
    {
        string assemblyFilePath = (string)args[0];
        Trace($"> ManagedHost.LoadAssembly({assemblyFilePath})");

        if (_loadedAssemblies.TryGetValue(assemblyFilePath, out AssemblyExporter? assemblyExporter))
        {
            Trace("< ManagedHost.LoadAssembly() => already loaded");
            return assemblyExporter.AssemblyObject;
        }

        Assembly assembly = _loadContext.LoadFromAssemblyPath(assemblyFilePath);
        assemblyExporter = new(assembly, _marshaler, target: new JSObject());
        _loadedAssemblies.Add(assemblyFilePath, assemblyExporter);
        JSValue assemblyValue = assemblyExporter.AssemblyObject;

        Trace("< ManagedHost.LoadAssembly() => newly loaded");
        return assemblyValue;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
