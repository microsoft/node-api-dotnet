using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
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

    private readonly JSMarshaler _marshaler = new();
    private readonly Dictionary<string, LambdaExpression> _callbackAdapters = new();
    private readonly List<ITypeSymbol> _exportedInterfaces = new();

    public void Initialize(GeneratorInitializationContext context)
    {
#if DEBUG
#pragma warning disable RS1035 // The symbol 'Environment' is banned for use by analyzers.
        // Note source generators are not covered by normal debugging,
        // because the generator runs at build time, not at application run-time.
        // Set the environment variable to trigger debugging at build time.

        if (Environment.GetEnvironmentVariable("DEBUG_NODE_API_GENERATOR") != null)
        {
            System.Diagnostics.Debugger.Launch();
        }
#pragma warning restore RS1035
#endif
    }

    public void Execute(GeneratorExecutionContext context)
    {
        Context = context;
        string generatedSourceFileName =
            (context.Compilation.AssemblyName ?? "Assembly") + ".NodeApi.g.cs";
        try
        {
            ISymbol? moduleInitializer = GetModuleInitializer();
            IEnumerable<ISymbol> exportItems = GetModuleExportItems();

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

                // TODO: Check for a public constructor that takes a single JSContext argument.

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
                if (type.TypeKind == TypeKind.Delegate)
                {
                    ReportError(
                        DiagnosticId.UnsupportedTypeKind,
                        type,
                        "Exporting delegates is not currently supported.");
                }
                else if (type.TypeKind != TypeKind.Class &&
                    type.TypeKind != TypeKind.Struct &&
                    type.TypeKind != TypeKind.Interface &&
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
    private SourceText GenerateModuleInitializer(
      ISymbol? moduleInitializer,
      IEnumerable<ISymbol> exportItems)
    {
        var s = new SourceBuilder();

        s += "using System.CodeDom.Compiler;";
        s += "using System.Runtime.InteropServices;";
        s += "using Microsoft.JavaScript.NodeApi.Interop;";
        s += "using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;";
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

        // The unmanaged entrypoint is used only when the AOT-compiled module is loaded.
        s += $"[UnmanagedCallersOnly(EntryPoint = \"{ModuleRegisterFunctionName}\")]";
        s += $"public static napi_value _{ModuleInitializeMethodName}(napi_env env, napi_value exports)";
        s += $"{s.Indent}=> {ModuleInitializeMethodName}(env, exports);";
        s++;

        // The main initialization entrypoint is called by the `ManagedHost`, and by the unmanaged entrypoint.
        s += $"public static napi_value {ModuleInitializeMethodName}(napi_env env, napi_value exports)";
        s += "{";
        s += "using var scope = new JSValueScope(JSValueScopeType.Root, env);";
        s += "try";
        s += "{";
        s += "JSContext context = scope.ModuleContext;";
        s += "JSValue exportsValue = new(exports, scope);";
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
        s += "return exports;";
        s += "}";
        s += "}";

        GenerateCallbackAdapters(ref s);

        foreach (ITypeSymbol interfaceSymbol in _exportedInterfaces)
        {
            s++;
            GenerateInterfaceAdapter(ref s, interfaceSymbol, _marshaler);
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
            s += $"exportsValue = new JSModuleBuilder<JSContext>()";
            s.IncreaseIndent();
        }

        // Export types and functions (static methods) tagged with [JSExport]
        foreach (ISymbol exportItem in exportItems)
        {
            string exportName = GetExportName(exportItem);
            if (exportItem is ITypeSymbol exportClass &&
                (exportClass.TypeKind == TypeKind.Class ||
                exportClass.TypeKind == TypeKind.Interface))
            {
                s += $".AddProperty(\"{exportName}\",";
                s.IncreaseIndent();

                string ns = GetNamespace(exportClass);
                if (exportClass.TypeKind == TypeKind.Interface)
                {
                    // Interfaces do not have constructors.
                    s += $"new JSClassBuilder<{exportClass}>(\"{exportName}\")";
                    _exportedInterfaces.Add(exportClass);
                }
                else if (exportClass.IsStatic)
                {
                    // Static classes do not have constructors, and cannot be used as type params.
                    s += $"new JSClassBuilder<object>(\"{exportName}\")";
                }
                else
                {
                    s += $"new JSClassBuilder<{ns}.{exportClass.Name}>(\"{exportName}\",";

                    // The class constructor may take no parameter, or a single JSCallbackArgs
                    // parameter, or may use an adapter to support arbitrary parameters.
                    if (IsConstructorCallbackAdapterRequired(exportClass))
                    {
                        // TODO: Overload resolution if more than one constructor.
                        LambdaExpression adapter = _marshaler.BuildFromJSConstructorExpression(
                            exportClass.GetMembers().OfType<IMethodSymbol>()
                                .Where((m) => m.MethodKind == MethodKind.Constructor)
                                .First().AsConstructorInfo());
                        s += $"\t{adapter.Name})";
                    }
                    else if (exportClass.GetMembers().OfType<IMethodSymbol>().Any((m) =>
                        m.MethodKind == MethodKind.Constructor && m.Parameters.Length == 0))
                    {
                        s += $"\t() => new {ns}.{exportClass.Name}())";
                    }
                    else
                    {
                        s += $"\t(args) => new {ns}.{exportClass.Name}(args))";
                    }
                }

                // Export all the class members, then define the class.
                ExportMembers(ref s, exportClass);
                s += exportClass.TypeKind == TypeKind.Interface ? ".DefineInterface())" :
                    exportClass.IsStatic ? ".DefineStaticClass())" : ".DefineClass())";
                s.DecreaseIndent();
            }
            else if (exportItem is ITypeSymbol exportStruct &&
                exportStruct.TypeKind == TypeKind.Struct)
            {
                s += $".AddProperty(\"{exportName}\",";
                s.IncreaseIndent();

                string ns = GetNamespace(exportStruct);
                s += $"new JSStructBuilder<{ns}.{exportStruct.Name}>(\"{exportName}\")";

                ExportMembers(ref s, exportStruct);
                s += ".DefineStruct())";
                s.DecreaseIndent();
            }
            else if (exportItem is ITypeSymbol exportEnum && exportEnum.TypeKind == TypeKind.Enum)
            {
                s += $".AddProperty(\"{exportName}\",";
                s.IncreaseIndent();

                // Exported enums are similar to static classes with integer properties.
                s += $"new JSClassBuilder<object>(\"{exportName}\")";
                ExportMembers(ref s, exportEnum);
                s += ".DefineEnum())";
                s.DecreaseIndent();
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
    private void ExportMembers(
      ref SourceBuilder s,
      ITypeSymbol type)
    {
        foreach (ISymbol? member in type.GetMembers()
          .Where((m) => m.DeclaredAccessibility == Accessibility.Public))
        {
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
        if (IsMethodCallbackAdapterRequired(method))
        {
            Expression<JSCallback> adapter =
                _marshaler.BuildFromJSMethodExpression(method.AsMethodInfo());
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
                _marshaler.BuildFromJSMethodExpression(property.AsPropertyInfo().GetMethod!);
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
                _marshaler.BuildFromJSMethodExpression(property.AsPropertyInfo().SetMethod!);
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
        if (!constructors.Any() || constructors.Any((c) => c.Parameters.Length == 0 ||
            (c.Parameters.Length == 1 && c.Parameters[0].Type.AsType() == typeof(JSCallbackArgs))))
        {
            return false;
        }

        // TODO: Look for [JSExport] attribute among multiple constructors, and/or
        // implement overload resolution in the adapter.
        if (constructors.Length > 1)
        {
            ReportError(
                DiagnosticId.UnsupportedOverloads,
                constructors.Skip(1).First(),
                "Exported class cannot have an overloaded constructor.");
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
            (method.ReturnsVoid ||
            method.ReturnType.AsType() == typeof(JSValue)))
        {
            return false;
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
            {
                ReportError(
                    DiagnosticId.UnsupportedMethodParameterType,
                    parameter,
                    "Parameters with 'ref' or 'out' modifiers are not supported " +
                        "in exported methods.");
            }
        }

        return true;
    }

    /// <summary>
    /// Generates a class that implements an interface by making calls to a JS value.
    /// </summary>
    private static void GenerateInterfaceAdapter(
        ref SourceBuilder s,
        ITypeSymbol interfaceType,
        JSMarshaler _marshaler)
    {
        string ns = GetNamespace(interfaceType);
        string adapterName = $"proxy_{ns.Replace('.', '_')}_{interfaceType.Name}";

        static string ReplaceMethodVariables(string cs) =>
            cs.Replace(typeof(JSValue).Namespace + ".", "")
            .Replace("JSValue __this, ", "")
            .Replace("__this", "Value")
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
                        _marshaler.BuildToJSPropertyGetExpression(property.AsPropertyInfo());
                    s += "get";
                    string cs = ReplaceMethodVariables(getterAdapter.ToCS());
                    s += string.Join("\n", cs.Split("\n").Skip(1));
                }

                if (!property.IsReadOnly)
                {
                    LambdaExpression setterAdapter =
                        _marshaler.BuildToJSPropertySetExpression(property.AsPropertyInfo());
                    s += "set";
                    string cs = ReplaceMethodVariables(setterAdapter.ToCS());
                    s += string.Join("\n", cs.Split("\n").Skip(1));
                }

                s += "}";
            }
            else if (member is IMethodSymbol method &&
                method.MethodKind == MethodKind.Ordinary)
            {
                s++;

                LambdaExpression methodAdapter =
                    _marshaler.BuildToJSMethodExpression(method.AsMethodInfo());
                s += ReplaceMethodVariables(methodAdapter.ToCS());
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
