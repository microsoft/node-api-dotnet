// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.Generator;

// An analyzer bug results in incorrect reports of CA1822 against methods in this class.
#pragma warning disable CA1822 // Mark members as static

/// <summary>
/// Generates JavaScript module registration code for C# APIs exported to JS.
/// </summary>
[Generator]
public class ModuleGenerator : SourceGenerator, ISourceGenerator
{
    private const string ModuleInitializerClassName = "Module";
    private const string ModuleInitializeMethodName = "Initialize";
    private const string ModuleRegisterFunctionName = "napi_register_module_v1";

    private readonly JSMarshaller _marshaller = new()
    {
        // Currently source-generated marshalling uses auto camel-casing,
        // while dynamic invocation does not.
        AutoCamelCase = true,
    };

    private readonly Dictionary<string, LambdaExpression> _callbackAdapters = new();
    private readonly List<ITypeSymbol> _exportedInterfaces = new();

    public GeneratorExecutionContext Context { get; protected set; }

#pragma warning disable IDE0060 // Unused parameter
    public void Initialize(GeneratorInitializationContext context)
#pragma warning restore IDE0060
    {
        // Note source generators cannot be directly launched in a debugger,
        // because the generator runs at build time, not at application run-time.
        // Set the environment variable to trigger debugging at build time.
        DebugHelper.AttachDebugger("NODE_API_DEBUG_GENERATOR");
    }

    public void Execute(GeneratorExecutionContext context)
    {
        Context = context;
        string generatedSourceFileName =
            (context.Compilation.AssemblyName ?? "Assembly") + ".NodeApi.g.cs";
        try
        {
            ISymbol? moduleInitializer = GetModuleInitializer();
            List<ISymbol> exportItems = GetModuleExportItems().ToList();

            if (exportItems.Count == 0 &&
                !GetCompilationTypes().Any((t) => t.DeclaredAccessibility == Accessibility.Public))
            {
                ReportDiagnostic(
                    DiagnosticSeverity.Info,
                    DiagnosticId.NoExports,
                    location: null,
                    "Skipping module code generation because no APIs are exported to JavaScript.");
                return;
            }

            SourceText initializerSource = GenerateModuleInitializer(
                moduleInitializer, exportItems);
            context.AddSource(generatedSourceFileName, initializerSource);
        }
        catch (Exception ex)
        {
            while (ex != null)
            {
                ReportException(ex);
                ex = ex.InnerException!;
            }
        }
    }

