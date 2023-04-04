// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;
using Microsoft.JavaScript.NodeApi.Interop;

#if !NETFRAMEWORK
using System.Runtime.Loader;
#endif

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Supports loading and invoking managed .NET assemblies in a JavaScript process.
/// </summary>
public sealed class ManagedHost : IDisposable
{
#if !NETFRAMEWORK
    /// <summary>
    /// Each instance of a managed host uses a separate assembly load context.
    /// That way, static data is not shared across multiple host instances.
    /// </summary>
    private readonly AssemblyLoadContext _loadContext = new(name: default);
#endif

    /// <summary>
    /// The marshaller dynamically generates adapter delegates for calls to & from JS,
    /// for assemblies that were not pre-built as Node API modules.
    /// </summary>
    private readonly JSMarshaller _marshaller = new()
    {
        // Currently dynamic invocation does not use automatic camel-casing.
        // However source-generated marshalling (for a .NET node module) does.
        AutoCamelCase = false,
    };

    private readonly Dictionary<string, JSReference> _loadedModules = new();
    private readonly Dictionary<string, AssemblyExporter> _loadedAssemblies = new();
    private readonly AssemblyExporter _systemAssembly;

    private ManagedHost(JSObject exports)
    {
#if !NETFRAMEWORK
        _loadContext.Resolving += OnResolvingAssembly;
#endif
        exports.DefineProperties(
            // The require() method loads a .NET assembly that was built to be a Node API module.
            // It uses static binding to the APIs the module specifically exports to JS.
            JSPropertyDescriptor.ForValue("require", JSValue.CreateFunction("require", LoadModule)),

            // The load() method loads any .NET assembly and enables dynamic invocation of any APIs.
            JSPropertyDescriptor.ForValue("load", JSValue.CreateFunction("load", LoadAssembly)));

        // Export the .NET core library assembly by default, along with additional methods above.
        _systemAssembly = new AssemblyExporter(typeof(object).Assembly, _marshaller, exports);
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
#endif

#if DEBUG
        if (Environment.GetEnvironmentVariable("DEBUG_NODE_API_RUNTIME") != null)
        {
            System.Diagnostics.Debugger.Launch();
        }
#endif

        using JSValueScope scope = new(JSValueScopeType.Root, env);

        try
        {
            JSObject exportsObject = (JSObject)new JSValue(exports, scope);

            // Save the require() function that was passed in by the init script.
            JSValue require = exportsObject["require"];
            if (require.IsFunction())
            {
                JSRuntimeContext.Current.Require = require;
            }

            ManagedHost host = new(exportsObject);
            exports = (napi_value)host._systemAssembly.AssemblyObject;

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

#if !NETFRAMEWORK
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
#endif

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

#if NETFRAMEWORK
        // TODO: Load module assemblies in separate appdomains.
        Assembly assembly = Assembly.LoadFrom(assemblyFilePath);
#else
        Assembly assembly = _loadContext.LoadFromAssemblyPath(assemblyFilePath);
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

        if (string.IsNullOrEmpty(Path.GetDirectoryName(assemblyFilePath)) &&
            !assemblyFilePath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            // Load the system assembly from the .NET directory.
            assemblyFilePath = Path.Combine(
                Path.GetDirectoryName(typeof(object).Assembly.Location)!,
                assemblyFilePath + ".dll");
        }
        else if (!Path.IsPathRooted(assemblyFilePath))
        {
            throw new ArgumentException(
                "Assembly argument must be either an absolute path to an assembly DLL file " +
                "or the name of a system assembly (without path or DLL extension).");
        }

#if NETFRAMEWORK
        // TODO: Load assemblies in a separate appdomain.
        Assembly assembly = Assembly.LoadFrom(assemblyFilePath);
#else
        Assembly assembly = _loadContext.LoadFromAssemblyPath(assemblyFilePath);
#endif
        assemblyExporter = new(assembly, _marshaller, target: new JSObject());
        _loadedAssemblies.Add(assemblyFilePath, assemblyExporter);
        JSValue assemblyValue = assemblyExporter.AssemblyObject;

        Trace("< ManagedHost.LoadAssembly() => newly loaded");
        return assemblyValue;
    }

    public void Dispose()
    {
    }
}
