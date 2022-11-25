using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NodeApi;

[Generator]
public class ModuleGenerator : ISourceGenerator
{
  private const string DiagnosticPrefix = "NAPI";
  private const string DiagnosticCategory = "NodeApi";

  private const string ModuleInitializerClassName = "Module";
  private const string ModuleInitializeMethodName = "Initialize";
  private const string ModuleRegisterFunctionName = "napi_register_module_v1";

  public void Initialize(GeneratorInitializationContext context)
  {
#if DEBUG
    // Note source generators re not covered by normal debugging,
    // because the generator runs at build time, not at application run-time.
    // Un-comment the line below to enable debugging at build time.

    ////System.Diagnostics.Debugger.Launch();
#endif
  }

  public void Execute(GeneratorExecutionContext context)
  {
    var moduleType = GetModuleType(context);
    if (moduleType != null)
    {
      var initializerSource = GenerateModuleInitializer(context, moduleType);
      context.AddSource($"{nameof(NodeApi)}.{ModuleInitializerClassName}", initializerSource);

      // Also write the generated code to a file under obj/ for diagnostics.
      // Depends on <CompilerVisibleProperty Include="BaseIntermediateOutputPath" />
      if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(
        "build_property.BaseIntermediateOutputPath", out var intermediateOutputPath))
      {
        var generatedSourcePath = Path.Combine(
          intermediateOutputPath,
          $"{nameof(NodeApi)}.{ModuleInitializerClassName}.cs");
        File.WriteAllText(generatedSourcePath, initializerSource.ToString());
      }
    }
  }

  private ITypeSymbol? GetModuleType(GeneratorExecutionContext context)
  {
    ITypeSymbol? moduleType = null;

    foreach (var type in context.Compilation.Assembly.TypeNames
      .SelectMany((n) => context.Compilation.GetSymbolsWithName(n, SymbolFilter.Type))
      .OfType<ITypeSymbol>())
    {
      if (type.GetAttributes().Any((a) => a.AttributeClass?.Name == nameof(JSModuleAttribute)))
      {
        if (moduleType != null)
        {
          var title = "Multiple types have Node API module attributes.";
          var descriptor = new DiagnosticDescriptor(
            id: DiagnosticPrefix + "1000",
            title,
            messageFormat: title + " Only a single class can represent the module exports.",
            DiagnosticCategory,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);
          context.ReportDiagnostic(
            Diagnostic.Create(descriptor, type.Locations.Single()));
          return null;
        }

        moduleType = type;
      }
    }

    return moduleType;
  }

  private SourceText GenerateModuleInitializer(
    GeneratorExecutionContext context,
    ITypeSymbol moduleType)
  {
    var s = new SourceBuilder();

    s += "using System.Collections.Generic;";
    s += "using System.Runtime.InteropServices;";
    s += "using static NodeApi.JSNativeApi.Interop;";

    if (moduleType.ContainingNamespace != null)
    {
      s += $"using {moduleType.ContainingNamespace};";
    }

    s++;
    s += "namespace NodeApi.Generated;";
    s++;
    s += $"public static class {ModuleInitializerClassName}";
    s += "{";

    s += $"[UnmanagedCallersOnly(EntryPoint = \"{ModuleRegisterFunctionName}\")]";
    s += $"public static napi_value {ModuleInitializeMethodName}(napi_env env, napi_value exports)";
    s += "{";
    s += "try";
    s += "{";
    s += "using var scope = new JSValueScope(env);";
    s += "var exportsValue = new JSValue(scope, exports);";
    s++;

    ExportModuleMembers(context, s, moduleType);

    s += "}";
    s += "catch (System.Exception ex)";
    s += "{";
    s += "System.Console.Error.WriteLine($\"Failed to export module: {ex}\");";
    s += "}";

    s++;
    s += "return exports;";
    s += "}";

    s += "}";

    return s;
  }

  private void ExportModuleMembers(
    GeneratorExecutionContext context,
    SourceBuilder s,
    ITypeSymbol moduleType)
  {
    // TODO: Also generate .d.ts?

    s += $"new JSModuleBuilder<{moduleType.Name}>()";
    s.IncreaseIndent();

    foreach (var member in moduleType.GetMembers()
      .Where((m) => m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic))
    {
      if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
      {
        ExportModuleMethod(context, s, method);
      }
      else if (member is IPropertySymbol property)
      {
        if (property.Type.Name == nameof(Type) && property.IsReadOnly)
        {
          ExportModuleClass(context, s, property);
        }
        else
        {
          ExportModuleProperty(context, s, property);
        }
      }
    }

    s += $".ExportModule(exportsValue, new {moduleType.Name}());";
    s.DecreaseIndent();
  }

  private void ExportModuleMethod(
    GeneratorExecutionContext context,
    SourceBuilder s,
    IMethodSymbol method)
  {
    ValidateExportedMethod(context, method);
    s += $".AddMethod(\"{ToCamelCase(method.Name)}\", obj => obj.{method.Name})";
  }

  private void ExportModuleProperty(
    GeneratorExecutionContext context,
    SourceBuilder s,
    IPropertySymbol property)
  {
    ValidateExportedProperty(context, property);

    s += $".AddProperty(\"{ToCamelCase(property.Name)}\",";
    s.IncreaseIndent();

    if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
    {
      s += $"getter: obj => obj.{property.Name},";
      s += $"setter: (obj, value) => obj.{property.Name} = value)";
    }
    else
    {
      s += $"getter: obj => obj.{property.Name},";
      s += $"setter: null)";
    }
    s.DecreaseIndent();
  }

  private void ExportModuleClass(
    GeneratorExecutionContext context,
    SourceBuilder s,
    IPropertySymbol property)
  {
    // TODO: Allow the typeof() expression to include a namespace.
    var expectedSource = $"typeof({property.Name})";

    var location = property.GetMethod!.Locations.Single();
    var sourceSpan = location.SourceSpan;
    var sourceText = location.SourceTree!.ToString();
    var getterSource = sourceText.Substring(sourceSpan.Start, sourceSpan.Length);
    if (getterSource != expectedSource)
    {
      ReportError(
        context,
        1004,
        "Exported class has unsupported getter code.",
        $"Getter for property {property.Name} must return {expectedSource}.",
        property.Locations.Single());
      return;
    }

    var classType = context.Compilation.GetSymbolsWithName(property.Name, SymbolFilter.Type)
      .Cast<ITypeSymbol>().Single();

    // TODO: Check that the class has a public constructor that takes a JSCallbackArgs parameter.

    s += $".AddProperty(\"{property.Name}\", "
      + $"new JSClassBuilder<{classType.Name}>(\"classType.Name\", args => new {classType.Name}(args))";
    s.IncreaseIndent();
    ExportClassMembers(context, s, classType);
    s += $".DefineClass())";
    s.DecreaseIndent();
  }

  private void ExportClassMembers(
    GeneratorExecutionContext context,
    SourceBuilder s,
    ITypeSymbol classType)
  {
    // TODO: Also generate .d.ts?

    foreach (var member in classType.GetMembers()
      .Where((m) => m.DeclaredAccessibility == Accessibility.Public))
    {
      if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
      {
        ExportClassMethod(context, s, method);
      }
      else if (member is IPropertySymbol property)
      {
        ExportClassProperty(context, s, property);
      }
    }
  }

  private void ExportClassMethod(
    GeneratorExecutionContext context,
    SourceBuilder s,
    IMethodSymbol method)
  {
    ValidateExportedMethod(context, method);
    if (method.IsStatic)
    {
      var className = method.ContainingType.Name;
      s += $".AddMethod(\"{ToCamelCase(method.Name)}\", () => {className}.{method.Name})";
    }
    else
    {
      s += $".AddMethod(\"{ToCamelCase(method.Name)}\", obj => obj.{method.Name})";
    }
  }

  private void ExportClassProperty(
    GeneratorExecutionContext context,
    SourceBuilder s,
    IPropertySymbol property)
  {
    ValidateExportedProperty(context, property);

    s += $".AddProperty(\"{ToCamelCase(property.Name)}\",";
    s.IncreaseIndent();
    if (property.IsStatic)
    {
      var className = property.ContainingType.Name;
      if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
      {
        s += $"getter: () => {className}.{property.Name},";
        s += $"setter: value => {className}.{property.Name} = value)";
      }
      else
      {
        s += $"getter: () => {className}.{property.Name},";
        s += $"setter: null)";
      }
    }
    else
    {
      if (property.SetMethod?.DeclaredAccessibility == Accessibility.Public)
      {
        s += $"getter: obj => obj.{property.Name},";
        s += $"setter: (obj, value) => obj.{property.Name} = value)";
      }
      else
      {
        s += $"getter: obj => obj.{property.Name},";
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
      (method.Parameters.Length == 1 && method.Parameters[0].Type.Name == nameof(JSCallbackArgs))))
    {
      ReportError(
        context,
        1001,
        $"Exported method {method.Name} has unsupported parameters.",
        "Exported methods must have either no parameters or a single parameter of type " +
          $"{typeof(JSCallbackArgs).Namespace}.{nameof(JSCallbackArgs)}.",
        method.Locations.Single());
      return;
    }

    if (method.ReturnType.Name != "Void" && method.ReturnType.Name != nameof(JSValue))
    {
      ReportError(
        context,
        1002,
        $"Exported method {method.Name} has unsupported return type.",
        "Exported methods must have return type " +
          $"{typeof(JSValue).Namespace}.{nameof(JSValue)} or void.",
        method.Locations.Single());
      return;
    }
  }

  private static void ValidateExportedProperty(
    GeneratorExecutionContext context,
    IPropertySymbol property)
  {
    if (property.Type.Name != nameof(JSValue))
    {
      ReportError(
        context,
        1003,
        "Exported property has unsupported type.",
        "Exported properties must have type " +
          $"{typeof(JSValue).Namespace}.{nameof(JSValue)}.",
        property.Locations.Single());
      return;
    }
  }

  private static string ToCamelCase(string name)
  {
    StringBuilder sb = new(name);
    sb[0] = char.ToLowerInvariant(sb[0]);
    return sb.ToString();
  }

  private static void ReportError(
    GeneratorExecutionContext context,
    int id,
    string title,
    string description,
    Location location) =>
    ReportDiagnostic(context, DiagnosticSeverity.Error, id, title, description, location);

  private static void ReportDiagnostic(
    GeneratorExecutionContext context,
    DiagnosticSeverity severity,
    int id,
    string title,
    string description,
    Location location)
  {
    var descriptor = new DiagnosticDescriptor(
      id: DiagnosticPrefix + id,
      title,
      messageFormat: title +
        (!string.IsNullOrEmpty(description) ? " " + description : string.Empty),
      DiagnosticCategory,
      DiagnosticSeverity.Error,
      isEnabledByDefault: true);
    context.ReportDiagnostic(
      Diagnostic.Create(descriptor, location));
  }
}
