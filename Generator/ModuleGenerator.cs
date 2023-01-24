using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NodeApi.Generator;

[Generator]
public class ModuleGenerator : ISourceGenerator
{
    private const string DiagnosticPrefix = "NAPI";
    private const string DiagnosticCategory = "NodeApi";

    private const string ModuleInitializerClassName = "Module";
    private const string ModuleInitializeMethodName = "Initialize";
    private const string ModuleRegisterFunctionName = "napi_register_module_v1";

    private enum DiagnosticId
    {
        MultipleModuleAttributes = 1001,
        InvalidModuleInitializer,
        ModuleInitializerIsNotPublic,
        ModuleInitializerIsNotStatic,
        ExportIsNotPublic,
        ExportIsNotStatic,
        UnsupportedPropertyType,
        UnsupportedMethodParameterType,
        UnsupportedMethodReturnType,
    }

#pragma warning disable CA1822 // Mark members as static
    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
        // Note source generators are not covered by normal debugging,
        // because the generator runs at build time, not at application run-time.
        // Un-comment the line below to enable debugging at build time.

        ////System.Diagnostics.Debugger.Launch();
#endif
    }
#pragma warning restore CA1822 // Mark members as static

    public void Execute(GeneratorExecutionContext context)
    {
        ISymbol? moduleInitializer = GetModuleInitializer(context);
        IEnumerable<ISymbol> exportItems = GetModuleExportItems(context);

        SourceText initializerSource = GenerateModuleInitializer(
            context, moduleInitializer, exportItems);
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

    private static ISymbol? GetModuleInitializer(GeneratorExecutionContext context)
    {
        List<ISymbol> moduleInitializers = new();

        foreach (ITypeSymbol type in context.Compilation.Assembly.TypeNames
          .SelectMany((n) => context.Compilation.GetSymbolsWithName(n, SymbolFilter.Type))
          .OfType<ITypeSymbol>())
        {
            if (type.GetAttributes().Any((a) => a.AttributeClass?.Name == "JSModuleAttribute"))
            {
                if (type.TypeKind != TypeKind.Class)
                {
                    ReportError(
                        context,
                        DiagnosticId.InvalidModuleInitializer,
                        type,
                        "[JSModule] attribute must be applied to a class.");
                }
                else if (type.DeclaredAccessibility != Accessibility.Public)
                {
                    ReportError(
                        context,
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
                                context,
                                DiagnosticId.InvalidModuleInitializer,
                                member,
                                "[JSModule] attribute must be applied to a method.");
                        }
                        else if (!member.IsStatic)
                        {
                            ReportError(
                                context,
                                DiagnosticId.ModuleInitializerIsNotStatic,
                                member,
                                "Module initialize method must be static.");
                        }
                        else if (type.DeclaredAccessibility != Accessibility.Public)
                        {
                            ReportError(
                                context,
                                DiagnosticId.ModuleInitializerIsNotPublic,
                                member,
                                "Containing type of module initialize method must be public.");
                        }
                        else if (member.DeclaredAccessibility != Accessibility.Public)
                        {
                            ReportError(
                                context,
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
                    context,
                    DiagnosticId.MultipleModuleAttributes,
                    initializer,
                    "Multiple [JSModule] attributes found.",
                    "Designate a single class or static method to handle module initialization.");
            }

            return null;
        }

        return moduleInitializers.SingleOrDefault();
    }

    private static IEnumerable<ISymbol> GetModuleExportItems(GeneratorExecutionContext context)
    {
        foreach (ITypeSymbol type in context.Compilation.Assembly.TypeNames
          .SelectMany((n) => context.Compilation.GetSymbolsWithName(n, SymbolFilter.Type))
          .OfType<ITypeSymbol>())
        {
            if (type.GetAttributes().Any((a) => a.AttributeClass?.Name == "JSExportAttribute"))
            {
                if (type.DeclaredAccessibility != Accessibility.Public)
                {
                    ReportError(
                        context,
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
                                context,
                                DiagnosticId.ExportIsNotPublic,
                                member,
                                "Containing type of exported member must be public.");
                        }
                        else if (member.DeclaredAccessibility != Accessibility.Public)
                        {
                            ReportError(
                                context,
                                DiagnosticId.ExportIsNotPublic,
                                member,
                                "Exported member must be public.");
                        }
                        else if (!(member.IsStatic))
                        {
                            ReportError(
                                context,
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

    private static SourceText GenerateModuleInitializer(
      GeneratorExecutionContext context,
      ISymbol? moduleInitializer,
      IEnumerable<ISymbol> exportItems)
    {
        var s = new SourceBuilder();

        s += "using System.Collections.Generic;";
        s += "using System.Runtime.InteropServices;";
        s += "using static NodeApi.JSNativeApi.Interop;";

        s++;
        s += "namespace NodeApi.Generated;";
        s++;
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

        if (moduleInitializer is IMethodSymbol moduleInitializerMethod)
        {
            if (exportItems.Any())
            {
                ReportError(
                    context,
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
            ExportModule(context, s, moduleInitializer as ITypeSymbol, exportItems);
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
        s += "}";

        return s;
    }

    private static void ExportModule(
      GeneratorExecutionContext context,
      SourceBuilder s,
      ITypeSymbol? moduleType,
      IEnumerable<ISymbol> exportItems)
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
                    ExportMethod(context, s, method);
                }
                else if (member is IPropertySymbol property)
                {
                    ExportProperty(context, s, property);
                }
            }
        }
        else
        {
            s += $"exportsValue = new JSModuleBuilder<System.Object>()";
            s.IncreaseIndent();
        }

        // Export static items tagged with [JSExport]
        foreach (ISymbol exportItem in exportItems)
        {
            string exportName = GetExportName(exportItem);
            if (exportItem is ITypeSymbol exportType)
            {
                s += $".AddProperty(\"{exportName}\",";
                s.IncreaseIndent();

                string ns = GetNamespace(exportType);
                if (exportType.IsStatic)
                {
                    s += $"new JSClassBuilder<{ns}.{exportType.Name}>(\"{exportName}\")";
                }
                else
                {
                    s += $"new JSClassBuilder<{ns}.{exportType.Name}>(\"{exportName}\",";
                    s += $"\t(args) => new {ns}.{exportType.Name}(args))";
                }

                ExportMembers(context, s, exportType);
                s += ".DefineClass())";
                s.DecreaseIndent();
            }
            else if (exportItem is IPropertySymbol exportProperty)
            {
                ExportProperty(context, s, exportProperty, exportName);
            }
            else if (exportItem is IMethodSymbol exportMethod)
            {
                ExportMethod(context, s, exportMethod, exportName);
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
      GeneratorExecutionContext context,
      SourceBuilder s,
      ITypeSymbol classType)
    {
        // TODO: Also generate .d.ts?

        foreach (ISymbol? member in classType.GetMembers()
          .Where((m) => m.DeclaredAccessibility == Accessibility.Public))
        {
            if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
            {
                ExportMethod(context, s, method);
            }
            else if (member is IPropertySymbol property)
            {
                ExportProperty(context, s, property);
            }
        }
    }

    private static void ExportMethod(
      GeneratorExecutionContext context,
      SourceBuilder s,
      IMethodSymbol method,
      string? exportName = null)
    {
        ValidateExportedMethod(context, method);
        exportName ??= ToCamelCase(method.Name);

        if (method.IsStatic)
        {
            string ns = GetNamespace(method);
            string className = method.ContainingType.Name;
            _ = s + $".AddMethod(\"{exportName}\", () => {ns}.{className}.{method.Name})";
        }
        else
        {
            _ = s + $".AddMethod(\"{exportName}\", (obj) => obj.{method.Name})";
        }
    }

    private static void ExportProperty(
      GeneratorExecutionContext context,
      SourceBuilder s,
      IPropertySymbol property,
      string? exportName = null)
    {
        ValidateExportedProperty(context, property);
        exportName ??= ToCamelCase(property.Name);

        s += $".AddProperty(\"{exportName}\",";
        s.IncreaseIndent();
        if (property.IsStatic)
        {
            string ns = GetNamespace(property);
            string className = property.ContainingType.Name;
            s += $"getter: () => {ns}.{className}.{property.Name},";
            if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
            {
                s += $"setter: (value) => {ns}.{className}.{property.Name} = value)";
            }
            else
            {
                s += $"setter: null)";
            }
        }
        else
        {
            s += $"getter: (obj) => obj.{property.Name},";
            if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
            {
                s += $"setter: (obj, value) => obj.{property.Name} = value)";
            }
            else
            {
                s += $"setter: null)";
            }
        }
        s.DecreaseIndent();
    }

    private static void ValidateExportedMethod(
      GeneratorExecutionContext context,
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
              context,
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
              context,
              DiagnosticId.UnsupportedMethodReturnType,
              method,
              $"Exported method {method.Name} has unsupported return type. ",
              $"Exported methods must have return type {nameof(NodeApi)}.JSValue or void.");
            return;
        }
    }

    private static void ValidateExportedProperty(
      GeneratorExecutionContext context,
      IPropertySymbol property)
    {
        if (property.Type.Name != "JSValue")
        {
            ReportError(
              context,
              DiagnosticId.UnsupportedPropertyType,
              property,
              "Exported property has unsupported type.",
              $"Exported properties must have type {nameof(NodeApi)}.JSValue.");
            return;
        }
    }

    private static string GetExportName(ISymbol symbol)
    {
        AttributeData? exportAttribute = symbol.GetAttributes().SingleOrDefault(
            (a) => a.AttributeClass?.Name == "JSExportAttribute");
        if (exportAttribute?.ConstructorArguments.SingleOrDefault().Value is string exportName)
        {
            return exportName;
        }

        return symbol is ITypeSymbol ? symbol.Name : ToCamelCase(symbol.Name);
    }

    private static string GetNamespace(ISymbol symbol)
    {
        string ns = string.Empty;
        for (INamespaceSymbol s = symbol.ContainingNamespace;
            !s.IsGlobalNamespace;
            s = s.ContainingNamespace)
        {
            ns = s.Name + (ns.Length > 0 ? "." + ns : string.Empty);
        }
        return ns;
    }

    private static string ToCamelCase(string name)
    {
        StringBuilder sb = new(name);
        sb[0] = char.ToLowerInvariant(sb[0]);
        return sb.ToString();
    }

    private static void ReportError(
      GeneratorExecutionContext context,
      DiagnosticId id,
      ISymbol symbol,
      string title,
      string? description = null) =>
      ReportDiagnostic(
          context,
          DiagnosticSeverity.Error,
          id,
          symbol.Locations.Single(),
          title,
          description);

    private static void ReportDiagnostic(
      GeneratorExecutionContext context,
      DiagnosticSeverity severity,
      DiagnosticId id,
      Location location,
      string title,
      string? description = null)
    {
        var descriptor = new DiagnosticDescriptor(
          id: DiagnosticPrefix + id,
          title,
          messageFormat: title +
            (!string.IsNullOrEmpty(description) ? " " + description : string.Empty),
          DiagnosticCategory,
          severity,
          isEnabledByDefault: true);
        context.ReportDiagnostic(
          Diagnostic.Create(descriptor, location));
    }
}
