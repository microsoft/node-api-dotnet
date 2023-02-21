
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi.Hosting;

[RequiresUnreferencedCode("Managed host is not used in trimmed assembly.")]
public class ManagedHost : IDisposable
{
    private ManagedHost()
    {
    }

    /// <summary>
    /// Each instance of a managed host uses a separate assembly load context.
    /// That way, static data is not shared across multiple host instances.
    /// </summary>
    private readonly AssemblyLoadContext _loadContext = new(name: default);

    private readonly Dictionary<string, JSReference> _loadedModules = new();

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

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static napi_value InitializeModule(napi_env env, napi_value exports)
    {
        Trace($"> ManagedHost.InitializeModule({env.Handle:X8})");

        try
        {
            // Ensure references to this assembly can be resolved when loading other assemblies.
            Assembly nodeApiAssembly = typeof(JSValue).Assembly;
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
                e.Name.Split(',')[0] == nameof(NodeApi) ? nodeApiAssembly : null;

            using var scope = new JSValueScope(JSValueScopeType.Root, env);
            var exportsValue = new JSValue(exports, scope);
            new JSModuleBuilder<ManagedHost>()
                .AddMethod("require", (host) => host.LoadModule)
                .AddMethod("loadAssembly", (host) => LoadAssembly)
                .ExportModule(new ManagedHost(), (JSObject)exportsValue);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load CLR managed host module: {ex}");
        }

        Trace("< ManagedHost.InitializeModule()");

        return exports;
    }

    public JSValue LoadModule(JSCallbackArgs args)
    {
        string assemblyFilePath = (string)args[0];

        if (_loadedModules.TryGetValue(assemblyFilePath, out JSReference? exportsRef))
        {
            return exportsRef.GetValue()!.Value;
        }

        Trace($"> ManagedHost.LoadModule({assemblyFilePath})");

        Assembly assembly = _loadContext.LoadFromAssemblyPath(assemblyFilePath);

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

        Trace("< ManagedHost.LoadModule()");

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

    public static JSValue LoadAssembly(JSCallbackArgs args)
    {
        // TODO: This can be used to load an arbitrary .NET assembly that isn't designed specially
        // as a JS module. Then additional methods on the returned JS object can be used by JS code
        // to "reflect" on the loaded assembly and invoke members.
        return default;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
