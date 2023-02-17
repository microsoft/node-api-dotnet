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
        Context = context;

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

            // No type definitions are generated when using a custom init function.
            if (moduleInitializer is not IMethodSymbol)
            {
                TypeDefinitionsGenerator tsGenerator = new(exportItems);
                SourceText typeDefinitions = tsGenerator.GenerateTypeDefinitions();
                if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(
                    "build_property.TargetPath", out string? targetPath))
                {
                    string typeDefinitionsPath = Path.ChangeExtension(targetPath, ".d.ts");
                    File.WriteAllText(typeDefinitionsPath, typeDefinitions.ToString());
                }
            }

        }
        catch (Exception ex)
        {
            ReportError(DiagnosticId.GeneratorError, null, "Generator failed.", ex.Message);
        }
    }

    /// <summary>
    /// Enumerates all the types defined in the current compilation.
    /// </summary>
    private IEnumerable<ITypeSymbol> GetCompilationTypes()
    {
        return Context.Compilation.Assembly.TypeNames
          .SelectMany((n) => Context.Compilation.GetSymbolsWithName(n, SymbolFilter.Type))
          .OfType<ITypeSymbol>();
    }

    /// <summary>
    /// Scans classes and static methods to find a single item with a [JSModule] attribute.
    /// </summary>
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

                // TODO: Check for a public constructor that takes a single JSContext argument.

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

    /// <summary>
    /// Enumerates all types and static methods with a [JSExport] attribute.
    /// </summary>
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
                else if (type.TypeKind != TypeKind.Class &&
                    type.TypeKind != TypeKind.Struct &&
                    type.TypeKind != TypeKind.Enum)
                {
                    ReportError(
                        DiagnosticId.UnsupportedTypeKind,
                        type,
                        $"Exporting {type.TypeKind} types is not supported.");
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

    /// <summary>
    /// Generates a `Module` class with an exported module register function.
    /// </summary>
    /// <param name="moduleInitializer">Optional custom module class or module initialization method.</param>
    /// <param name="exportItems">Enumeration of all exported types and functions (static methods).</param>
    /// <returns>The generated source.</returns>
    private SourceText GenerateModuleInitializer(
      ISymbol? moduleInitializer,
      IEnumerable<ISymbol> exportItems)
    {
        var s = new SourceBuilder();

        s += "using System.CodeDom.Compiler;";
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

        // The unmanaged entrypoint is used only when the AOT-compiled module is loaded.
        s += $"[UnmanagedCallersOnly(EntryPoint = \"{ModuleRegisterFunctionName}\")]";
        s += $"public static napi_value _{ModuleInitializeMethodName}(napi_env env, napi_value exports)";
        s += $"{s.Indent}=> {ModuleInitializeMethodName}(env, exports);";
        s++;

        // The main initialization entrypoint is called by the `ManagedHost`, and by the unmanaged entrypoint.
        s += $"public static napi_value {ModuleInitializeMethodName}(napi_env env, napi_value exports)";
        s += "{";
        s += "try";
        s += "{";
        s += "using JSValueScope scope = new(env);";
        s += "JSContext context = new(env);";
        s += "JSValue exportsValue = new(scope, exports);";
        s++;

        AdapterGenerator adapterGenerator = new(Context);
        if (moduleInitializer is IMethodSymbol moduleInitializerMethod)
        {
            // Just call the custom module initialization method. Additional tagged exports aren't supported.

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
            s += $"return {ns}.{className}.{methodName}(context, (JSObject)exportsValue)";
            s += "\t.GetCheckedHandle();";
        }
        else
        {
            // Export the custom module class and/or additional types and static methods tagged for export.

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

        // Generate any supporting adapter methods that the module initialization depended on.
        adapterGenerator.GenerateAdapters(ref s);

        s += "}";

        return s;
    }

    /// <summary>
    /// Generates code to define all the properties of the module and return the module exports.
    /// </summary>
    private static void ExportModule(
      ref SourceBuilder s,
      ITypeSymbol? moduleType,
      IEnumerable<ISymbol> exportItems,
      AdapterGenerator adapterGenerator)
    {
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
            s += $"exportsValue = new JSModuleBuilder<JSContext>()";
            s.IncreaseIndent();
        }

        // Export types and functions (static methods) tagged with [JSExport]
        foreach (ISymbol exportItem in exportItems)
        {
            string exportName = GetExportName(exportItem);
            if (exportItem is ITypeSymbol exportClass &&
                exportClass.TypeKind == TypeKind.Class)
            {
                s += $".AddProperty(\"{exportName}\",";
                s.IncreaseIndent();

                string ns = GetNamespace(exportClass);
                if (exportClass.IsStatic)
                {
                    // Static classes are projected as simple JS objects with defined properties.
                    s += $"new JSClassBuilder<object>(context, \"{exportName}\")";
                }
                else
                {
                    s += $"new JSClassBuilder<{ns}.{exportClass.Name}>(context, \"{exportName}\",";

                    // The class constructor may take no parameter, or a single JSCallbackArgs
                    // parameter, or may use an adapter to support arbitrary parameters.
                    string? constructorAdapterName =
                        adapterGenerator.GetConstructorAdapterName(exportClass);
                    if (constructorAdapterName != null)
                    {
                        s += $"\t{constructorAdapterName})";
                    }
                    else if (AdapterGenerator.HasNoArgsConstructor(exportClass))
                    {
                        s += $"\t() => new {ns}.{exportClass.Name}())";
                    }
                    else
                    {
                        s += $"\t(args) => new {ns}.{exportClass.Name}(args))";
                    }
                }

                // Export all the class members, then define the class.
                ExportMembers(ref s, exportClass, adapterGenerator);
                s += exportClass.IsStatic ? ".DefineStaticClass())" : ".DefineClass())";
                s.DecreaseIndent();
            }
            else if (exportItem is ITypeSymbol exportStruct &&
                exportStruct.TypeKind == TypeKind.Struct)
            {
                s += $".AddProperty(\"{exportName}\",";
                s.IncreaseIndent();

                string ns = GetNamespace(exportStruct);
                s += $"new JSStructBuilder<{ns}.{exportStruct.Name}>(context, \"{exportName}\")";

                ExportMembers(ref s, exportStruct, adapterGenerator);
                s += ".DefineStruct())";
                s.DecreaseIndent();
            }
            else if (exportItem is ITypeSymbol exportEnum && exportEnum.TypeKind == TypeKind.Enum)
            {
                s += $".AddProperty(\"{exportName}\",";
                s.IncreaseIndent();

                // Exported enums are similar to static classes with integer properties.
                s += $"new JSClassBuilder<object>(context, \"{exportName}\")";
                ExportMembers(ref s, exportEnum, adapterGenerator);
                s += ".DefineEnum())";
                s.DecreaseIndent();
            }
            else if (exportItem is IPropertySymbol exportProperty)
            {
                // Export tagged static properties as properties on the module.
                ExportProperty(ref s, exportProperty, adapterGenerator, exportName);
            }
            else if (exportItem is IMethodSymbol exportMethod)
            {
                // Export tagged static methods as top-level functions on the module.
                ExportMethod(ref s, exportMethod, adapterGenerator, exportName);
            }
        }

        if (moduleType != null)
        {
            // Construct an instance of the custom module class when the module is initialized.
            // The module class constructor may optionally take a JSContext parameter. If an
            // appropriate constructor is not present then the generated code will not compile.
            IEnumerable<IMethodSymbol> constructors = moduleType.GetMembers()
                .OfType<IMethodSymbol>().Where((m) => m.MethodKind == MethodKind.Constructor);
            IMethodSymbol? constructor = constructors.SingleOrDefault((c) =>
                c.Parameters.Length == 1 && c.Parameters[0].Type.Name == "JSContext") ??
                constructors.SingleOrDefault((c) => c.Parameters.Length == 0);
            string contextParameter = constructor?.Parameters.Length == 1 ?
                "context" : string.Empty;
            string ns = GetNamespace(moduleType);
            s += $".ExportModule(new {ns}.{moduleType.Name}({contextParameter}), (JSObject)exportsValue);";
        }
        else
        {
            s += $".ExportModule(context, (JSObject)exportsValue);";
        }

        s.DecreaseIndent();
    }

    /// <summary>
    /// Generates code to define properties and methods for an exported class or struct type.
    /// </summary>
    private static void ExportMembers(
      ref SourceBuilder s,
      ITypeSymbol type,
      AdapterGenerator adapterGenerator)
    {
        foreach (ISymbol? member in type.GetMembers()
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
            else if (type.TypeKind == TypeKind.Enum && member is IFieldSymbol field)
            {
                s += $".AddProperty(\"{field.Name}\", {field.ConstantValue}, " +
                    "JSPropertyAttributes.Static | JSPropertyAttributes.Enumerable)";
            }
        }
    }

    /// <summary>
    /// Generate code for a method exported on a class, struct, or module.
    /// </summary>
    private static void ExportMethod(
      ref SourceBuilder s,
      IMethodSymbol method,
      AdapterGenerator adapterGenerator,
      string? exportName = null)
    {
        exportName ??= ToCamelCase(method.Name);

        // An adapter method may be used to support marshalling arbitrary parameters,
        // if the method does not match the `JSCallback` signature.
        string? adapterName = adapterGenerator.GetMethodAdapterName(method);
        string attributes = "JSPropertyAttributes.DefaultMethod" +
            (method.IsStatic ? "| JSPropertyAttributes.Static" : string.Empty);
        if (adapterName != null)
        {
            s += $".AddMethod(\"{exportName}\", {adapterName},\n{attributes})";
        }
        else if (method.IsStatic)
        {
            string ns = GetNamespace(method);
            string className = method.ContainingType.Name;
            s += $".AddMethod(\"{exportName}\", () => {ns}.{className}.{method.Name},\n{attributes})";
        }
        else
        {
            s += $".AddMethod(\"{exportName}\", (obj) => obj.{method.Name},\n{attributes})";
        }
    }

    /// <summary>
    /// Generates code for a property exported on a class, struct, or module.
    /// </summary>
    private static void ExportProperty(
      ref SourceBuilder s,
      IPropertySymbol property,
      AdapterGenerator adapterGenerator,
      string? exportName = null)
    {
        exportName ??= ToCamelCase(property.Name);

        string attributes = "JSPropertyAttributes.DefaultProperty" +
            (property.IsStatic ? " | JSPropertyAttributes.Static" : string.Empty);

        // Getter and setter adapter methods may be used if the property type is not `JSValue`.
        (string? getterAdapterName, string? setterAdapterName) =
            adapterGenerator.GetPropertyAdapterNames(property);

        if (property.ContainingType.TypeKind == TypeKind.Struct)
        {
            // Struct properties are not backed by getter/setter methods. The entire struct is
            // always passed by value. Properties are converted to/from `JSValue` by the struct
            // adapter method.
            s += $".AddProperty(\"{exportName}\", {attributes})";
            return;
        }

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
            s += $"setter: {setterAdapterName},";
        }
        else if (property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
        {
            s += $"setter: null,";
        }
        else if (property.IsStatic)
        {
            s += $"setter: (value) => {ns}.{className}.{property.Name} = value,";
        }
        else
        {
            s += $"setter: (obj, value) => obj.{property.Name} = value,";
        }

        s += $"{attributes})";

        s.DecreaseIndent();
    }

    /// <summary>
    /// Gets the projected name for a symbol, which may be different from its C# name.
    /// </summary>
    public static string GetExportName(ISymbol symbol)
    {
        // If the symbol has a JSExportAttribute.Name property, use that.
        if (GetJSExportAttribute(symbol)?.ConstructorArguments.SingleOrDefault().Value
            is string exportName)
        {
            return exportName;
        }

        // Member names are automatically formatted as camelCase; type names are not.
        return symbol is ITypeSymbol ? symbol.Name : ToCamelCase(symbol.Name);
    }

    /// <summary>
    /// Gets the [JSExport] attribute data for a symbol, if any.
    /// </summary>
    public static AttributeData? GetJSExportAttribute(ISymbol symbol)
    {
        return symbol.GetAttributes().SingleOrDefault(
            (a) => a.AttributeClass?.Name == "JSExportAttribute");
    }
}