    public override void ReportDiagnostic(Diagnostic diagnostic)
        => Context.ReportDiagnostic(diagnostic);

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
            if (type.GetAttributes().Any(
                (a) => a.AttributeClass?.AsType() == typeof(JSModuleAttribute)))
            {
                if (type.TypeKind != TypeKind.Class)
                {
                    ReportError(
                        DiagnosticId.InvalidModuleInitializer,
                        type,
                        "[JSModule] attribute must be applied to a class or method.");
                }
                else if (type.DeclaredAccessibility != Accessibility.Public)
                {
                    ReportError(
                        DiagnosticId.ModuleInitializerIsNotPublic,
                        type,
                        "Module class must have public visibility.");
                }

                // TODO: Check for a public constructor that takes a single JSRuntimeContext argument.

                moduleInitializers.Add(type);
            }
            else if (type.TypeKind == TypeKind.Class)
            {
                foreach (ISymbol? member in type.GetMembers())
                {
                    if (member.GetAttributes().Any(
                        (a) => a.AttributeClass?.AsType() == typeof(JSModuleAttribute)))
                    {
                        // TODO: Check method parameter and return types.
                        if (member is not IMethodSymbol)
                        {
                            ReportError(
                                DiagnosticId.InvalidModuleInitializer,
                                member,
                                "[JSModule] attribute must be applied to a class or method.");
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
            if (IsExported(type))
            {
                if (type.TypeKind != TypeKind.Class &&
                    type.TypeKind != TypeKind.Struct &&
                    type.TypeKind != TypeKind.Interface &&
                    type.TypeKind != TypeKind.Delegate &&
                    type.TypeKind != TypeKind.Enum)
                {
                    ReportWarning(
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

                // Don't return nested types when the containing type is also exported.
                // Nested types will be exported as properties of their containing type.
                if (type.ContainingType == null || !IsExported(type.ContainingType))
                {
                    yield return type;
                }
            }
            else if (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct)
            {
                foreach (ISymbol? member in type.GetMembers())
                {
                    if (IsExported(member))
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
                        else if (!member.IsStatic)
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
    private SourceBuilder GenerateModuleInitializer(
      ISymbol? moduleInitializer,
      IEnumerable<ISymbol> exportItems)
    {
        var s = new SourceBuilder();

        s += "using System.CodeDom.Compiler;";
        s += "using System.Runtime.InteropServices;";
        s += "using Microsoft.JavaScript.NodeApi.Interop;";
        s += "using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;";
        s++;
        s += "#pragma warning disable CS1591 // Do not warn about missing doc comments in generated code.";
        s++;
        s += "namespace Microsoft.JavaScript.NodeApi.Generated;";
        s++;

        string generatorName = typeof(ModuleGenerator).Assembly.GetName()!.Name!;
        Version? generatorVersion = typeof(ModuleGenerator).Assembly.GetName().Version;
        s += $"[GeneratedCode(\"{generatorName}\", \"{generatorVersion}\")]";
        s += "[JSExport(false)]"; // Prevent typedefs from being generated for this class.
        s += $"public static class {ModuleInitializerClassName}";
        s += "{";

        // The module scope is not disposed after a successful initialization. It becomes
        // the parent of callback scopes, allowing the JS runtime instance to be inherited.
        s += "private static JSValueScope _moduleScope;";

        // The unmanaged entrypoint is used only when the AOT-compiled module is loaded.
        s += "#if !NETFRAMEWORK";
        s += $"[UnmanagedCallersOnly(EntryPoint = \"{ModuleRegisterFunctionName}\")]";
        s += $"public static napi_value _{ModuleInitializeMethodName}(napi_env env, napi_value exports)";
        s += $"{s.Indent}=> {ModuleInitializeMethodName}(env, exports);";
        s += "#endif";
        s++;

        // The main initialization entrypoint is called by the `ManagedHost`, and by the unmanaged entrypoint.
        s += $"public static napi_value {ModuleInitializeMethodName}(napi_env env, napi_value exports)";
        s += "{";
        s += "_moduleScope = new JSValueScope(JSValueScopeType.Module, env, runtime: default);";
        s += "try";
        s += "{";
        s += "JSRuntimeContext context = _moduleScope.RuntimeContext;";
        s += "JSValue exportsValue = new(exports, _moduleScope);";
        s++;

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

            string classFullName = GetFullName(moduleInitializerMethod.ContainingType);
            string methodName = moduleInitializerMethod.Name;
            s += $"return (napi_value){classFullName}.{methodName}(";
            s += "context, (JSObject)exportsValue);";
        }
        else
        {
            // Export the custom module class and/or additional types and static methods tagged for export.

            ExportModule(ref s, moduleInitializer as ITypeSymbol, exportItems);
            s++;
            s += "return (napi_value)exportsValue;";
        }

        s += "}";
        s += "catch (System.Exception ex)";
        s += "{";
        s += "System.Console.Error.WriteLine($\"Failed to export module: {ex}\");";
        s += "JSError.ThrowError(ex);";
        s += "_moduleScope.Dispose();";
        s += "return exports;";
        s += "}";
        s += "}";

        GenerateCallbackAdapters(ref s);

        foreach (ITypeSymbol interfaceSymbol in _exportedInterfaces)
        {
            s++;
            GenerateInterfaceAdapter(ref s, interfaceSymbol, _marshaller);
        }

        s += "}";

        return s;
    }

    /// <summary>
    /// Generates code to define all the properties of the module and return the module exports.
    /// </summary>
    private void ExportModule(
      ref SourceBuilder s,
      ITypeSymbol? moduleType,
      IEnumerable<ISymbol> exportItems)
    {
        if (moduleType != null)
        {
            string typeFullName = GetFullName(moduleType);
            s += $"var module = new JSModuleBuilder<{typeFullName}>()";
            s.IncreaseIndent();

            // Export public non-static members of the module class.
            IEnumerable<ISymbol> members = moduleType.GetMembers()
                .Where((m) => m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic);

            foreach (IPropertySymbol property in members.OfType<IPropertySymbol>())
            {
                ExportProperty(ref s, property, GetExportName(property));
            }

            foreach (IGrouping<string, IMethodSymbol> methodGroup in members.OfType<IMethodSymbol>()
                .Where((m) => m.MethodKind == MethodKind.Ordinary)
                .GroupBy(GetExportName))
            {
                ExportMethod(ref s, methodGroup, methodGroup.Key);
            }
        }
        else
        {
            s += $"var module = new JSModuleBuilder<JSRuntimeContext>()";
            s.IncreaseIndent();
        }

        // Generate adapters for exported delegates for later use in method marshalling.
        foreach (ITypeSymbol exportDelegate in exportItems.OfType<ITypeSymbol>()
            .Where((t) => t.TypeKind == TypeKind.Delegate))
        {
            string exportName = GetExportName(exportDelegate);
            ExportDelegate(exportDelegate);
        }

        // Export static properties tagged with [JSExport] as module-level properties.
        foreach (IPropertySymbol exportProperty in exportItems.OfType<IPropertySymbol>())
        {
            string exportName = GetExportName(exportProperty);
            ExportProperty(ref s, exportProperty, exportName);
        }

        // Export static methods tagged with [JSExport] as module-level functions.
        foreach (IGrouping<string, IMethodSymbol> methodGroup in exportItems.OfType<IMethodSymbol>()
            .GroupBy(GetExportName))
        {
            ExportMethod(ref s, methodGroup, methodGroup.Key);
        }

        s += ";";
        s.DecreaseIndent();
        s++;

        // Export types tagged with [JSExport] as properties on the module.
        // Ensure base classes are exported before derived classes.
        ITypeSymbol[] exportTypes = exportItems.OfType<ITypeSymbol>().ToArray();
        Array.Sort(exportTypes, OrderByTypeHierarchy);
        foreach (ITypeSymbol exportType in exportTypes)
        {
            string exportName = GetExportName(exportType);
            ExportType(ref s, exportType, exportName);
        }

        if (moduleType != null)
        {
            // Construct an instance of the custom module class when the module is initialized.
            // If a no-args constructor is not present then the generated code will not compile.
            string typeFullName = GetFullName(moduleType);
            s += $"exportsValue = module.ExportModule(new {typeFullName}(), (JSObject)exportsValue);";
        }
        else
        {
            s += $"exportsValue = module.ExportModule(context, (JSObject)exportsValue);";
        }
    }

    /// <summary>
    /// Orders types by their inheritance hierarchy, so base types are ordered before derived
    /// types, and types with the same base type are in alphabetical order.
    private static int OrderByTypeHierarchy(ITypeSymbol a, ITypeSymbol b)
    {
        static string GetTypeHierarchyPath(ITypeSymbol type)
        {
            if (type.BaseType == null) // System.Object
            {
                return "/";
            }

            string typeFullName = GetFullName(type);
            return GetTypeHierarchyPath(type.BaseType) + "/" + typeFullName;
        }

        return string.CompareOrdinal(GetTypeHierarchyPath(a), GetTypeHierarchyPath(b));
    }

    /// <summary>
    /// Generates code to export a class, struct, interface, enum, or delegate type.
    /// </summary>
    private void ExportType(
        ref SourceBuilder s,
        ITypeSymbol type,
        string exportName)
    {
        string propertyAttributes =
            $"{nameof(JSPropertyAttributes)}.{nameof(JSPropertyAttributes.Static)} | " +
            $"{nameof(JSPropertyAttributes)}.{nameof(JSPropertyAttributes.Enumerable)}";

        // Declare nested types first, so they can be exported as static properties of this type.
        foreach (INamedTypeSymbol nestedType in type.GetTypeMembers()
            .Where((t) => t.DeclaredAccessibility == Accessibility.Public))
        {
            ExportType(ref s, nestedType, GetExportName(nestedType));
        }

        string typeVariableName = "type_" + GetFullName(type).Replace('.', '_');

        if (type.TypeKind == TypeKind.Class ||
            type.TypeKind == TypeKind.Interface)
        {
            if (type.TypeKind == TypeKind.Interface)
            {
                // Interfaces do not have constructors.
                s += $"var {typeVariableName} = new JSClassBuilder<{GetFullName(type)}>(\"{exportName}\")";
                _exportedInterfaces.Add(type);
            }
            else if (type.IsStatic)
            {
                // Static classes do not have constructors, and cannot be used as type params.
                s += $"var {typeVariableName} = new JSClassBuilder<object>(\"{exportName}\")";
            }
            else
            {
                s += $"var {typeVariableName} = new JSClassBuilder<{GetFullName(type)}>(\"{exportName}\",";
                ExportConstructor(ref s, type);
            }

            s.IncreaseIndent();

            // Export all the class members, then define the class.
            ExportMembers(ref s, type);

            bool isStreamClass = typeof(System.IO.Stream).IsAssignableFrom(type.AsType());
            if (type.TypeKind == TypeKind.Class && !isStreamClass && IsExported(type.BaseType!))
            {
                string baseTypeVariableName = "type_" +
                    GetFullName(type.BaseType!).Replace('.', '_');
                s += $".DefineClass(baseClass: {baseTypeVariableName});";
            }
            else
            {
                s += (type.TypeKind == TypeKind.Interface ? ".DefineInterface()" :
                    type.IsStatic ? ".DefineStaticClass()" : ".DefineClass()") + ';';
            }

            s.DecreaseIndent();

            if (type.ContainingType == null)
            {
                s += $"module.AddProperty(\"{exportName}\", {typeVariableName}, {propertyAttributes});";
            }
        }
        else if (type.TypeKind == TypeKind.Struct)
        {
            s += $"var {typeVariableName} = new JSClassBuilder<{GetFullName(type)}>(\"{exportName}\",";
            ExportConstructor(ref s, type);

            s.IncreaseIndent();
            ExportMembers(ref s, type);
            s += $".DefineStruct();";
            s.DecreaseIndent();

            if (type.ContainingType == null)
            {
                s += $"module.AddProperty(\"{exportName}\", {typeVariableName}, {propertyAttributes});";
            }
        }
        else if (type.TypeKind == TypeKind.Enum)
        {

            // Exported enums are similar to static classes with integer properties.
            s += $"var {typeVariableName} = new JSClassBuilder<object>(\"{exportName}\")";

            s.IncreaseIndent();
            ExportMembers(ref s, type);
            s += $".DefineEnum();";
            s.DecreaseIndent();

            if (type.ContainingType == null)
            {
                s += $"module.AddProperty(\"{exportName}\", {typeVariableName}, {propertyAttributes});";
            }
        }
        else if (type.TypeKind == TypeKind.Delegate)
        {
            ExportDelegate(type);
        }

        s++;
    }

    private void ExportConstructor(
        ref SourceBuilder s,
        ITypeSymbol type)
    {
        ConstructorInfo[] constructors = type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where((m) => m.MethodKind == MethodKind.Constructor &&
                m.DeclaredAccessibility == Accessibility.Public)
            .Select((c) => c.AsConstructorInfo())
            .ToArray();

        if (constructors.Length == 0)
        {
            s += $"\t() => throw new {typeof(JSException).Namespace}" +
                $".{typeof(JSException).Name}(" +
                $"\"Class '{type.Name}' does not have a public constructor.\"))";
        }
        else if (constructors.Length == 1 && constructors[0].GetParameters().Length == 0)
        {
            if (type.IsValueType)
            {
                s += $"\t(args) => args.ThisArg)";
            }
            else
            {
                s += $"\t() => new {GetFullName(type)}())";
            }
        }
        else if (constructors.Length == 1 &&
            constructors[0].GetParameters()[0].ParameterType == typeof(JSCallbackArgs))
        {
            s += $"\t(args) => new {GetFullName(type)}(args))";
        }
        else
        {
            // An adapter method supports arbitrary parameters or overloads.
            LambdaExpression adapter;
            if (constructors.Length == 1)
            {
                adapter = _marshaller.BuildFromJSConstructorExpression(constructors[0]);
                s += $"\t{adapter.Name})";
            }
            else
            {
                adapter = _marshaller.BuildConstructorOverloadDescriptorExpression(
                    constructors);
                s += $"\t{adapter.Name}())";
            }
            _callbackAdapters.Add(adapter.Name!, adapter);
        }
    }

    /// <summary>
    /// Generates code to define properties and methods for an exported class or struct type.
    /// </summary>
    private void ExportMembers(
      ref SourceBuilder s,
      ITypeSymbol type)
    {
        string propertyAttributes =
            $"{nameof(JSPropertyAttributes)}.{nameof(JSPropertyAttributes.Static)} | " +
            $"{nameof(JSPropertyAttributes)}.{nameof(JSPropertyAttributes.Enumerable)}";

        bool isStreamClass = typeof(System.IO.Stream).IsAssignableFrom(type.AsType());

        // TODO: If the base type is not exported, export members from the base type on this type?

        IEnumerable<ISymbol> members = type.GetMembers()
            .Where((m) => m.DeclaredAccessibility == Accessibility.Public)
            .Where((m) => !isStreamClass || m.IsStatic)
            .Where(IsExported);

        foreach (ISymbol member in members)
        {
            if (member is IPropertySymbol property)
            {
                if (property.Parameters.Any())
                {
                    ReportWarning(
                        DiagnosticId.UnsupportedIndexer,
                        property,
                        $"Exporting indexers is not supported.");
                }
                else
                {
                    ExportProperty(ref s, property, GetExportName(member));
                }
            }
            else if (type.TypeKind == TypeKind.Enum && member is IFieldSymbol field)
            {
                s += $".AddProperty(\"{field.Name}\", {field.ConstantValue}, {propertyAttributes})";
            }
            else if (member is INamedTypeSymbol nestedType)
            {
                string nestedTypeVariableName = "type_" + GetFullName(nestedType).Replace('.', '_');
                s += $".AddProperty(\"{GetExportName(nestedType)}\", {nestedTypeVariableName}, " +
                    $"{propertyAttributes})";
            }
        }

        foreach (IGrouping<string, IMethodSymbol> methodGroup in members
            .OfType<IMethodSymbol>().Where((m) => m.MethodKind == MethodKind.Ordinary)
            .GroupBy(GetExportName))
        {
            ExportMethod(ref s, methodGroup, methodGroup.Key);
        }
    }

    /// <summary>
    /// Generate code for a method exported on a class, struct, or module.
    /// </summary>
    private void ExportMethod(
      ref SourceBuilder s,
      IEnumerable<IMethodSymbol> methods,
      string exportName)
    {
        // TODO: Support exporting generic methods.
        methods = methods.Where((m) => !m.IsGenericMethod);

        IMethodSymbol? method = methods.FirstOrDefault();
        if (method == null)
        {
            return;
        }

        string attributes = "JSPropertyAttributes.DefaultMethod" +
            (method.IsStatic ? " | JSPropertyAttributes.Static" : string.Empty);

        if (methods.Count() == 1 && !IsMethodCallbackAdapterRequired(method))
        {
            // No adapter is needed for a method with a JSCallback signature.
            string typeFullName = GetFullName(method.ContainingType);
            s += $".AddMethod(\"{exportName}\", " +
                $"{typeFullName}.{method.Name},\n\t{attributes})";
        }
        else if (methods.Count() == 1)
        {
            // An adapter method supports marshalling arbitrary parameters.
            Expression<JSCallback> adapter =
                _marshaller.BuildFromJSMethodExpression(method.AsMethodInfo());
            _callbackAdapters.Add(adapter.Name!, adapter);
            s += $".AddMethod(\"{exportName}\", {adapter.Name},\n\t{attributes})";
        }
        else
        {
            // An adapter method provides overload resolution.
            LambdaExpression adapter = _marshaller.BuildMethodOverloadDescriptorExpression(
                methods.Select((m) => m.AsMethodInfo()).ToArray());
            _callbackAdapters.Add(adapter.Name!, adapter);
            s += $".AddMethod(\"{exportName}\", {adapter.Name}(),\n\t{attributes})";
        }
    }

    /// <summary>
    /// Generates code for a property exported on a class, struct, or module.
    /// </summary>
    private void ExportProperty(
      ref SourceBuilder s,
      IPropertySymbol property,
      string exportName)
    {
        bool writable = property.SetMethod != null ||
            (!property.IsStatic && property.ContainingType.TypeKind == TypeKind.Struct);
        string attributes = "JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable" +
            (writable ? " | JSPropertyAttributes.Writable" : string.Empty) +
            (property.IsStatic ? " | JSPropertyAttributes.Static" : string.Empty);

        if (property.ContainingType.TypeKind == TypeKind.Struct && !property.IsStatic)
        {
            // Struct instance properties are not backed by getter/setter methods. The entire
            // struct is always passed by value. Properties are converted to/from `JSValue` by
            // the struct adapter method.
            s += $".AddProperty(\"{exportName}\", {attributes})";
            return;
        }

        s += $".AddProperty(\"{exportName}\",";
        s.IncreaseIndent();

        string typeFullName = GetFullName(property.ContainingType);

        if (property.GetMethod?.DeclaredAccessibility != Accessibility.Public)
        {
            s += $"getter: () => default,";
        }
        else if (property.Type.AsType() != typeof(JSValue))
        {
            Expression<JSCallback> adapter =
                _marshaller.BuildFromJSMethodExpression(property.AsPropertyInfo().GetMethod!);
            _callbackAdapters.Add(adapter.Name!, adapter);
            s += $"getter: {adapter.Name},";
        }
        else if (property.IsStatic)
        {
            s += $"getter: () => {typeFullName}.{property.Name},";
        }
        else
        {
            s += $"getter: (obj) => obj.{property.Name},";
        }

        if (property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
        {
            s += $"setter: null,";
        }
        else if (property.Type.AsType() != typeof(JSValue))
        {
            Expression<JSCallback> adapter =
                _marshaller.BuildFromJSMethodExpression(property.AsPropertyInfo().SetMethod!);
            _callbackAdapters.Add(adapter.Name!, adapter);
            s += $"setter: {adapter.Name},";
        }
        else if (property.IsStatic)
        {
            s += $"setter: (value) => {typeFullName}.{property.Name} = value,";
        }
        else
        {
            s += $"setter: (obj, value) => obj.{property.Name} = value,";
        }

        s += $"{attributes})";

        s.DecreaseIndent();
    }

    private void ExportDelegate(ITypeSymbol delegateType)
    {
        MethodInfo delegateInvokeMethod = delegateType.AsType().GetMethod("Invoke")!;
        LambdaExpression fromAdapter = _marshaller.BuildFromJSFunctionExpression(
            delegateInvokeMethod);
        _callbackAdapters.Add(fromAdapter.Name!, fromAdapter);
        LambdaExpression toAapter = _marshaller.BuildToJSFunctionExpression(
            delegateInvokeMethod);
        _callbackAdapters.Add(toAapter.Name!, toAapter);
    }

    public static bool IsExported(ISymbol symbol)
    {
        AttributeData? exportAttribute = GetJSExportAttribute(symbol);

        // A private symbol with no [JSExport] attribute is not exported.
        if (exportAttribute == null && symbol.DeclaredAccessibility != Accessibility.Public)
        {
            return false;
        }

        // If the symbol doesn't have a [JSExport] attribute, check its containing type
        // and containing assembly.
        while (exportAttribute == null && symbol.ContainingType != null)
        {
            symbol = symbol.ContainingType;
            exportAttribute = GetJSExportAttribute(symbol);

            if (exportAttribute == null && symbol.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        if (exportAttribute == null)
        {
            exportAttribute = GetJSExportAttribute(symbol.ContainingAssembly);

            if (exportAttribute == null)
            {
                return false;
            }
        }

        // If the [JSExport] attribute has a single boolean constructor argument, use that.
        // Any other constructor defaults to true.
        TypedConstant constructorArgument = exportAttribute.ConstructorArguments.SingleOrDefault();
        return constructorArgument.Value as bool? ?? true;
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
            (a) => a.AttributeClass?.Name == typeof(JSExportAttribute).Name &&
                a.AttributeClass.ContainingNamespace.ToDisplayString() ==
                    typeof(JSExportAttribute).Namespace);
    }

    /// <summary>
    /// Checks whether an adapter must be generated for a method. An adapter is unnecessary if
    /// the method takes either no parameters or a single JSCallbackArgs parameter and returns
    /// either void or JSValue.
    /// </summary>
    private bool IsMethodCallbackAdapterRequired(IMethodSymbol method)
    {
        if (method.IsStatic &&
            (method.Parameters.Length == 0 ||
            (method.Parameters.Length == 1 &&
            GetFullName(method.Parameters[0].Type) == typeof(JSCallbackArgs).FullName)) &&
            method.Parameters.All((p) => p.RefKind == RefKind.None) &&
            (method.ReturnsVoid ||
            GetFullName(method.ReturnType) == typeof(JSValue).FullName))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Generates a class that implements an interface by making calls to a JS value.
    /// </summary>
    private static void GenerateInterfaceAdapter(
        ref SourceBuilder s,
        ITypeSymbol interfaceType,
        JSMarshaller _marshaller)
    {
        string ns = GetNamespace(interfaceType);
        if (ns.Length > 0)
        {
            ns += '_';
        }

        string adapterName = $"proxy_{ns.Replace('.', '_')}{interfaceType.Name}";

        static string ReplaceMethodVariables(string cs) =>
            cs.Replace(typeof(JSValue).Namespace + ".", "")
            .Replace("__value", "value");

        /*
         * private sealed class proxy_IInterfaceName : JSInterface, IInterfaceName
         * {
         *     public proxy_IInterfaceName(JSValue value) : base(value) { }
         *
         */
        s += $"private sealed class {adapterName} : JSInterface, {interfaceType}";
        s += "{";
        s += $"public {adapterName}(JSValue value) : base(value) {{ }}";

        foreach (ISymbol member in GetMembers(interfaceType, includeBaseMembers: true))
        {
            if (member is IPropertySymbol property)
            {
                s++;
                s += $"{property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)} " +
                    $"{GetFullName(member.ContainingType)}.{property.Name}";
                s += "{";

                if (!property.IsWriteOnly)
                {
                    LambdaExpression getterAdapter =
                        _marshaller.BuildToJSPropertyGetExpression(property.AsPropertyInfo());
                    s += "get";
                    string cs = ReplaceMethodVariables(
                        _marshaller.MakeInterfaceExpression(getterAdapter).ToCS());
                    s += string.Join("\n", cs.Split('\n').Skip(1));
                }

                if (!property.IsReadOnly)
                {
                    LambdaExpression setterAdapter =
                        _marshaller.BuildToJSPropertySetExpression(property.AsPropertyInfo());
                    s += "set";
                    string cs = ReplaceMethodVariables(
                        _marshaller.MakeInterfaceExpression(setterAdapter).ToCS());
                    s += string.Join("\n", cs.Split('\n').Skip(1));
                }

                s += "}";
            }
            else if (member is IMethodSymbol method &&
                method.MethodKind == MethodKind.Ordinary)
            {
                s++;

                if (method.IsGenericMethod)
                {
                    // Invoking a generic method implemented by JS requires dynamic
                    // marshalling because the generic type arguments are not known
                    // ahead of time. This does not work in an AOT-compiled executable.

                    MethodInfo methodInfo = method.AsMethodInfo();
                    s += $"{ExpressionExtensions.FormatType(methodInfo.ReturnType)} " +
                        $"{GetFullName(method)}<" +
                        string.Join(", ", method.TypeParameters.Select((t) => t.Name)) +
                        ">(" + string.Join(", ", methodInfo.GetParameters().Select((p) =>
                            $"{ExpressionExtensions.FormatType(p.ParameterType)} {p.Name}")) + ")";
                    s += "{";

                    // The build-time generated dynamic marshalling code here is similar to
                    // that generated at runtime by `JSInterfaceMarshaller` when dynamic-binding.
                    IEnumerable<ITypeSymbol> typeArgs = method.TypeArguments;
                    s += "var currentMethod = (System.Reflection.MethodInfo)" +
                        "System.Reflection.MethodBase.GetCurrentMethod();";
                    s += $"currentMethod = currentMethod.{nameof(MethodInfo.MakeGenericMethod)}(" +
                        string.Join(", ", typeArgs.Select((t) => $"typeof({t.Name})")) + ");";
                    s += $"var jsMarshaller = {typeof(JSMarshaller).Namespace}." +
                        $"{nameof(JSMarshaller)}.{nameof(JSMarshaller.Current)};";

                    s += $"return ValueReference.Run((__this) => {{";

                    s += $"return ({GetFullName(method.ReturnType)})" +
                        $"jsMarshaller.{nameof(JSMarshaller.GetToJSMethodDelegate)}" +
                        $"(currentMethod).DynamicInvoke(__this, " +
                        string.Join(", ", method.Parameters.Select((p) => p.Name)) + ");";

                    s += "});";

                    s += "}";
                }
                else
                {
                    LambdaExpression methodAdapter = _marshaller.MakeInterfaceExpression(
                        _marshaller.BuildToJSMethodExpression(method.AsMethodInfo()));
                    s += ReplaceMethodVariables(methodAdapter.ToCS());
                }
            }
        }

        s += "}";
    }

    private static IEnumerable<ISymbol> GetMembers(ITypeSymbol typeSymbol, bool includeBaseMembers)
    {
        foreach (ISymbol member in typeSymbol.GetMembers())
        {
            yield return member;
        }

        // Exclude members from System.Object.
        if (includeBaseMembers && typeSymbol.TypeKind == TypeKind.Class &&
            typeSymbol.BaseType?.BaseType != null)
        {
            foreach (ISymbol member in GetMembers(typeSymbol.BaseType, includeBaseMembers: true))
            {
                yield return member;
            }
        }
        else if (includeBaseMembers && typeSymbol.TypeKind == TypeKind.Interface)
        {
            foreach (ITypeSymbol interfaceSymbol in typeSymbol.AllInterfaces)
            {
                foreach (ISymbol member in GetMembers(interfaceSymbol, includeBaseMembers: true))
                {
                    yield return member;
                }
            }
        }
    }

    /// <summary>
    /// Generate supporting adapter methods that the module initialization depended on.
    /// </summary>
    private void GenerateCallbackAdapters(ref SourceBuilder s)
    {
        // First search through the callback expression trees to collect any additional
        // adapter lambdas that they depend on, so the dependencies can be generated also.
        CallbackAdapterCollector callbackAdapterFinder = new(_callbackAdapters);
        foreach (LambdaExpression? callbackAdapter in _callbackAdapters.Values.ToArray())
        {
            callbackAdapterFinder.Visit(callbackAdapter);
        }

        foreach (LambdaExpression callbackAdapter in _callbackAdapters.Values)
        {
            s++;
            s += "private static " + callbackAdapter.ToCS();
        }
    }

    private class CallbackAdapterCollector : ExpressionVisitor
    {
        private readonly Dictionary<string, LambdaExpression> _callbackAdapters;

        public CallbackAdapterCollector(
            Dictionary<string, LambdaExpression> callbackAdapters)
        {
            _callbackAdapters = callbackAdapters;
        }

        protected override Expression VisitInvocation(InvocationExpression node)
        {
            if (node.Expression is LambdaExpression callbackAdapter &&
                !_callbackAdapters.ContainsKey(callbackAdapter.Name!))
            {
                _callbackAdapters.Add(callbackAdapter.Name!, callbackAdapter);
            }

            return base.VisitInvocation(node);
        }
    }
}
