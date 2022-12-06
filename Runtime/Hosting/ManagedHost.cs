
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi.Hosting;

[RequiresUnreferencedCode("Managed host is not used in trimmed assembly.")]
public class ManagedHost
{
    private Dictionary<string, JSReference> _loadedModules = new();

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static napi_value InitializeModule(napi_env env, napi_value exports)
    {
        ////Console.WriteLine("> ManagedHost.InitializeModule()");

        try
        {
            JSNativeApi.Interop.Initialize();

            // Ensure references to this assembly can be resolved when loading other assemblies.
            var nodeApiAssembly = typeof(JSValue).Assembly;
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
                e.Name.Split(',')[0] == nameof(NodeApi) ? nodeApiAssembly : null;

            using var scope = new JSValueScope(env);
            new JSModuleBuilder<ManagedHost>()
                .AddMethod("require", (host) => host.LoadModule)
                .AddMethod("loadAssembly", (host) => host.LoadAssembly)
                .ExportModule(new JSValue(scope, exports), new ManagedHost());
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load CLR managed host module: {ex}");
        }

        ////Console.WriteLine("< ManagedHost.InitializeModule()");

        return exports;
    }

    public JSValue LoadModule(JSCallbackArgs args)
    {
        string assemblyFilePath = (string)args[0];

        if (_loadedModules.TryGetValue(assemblyFilePath, out JSReference? exportsRef))
        {
            return exportsRef.GetValue()!.Value;
        }

        ////Console.WriteLine($"> ManagedHost.LoadModule({assemblyFilePath})");

        var assembly = Assembly.LoadFile(assemblyFilePath);

        MethodInfo? initializeMethod = null;

        // First look for an auto-generated module initializer.
        Type? moduleClass = assembly.GetType("NodeApi.Generated.Module", throwOnError: false);
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
            foreach (var publicStaticClass in assembly.DefinedTypes
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


        JSValue exports = JSNativeApi.CreateObject();

        using var childScope = new JSSimpleValueScope((napi_env)args.Scope);

        // TODO: Return the module initialize result? Can it be different from the exports object?
        object? result = initializeMethod.Invoke(
            null, new object[] { (napi_env)childScope, (napi_value)exports });

        exportsRef = JSNativeApi.CreateReference(exports);
        _loadedModules.Add(assemblyFilePath, exportsRef);

        ////Console.WriteLine("< ManagedHost.LoadModule()");

        return exports;
    }

    private MethodInfo? GetInitializeMethod(Type moduleClass, string methodName)
    {
        MethodInfo? initializeMethod = moduleClass.GetMethod(
            methodName, BindingFlags.Public | BindingFlags.Static);

        if (initializeMethod != null)
        {
            var parameters = initializeMethod.GetParameters();
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

    public JSValue LoadAssembly(JSCallbackArgs args)
    {
        // TODO: This can be used to load an arbitrary .NET assembly that isn't designed specially
        // as a JS module. Then additional methods on the returned JS object can be used by JS code
        // to "reflect" on the loaded assembly and invoke members.
        return default;
    }
}
