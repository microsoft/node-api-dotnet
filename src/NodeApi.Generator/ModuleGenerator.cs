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
                        "[JSModule] attribute must be applied to a class.");
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
                if (type.TypeKind != TypeKind.Class &&
                    type.TypeKind != TypeKind.Struct &&
                    type.TypeKind != TypeKind.Interface &&
                    type.TypeKind != TypeKind.Delegate &&
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
            else if (type.TypeKind == TypeKind.Class || type.TypeKind == TypeKind.Struct)
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

            string ns = GetNamespace(moduleInitializerMethod);
            string className = moduleInitializerMethod.ContainingType.Name;
            string methodName = moduleInitializerMethod.Name;
            s += $"return (napi_value){ns}.{className}.{methodName}(";
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
            string ns = GetNamespace(moduleType);
            s += $"exportsValue = new JSModuleBuilder<{ns}.{moduleType.Name}>()";
            s.IncreaseIndent();

            // Export non-static members of the module class.
            foreach (ISymbol? member in moduleType.GetMembers()
              .Where((m) => m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic))
            {
                if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
                {
                    ExportMethod(ref s, method);
                }
                else if (member is IPropertySymbol property)
                {
                    ExportProperty(ref s, property);
                }
            }
        }
        else
        {
            s += $"exportsValue = new JSModuleBuilder<JSRuntimeContext>()";
            s.IncreaseIndent();
        }

        // Export types and functions (static methods) tagged with [JSExport]
        foreach (ISymbol exportItem in exportItems)
        {
            string exportName = GetExportName(exportItem);
            if (exportItem is ITypeSymbol exportType)
            {
                ExportType(ref s, exportType, exportName);
            }
            else if (exportItem is IPropertySymbol exportProperty)
            {
                // Export tagged static properties as properties on the module.
                ExportProperty(ref s, exportProperty, exportName);
            }
            else if (exportItem is IMethodSymbol exportMethod)
            {
                // Export tagged static methods as top-level functions on the module.
                ExportMethod(ref s, exportMethod, exportName);
            }
            else if (exportItem is ITypeSymbol exportDelegate &&
                exportDelegate.TypeKind == TypeKind.Delegate)
            {
                ExportDelegate(exportDelegate);
            }
        }

        if (moduleType != null)
        {
            // Construct an instance of the custom module class when the module is initialized.
            // If a no-args constructor is not present then the generated code will not compile.
            string ns = GetNamespace(moduleType);
            s += $".ExportModule(new {ns}.{moduleType.Name}(), (JSObject)exportsValue);";
        }
        else
        {
            s += $".ExportModule(context, (JSObject)exportsValue);";
        }

        s.DecreaseIndent();
    }

    /// <summary>
    /// Generates code to export a class, struct, interface, enum, or delegate type.
    /// </summary>
    private void ExportType(
        ref SourceBuilder s,
        ITypeSymbol type,
        string? exportName = null)
    {
        exportName ??= type.Name;

        string propertyAttributes = string.Empty;
        if (type.ContainingType != null)
        {
            propertyAttributes = ", JSPropertyAttributes.Static | " +
                "JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable";
        }

        if (type.TypeKind == TypeKind.Class ||
            type.TypeKind == TypeKind.Interface)
        {
            s += $".AddProperty(\"{exportName}\",";
            s.IncreaseIndent();

            string ns = GetNamespace(type);
            if (type.TypeKind == TypeKind.Interface)
            {
                // Interfaces do not have constructors.
                s += $"new JSClassBuilder<{GetFullName(type)}>(\"{exportName}\")";
                _exportedInterfaces.Add(type);
            }
            else if (type.IsStatic)
            {
                // Static classes do not have constructors, and cannot be used as type params.
                s += $"new JSClassBuilder<object>(\"{exportName}\")";
            }
            else
            {
                s += $"new JSClassBuilder<{GetFullName(type)}>(\"{exportName}\",";

                // The class constructor may take no parameter, or a single JSCallbackArgs
                // parameter, or may use an adapter to support arbitrary parameters.
                if (IsConstructorCallbackAdapterRequired(type))
                {
                    LambdaExpression adapter;
                    ConstructorInfo[] constructors = type.GetMembers()
                        .OfType<IMethodSymbol>()
                        .Where((m) => m.MethodKind == MethodKind.Constructor)
                        .Select((c) => c.AsConstructorInfo())
                        .ToArray();
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
                else if (type.GetMembers().OfType<IMethodSymbol>().Any((m) =>
                    m.MethodKind == MethodKind.Constructor && m.Parameters.Length == 0))
                {
                    s += $"\t() => new {ns}.{type.Name}())";
                }
                else
                {
                    s += $"\t(args) => new {ns}.{type.Name}(args))";
                }
            }

            // Export all the class members, then define the class.
            ExportMembers(ref s, type);

            s += (type.TypeKind == TypeKind.Interface ? ".DefineInterface()" :
                type.IsStatic ? ".DefineStaticClass()" : ".DefineClass()") +
                propertyAttributes + ')';
            s.DecreaseIndent();
        }
        else if (type.TypeKind == TypeKind.Struct)
        {
            s += $".AddProperty(\"{exportName}\",";
            s.IncreaseIndent();

            string ns = GetNamespace(type);
            s += $"new JSStructBuilder<{GetFullName(type)}>(\"{exportName}\")";

            ExportMembers(ref s, type);
            s += $".DefineStruct(){propertyAttributes})";
            s.DecreaseIndent();
        }
        else if (type.TypeKind == TypeKind.Enum)
        {
            s += $".AddProperty(\"{exportName}\",";
            s.IncreaseIndent();

            // Exported enums are similar to static classes with integer properties.
            s += $"new JSClassBuilder<object>(\"{exportName}\")";
            ExportMembers(ref s, type);
            s += $".DefineEnum(){propertyAttributes})";
            s.DecreaseIndent();
        }
        else if (type.TypeKind == TypeKind.Delegate)
        {
            ExportDelegate(type);
        }
    }

    /// <summary>
    /// Generates code to define properties and methods for an exported class or struct type.
    /// </summary>
    private void ExportMembers(
      ref SourceBuilder s,
      ITypeSymbol type)
    {
        bool isStreamClass = typeof(System.IO.Stream).IsAssignableFrom(type.AsType());

        foreach (ISymbol member in type.GetMembers()
          .Where((m) => m.DeclaredAccessibility == Accessibility.Public))
        {
            if (isStreamClass && !member.IsStatic)
            {
                // Only static members on stream subclasses are exported to JS.
                continue;
            }

            if (member is IMethodSymbol method && method.MethodKind == MethodKind.Ordinary)
            {
                ExportMethod(ref s, method);
            }
            else if (member is IPropertySymbol property)
            {
                ExportProperty(ref s, property);
            }
            else if (type.TypeKind == TypeKind.Enum && member is IFieldSymbol field)
            {
                s += $".AddProperty(\"{field.Name}\", {field.ConstantValue}, " +
                    "JSPropertyAttributes.Static | JSPropertyAttributes.Enumerable)";
            }
            else if (member is INamedTypeSymbol nestedType)
            {
                ExportType(ref s, nestedType);
            }
        }
    }

    /// <summary>
    /// Generate code for a method exported on a class, struct, or module.
    /// </summary>
    private void ExportMethod(
      ref SourceBuilder s,
      IMethodSymbol method,
      string? exportName = null)
    {
        exportName ??= ToCamelCase(method.Name);

        // An adapter method may be used to support marshalling arbitrary parameters,
        // if the method does not match the `JSCallback` signature.
        string attributes = "JSPropertyAttributes.DefaultMethod" +
            (method.IsStatic ? " | JSPropertyAttributes.Static" : string.Empty);
        if (method.IsGenericMethod)
        {
            // TODO: Export generic method.
        }
        else if (IsMethodCallbackAdapterRequired(method))
        {
            Expression<JSCallback> adapter =
                _marshaller.BuildFromJSMethodExpression(method.AsMethodInfo());
            _callbackAdapters.Add(adapter.Name!, adapter);
            s += $".AddMethod(\"{exportName}\", {adapter.Name},\n\t{attributes})";
        }
        else
        {
            string ns = GetNamespace(method);
            string className = method.ContainingType.Name;
            s += $".AddMethod(\"{exportName}\", " +
                $"{ns}.{className}.{method.Name},\n\t{attributes})";
        }
    }

    /// <summary>
    /// Generates code for a property exported on a class, struct, or module.
    /// </summary>
    private void ExportProperty(
      ref SourceBuilder s,
      IPropertySymbol property,
      string? exportName = null)
    {
        exportName ??= ToCamelCase(property.Name);

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

        string ns = GetNamespace(property);
        string className = property.ContainingType.Name;

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
            s += $"getter: () => {ns}.{className}.{property.Name},";
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
            s += $"setter: (value) => {ns}.{className}.{property.Name} = value,";
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

    /// <summary>
    /// Checks whether an adapter must be generated for a constructor. An adapter is unnecessary
    /// if the constructor takes either no parameters or a single JSCallbackArgs parameter.
    /// </summary>
    private bool IsConstructorCallbackAdapterRequired(ITypeSymbol type)
    {
        IMethodSymbol[] constructors = type.GetMembers().OfType<IMethodSymbol>()
            .Where((m) => m.MethodKind == MethodKind.Constructor)
            .ToArray();
        if (constructors.Length > 1)
        {
            return true;
        }

        if (constructors.Length == 0 || constructors.Any((c) => c.Parameters.Length == 0 ||
            (c.Parameters.Length == 1 && c.Parameters[0].Type.AsType() == typeof(JSCallbackArgs))))
        {
            return false;
        }

        return true;
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
            method.Parameters[0].Type.AsType() == typeof(JSCallbackArgs))) &&
            method.Parameters.All((p) => p.RefKind == RefKind.None) &&
            (method.ReturnsVoid ||
            method.ReturnType.AsType() == typeof(JSValue)))
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
        string adapterName = $"proxy_{ns.Replace('.', '_')}_{interfaceType.Name}";

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

        foreach (ISymbol member in interfaceType.GetMembers())
        {
            if (member is IPropertySymbol property)
            {
                s++;
                s += $"{property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)} " +
                    $"{GetFullName(interfaceType)}.{property.Name}";
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
