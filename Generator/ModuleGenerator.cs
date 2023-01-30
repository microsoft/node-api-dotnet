using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NodeApi.Generator;

/// <summary>
/// Generates JavaScript module registration code for C# APIs exported to JS.
/// </summary>
[Generator]
public class ModuleGenerator : SourceGenerator, ISourceGenerator
{
    private const string ModuleInitializerClassName = "Module";
    private const string ModuleInitializeMethodName = "Initialize";
    private const string ModuleRegisterFunctionName = "napi_register_module_v1";

    void ISourceGenerator.Initialize(GeneratorInitializationContext context) { }

    public void Execute(GeneratorExecutionContext context)
    {
        Context = context;
#if DEBUG
        // Note source generators are not covered by normal debugging,
        // because the generator runs at build time, not at application run-time.
        // Un-comment the line below to enable debugging at build time.
        ////System.Diagnostics.Debugger.Launch();
#endif

        try
        {
            ISymbol? moduleInitializer = GetModuleInitializer();
            IEnumerable<ISymbol> exportItems = GetModuleExportItems();

            SourceText initializerSource = GenerateModuleInitializer(
                moduleInitializer, exportItems);
            context.AddSource($"{nameof(NodeApi)}.{ModuleInitializerClassName}", initializerSource);

            // Also write the generated code to a file under obj/ for diagnostics.
            // Depends on <CompilerVisibleProperty Include="BaseIntermediateOutputPath" />
            if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(
                "build_property.BaseIntermediateOutputPath", out string? intermediateOutputPath))
            {
                string generatedSourcePath = Path.Combine(
                    intermediateOutputPath,
                    $"{nameof(NodeApi)}.{ModuleInitializerClassName}.cs");
                File.WriteAllText(generatedSourcePath, initializerSource.ToString());
            }
        }
        catch (Exception ex)
        {
            ReportError(DiagnosticId.GeneratorError, null, "Generator failed.", ex.Message);
        }
    }

    private IEnumerable<ITypeSymbol> GetCompilationTypes()
    {
        return Context.Compilation.Assembly.TypeNames
          .SelectMany((n) => Context.Compilation.GetSymbolsWithName(n, SymbolFilter.Type))
          .OfType<ITypeSymbol>();
    }

    private ISymbol? GetModuleInitializer()
    {
        List<ISymbol> moduleInitializers = new();

        foreach (ITypeSymbol type in GetCompilationTypes())
        {
            if (type.GetAttributes().Any((a) => a.AttributeClass?.Name == "JSModuleAttribute"))
            {
                if (type.TypeKind != TypeKind.Class)
                {
                    ReportError(
                        DiagnosticId.InvalidModuleInitializer,
                        type,
                        "[JSModule] attribute must be applied to a class.");
                }
                else if (type.DeclaredAccessibility != Accessibility.Public)
                {
                    ReportError(
                        DiagnosticId.ModuleInitializerIsNotPublic,
                        type,
                        "Module class must have public visibility.");
                }


                moduleInitializers.Add(type);
            }
            else if (type.TypeKind == TypeKind.Class)
            {
                foreach (ISymbol? member in type.GetMembers())
                {
                    if (member.GetAttributes().Any(
                        (a) => a.AttributeClass?.Name == "JSModuleAttribute"))
                    {
                        // TODO: Check method parameter and return types.
                        if (member is not IMethodSymbol)
                        {
                            ReportError(
                                DiagnosticId.InvalidModuleInitializer,
                                member,
                                "[JSModule] attribute must be applied to a method.");
                        }
                        else if (!member.IsStatic)
                        {
                            ReportError(
                                DiagnosticId.ModuleInitializerIsNotStatic,
                                member,
                                "Module initialize method must be static.");
                        }
                        else if (type.DeclaredAccessibility != Accessibility.Public)
                        {
                            ReportError(
                                DiagnosticId.ModuleInitializerIsNotPublic,
                                member,
                                "Containing type of module initialize method must be public.");
                        }
                        else if (member.DeclaredAccessibility != Accessibility.Public)
                        {
                            ReportError(
                                DiagnosticId.ModuleInitializerIsNotPublic,
                                member,
                                "Module initialize method must be public.");
                        }

                        moduleInitializers.Add(member);
                    }
                }
            }
        }

        if (moduleInitializers.Count > 1)
        {
            foreach (ISymbol initializer in moduleInitializers)
            {
                ReportError(
                    DiagnosticId.MultipleModuleAttributes,
                    initializer,
                    "Multiple [JSModule] attributes found.",
                    "Designate a single class or static method to handle module initialization.");
            }

            return null;
        }

        return moduleInitializers.SingleOrDefault();
    }

    private IEnumerable<ISymbol> GetModuleExportItems()
    {
        foreach (ITypeSymbol type in GetCompilationTypes())
        {
            if (type.GetAttributes().Any((a) => a.AttributeClass?.Name == "JSExportAttribute"))
            {
                if (type.TypeKind == TypeKind.Delegate)
                {
                    ReportError(
                        DiagnosticId.UnsupportedTypeKind,
                        type,
                        "Exporting delegates is not currently supported.");
                }
                else if (type.TypeKind == TypeKind.Interface)
                {
                    ReportError(
                        DiagnosticId.UnsupportedTypeKind,
                        type,
                        "Exporting interfaces is not currently supported.");
                }
                else if (type.TypeKind != TypeKind.Class)
                {
                    ReportError(
                        DiagnosticId.UnsupportedTypeKind,
                        type,
                        "Exporting value types is not currently supported.");
                }


                if (type.DeclaredAccessibility != Accessibility.Public)
                {
                    ReportError(
                        DiagnosticId.ExportIsNotPublic,
                        type,
                        "Exported type must be public.");
                }

                yield return type;
            }
            else if (type.TypeKind == TypeKind.Class)
            {
                foreach (ISymbol? member in type.GetMembers())
                {
                    if (member.GetAttributes().Any(
                        (a) => a.AttributeClass?.Name == "JSExportAttribute"))
                    {
                        if (type.DeclaredAccessibility != Accessibility.Public)
                        {
                            ReportError(
                                DiagnosticId.ExportIsNotPublic,
                                member,
                                "Containing type of exported member must be public.");
                        }
                        else if (member.DeclaredAccessibility != Accessibility.Public)
                        {
                            ReportError(
                                DiagnosticId.ExportIsNotPublic,
                                member,
                                "Exported member must be public.");
                        }
                        else if (!(member.IsStatic))
                        {
                            ReportError(
                                DiagnosticId.ExportIsNotStatic,
                                member,
                                "Exported member must be static.");
                        }

                        yield return member;
                    }
                }
            }
        }
    }

    private SourceText GenerateModuleInitializer(
      ISymbol? moduleInitializer,
      IEnumerable<ISymbol> exportItems)
    {
        var s = new SourceBuilder();

        s += "using System.CodeDom.Compiler;";
        s += "using System.Collections.Generic;";
        s += "using System.Runtime.InteropServices;";
        s += "using static NodeApi.JSNativeApi.Interop;";

        s++;
        s += "namespace NodeApi.Generated;";
        s++;

        string generatorName = typeof(ModuleGenerator).Assembly.GetName()!.Name!;
        Version? generatorVersion = typeof(ModuleGenerator).Assembly.GetName().Version;
        s += $"[GeneratedCode(\"{generatorName}\", \"{generatorVersion}\")]";
        s += $"public static class {ModuleInitializerClassName}";
        s += "{";

        s += $"[UnmanagedCallersOnly(EntryPoint = \"{ModuleRegisterFunctionName}\")]";
        s += $"public static napi_value _{ModuleInitializeMethodName}(napi_env env, napi_value exports)";
        s += $"{s.Indent}=> Initialize(env, exports);";
        s += "";
        s += $"public static napi_value {ModuleInitializeMethodName}(napi_env env, napi_value exports)";
        s += "{";
        s += "try";
        s += "{";
        s += "JSNativeApi.Interop.Initialize();";
        s += "";
        s += "using JSValueScope scope = new(env);";
        s += "JSValue exportsValue = new(scope, exports);";
        s++;

        AdapterGenerator adapterGenerator = new(Context);
        if (moduleInitializer is IMethodSymbol moduleInitializerMethod)
        {
            if (exportItems.Any())
            {
                ReportError(
                    DiagnosticId.InvalidModuleInitializer,
                    moduleInitializerMethod,
                    "[JSExport] attributes cannot be used with custom init method.");
            }

            string ns = GetNamespace(moduleInitializerMethod);
            string className = moduleInitializerMethod.ContainingType.Name;
            string methodName = moduleInitializerMethod.Name;
            s += $"return {ns}.{className}.{methodName}((JSObject)exportsValue)";
            s += "\t.GetCheckedHandle();";
        }
        else
        {
            ExportModule(ref s, moduleInitializer as ITypeSymbol, exportItems, adapterGenerator);
            s++;
            s += "return exportsValue.GetCheckedHandle();";
        }

        s += "}";
        s += "catch (System.Exception ex)";
        s += "{";
        s += "System.Console.Error.WriteLine($\"Failed to export module: {ex}\");";
        s += "return exports;";
        s += "}";
        s += "}";

        adapterGenerator.GenerateAdapters(s);

        s += "}";

        return s;
    }

    private static void ExportModule(
      ref SourceBuilder s,
      ITypeSymbol? moduleType,
      IEnumerable<ISymbol> exportItems,
      AdapterGenerator adapterGenerator)
    {
        // TODO: Also generate .d.ts?

        if (moduleType != null)
        {
            string ns = GetNamespace(moduleType);
            s += $"exportsValue = new JSModuleBuilder<{ns}.{moduleType.Name}>()";
            s.IncreaseIndent();

            // Export non-static members of the module class.
            foreach (ISymbol? member in moduleType.GetMembers()
              .Where((m) => m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic))
            {
                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    ExportMethod(ref s, method, adapterGenerator);
                }
                else if (member is IPropertySymbol property)
                {
                    ExportProperty(ref s, property, adapterGenerator);
                }
            }
        }
        else
        {
            s += $"exportsValue = new JSModuleBuilder<System.Object>()";
            s.IncreaseIndent();
        }

        // Export items tagged with [JSExport]
        foreach (ISymbol exportItem in exportItems)
        {
            string exportName = GetExportName(exportItem);
            if (exportItem is ITypeSymbol exportType && exportType.TypeKind == TypeKind.Class)
            {
                s += $".AddProperty(\"{exportName}\",";
                s.IncreaseIndent();

                string ns = GetNamespace(exportType);
                if (exportType.IsStatic)
                {
                    s += $"new JSClassBuilder<object>(\"{exportName}\")";
                }
                else
                {
                    s += $"new JSClassBuilder<{ns}.{exportType.Name}>(\"{exportName}\",";

                    string? constructorAdapterName =
                        adapterGenerator.GetConstructorAdapterName(exportType);
                    if (constructorAdapterName != null)
                    {
                        s += $"\t{constructorAdapterName})";
                    }
                    else if (AdapterGenerator.HasNoArgsConstructor(exportType))
                    {
                        s += $"\t() => new {ns}.{exportType.Name}())";
                    }
                    else
                    {
                        s += $"\t(args) => new {ns}.{exportType.Name}(args))";
                    }
                }

                ExportMembers(ref s, exportType, adapterGenerator);
                s += ".DefineClass())";
                s.DecreaseIndent();
            }
            else if (exportItem is IPropertySymbol exportProperty)
            {
                ExportProperty(ref s, exportProperty, adapterGenerator, exportName);
            }
            else if (exportItem is IMethodSymbol exportMethod)
            {
                ExportMethod(ref s, exportMethod, adapterGenerator, exportName);
            }
        }

        if (moduleType != null)
        {
            string ns = GetNamespace(moduleType);
            s += $".ExportModule((JSObject)exportsValue, new {ns}.{moduleType.Name}());";
        }
        else
        {
            s += $".ExportModule((JSObject)exportsValue, null);";
        }

        s.DecreaseIndent();
    }

    private static void ExportMembers(
      ref SourceBuilder s,
      ITypeSymbol classType,
      AdapterGenerator adapterGenerator)
    {
        // TODO: Also generate .d.ts?

        foreach (ISymbol? member in classType.GetMembers()
          .Where((m) => m.DeclaredAccessibility == Accessibility.Public))
        {
            if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
            {
                ExportMethod(ref s, method, adapterGenerator);
            }
            else if (member is IPropertySymbol property)
            {
                ExportProperty(ref s, property, adapterGenerator);
            }
        }
    }

    private static void ExportMethod(
      ref SourceBuilder s,
      IMethodSymbol method,
      AdapterGenerator adapterGenerator,
      string? exportName = null)
    {
        exportName ??= ToCamelCase(method.Name);

        string? adapterName = adapterGenerator.GetMethodAdapterName(method);
        if (adapterName != null)
        {
            s += $".AddMethod(\"{exportName}\", {adapterName})";
        }
        else if (method.IsStatic)
        {
            string ns = GetNamespace(method);
            string className = method.ContainingType.Name;
            s += $".AddMethod(\"{exportName}\", () => {ns}.{className}.{method.Name})";
        }
        else
        {
            s += $".AddMethod(\"{exportName}\", (obj) => obj.{method.Name})";
        }
    }

    private static void ExportProperty(
      ref SourceBuilder s,
      IPropertySymbol property,
      AdapterGenerator adapterGenerator,
      string? exportName = null)
    {
        exportName ??= ToCamelCase(property.Name);

        (string? getterAdapterName, string? setterAdapterName) =
            adapterGenerator.GetPropertyAdapterNames(property);

        s += $".AddProperty(\"{exportName}\",";
        s.IncreaseIndent();

        string ns = GetNamespace(property);
        string className = property.ContainingType.Name;

        if (getterAdapterName != null)
        {
            s += $"getter: {getterAdapterName},";
        }
        else if (property.GetMethod?.DeclaredAccessibility != Accessibility.Public)
        {
            s += $"getter: () => default,";
        }
        else if (property.IsStatic)
        {
            s += $"getter: () => {ns}.{className}.{property.Name},";
        }
        else
        {
            s += $"getter: (obj) => obj.{property.Name},";
        }

        if (setterAdapterName != null)
        {
            s += $"setter: {setterAdapterName})";
        }
        else if (property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
        {
            s += $"setter: null)";
        }
        else if (property.IsStatic)
        {
            s += $"setter: (value) => {ns}.{className}.{property.Name} = value)";
        }
        else
        {
            s += $"setter: (obj, value) => obj.{property.Name} = value)";
        }

        s.DecreaseIndent();
    }

    private void ValidateExportedMethod(
      IMethodSymbol method)
    {
        // TODO: Marshal other parameter and return types.
        // TODO: Implement correct type matching
        if (!(method.Parameters.Length == 0 ||
          (method.Parameters.Length == 1 &&
            (method.Parameters[0].Type.Name == "JSCallbackArgs" ||
             method.Parameters[0].Type.Name == "JSValue"))))
        {
            ReportError(
              DiagnosticId.UnsupportedMethodParameterType,
              method,
              $"Exported method {method.Name} has unsupported parameters.",
              "Exported methods must have either no parameters or a single parameter of type " +
                  $"{nameof(NodeApi)}.JSCallbackArgs.");
            return;
        }

        if (method.ReturnType.Name != "Void" && method.ReturnType.Name != "JSValue")
        {
            ReportError(
              DiagnosticId.UnsupportedMethodReturnType,
              method,
              $"Exported method {method.Name} has unsupported return type. ",
              $"Exported methods must have return type {nameof(NodeApi)}.JSValue or void.");
            return;
        }
    }

    private void ValidateExportedProperty(
      IPropertySymbol property)
    {
        if (property.Type.Name != "JSValue")
        {
            ReportError(
              DiagnosticId.UnsupportedPropertyType,
              property,
              "Exported property has unsupported type.",
              $"Exported properties must have type {nameof(NodeApi)}.JSValue.");
            return;
        }
    }

    public static string GetExportName(ISymbol symbol)
    {
        AttributeData? exportAttribute = symbol.GetAttributes().SingleOrDefault(
            (a) => a.AttributeClass?.Name == "JSExportAttribute");
        if (exportAttribute?.ConstructorArguments.SingleOrDefault().Value is string exportName)
        {
            return exportName;
        }

        return symbol is ITypeSymbol ? symbol.Name : ToCamelCase(symbol.Name);
    }

}
