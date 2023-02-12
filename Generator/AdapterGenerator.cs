using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NodeApi.Generator;

// An analyzer bug results in incorrect reports of CA1822 against methods in this class.
#pragma warning disable CA1822 // Mark members as static

/// <summary>
/// Generates adapter methods for C# members exported to JavaScript.
/// </summary>
internal class AdapterGenerator : SourceGenerator
{
    private const string AdapterGetPrefix = "get_";
    private const string AdapterSetPrefix = "set_";
    private const string AdapterConstructorPrefix = "new_";
    private const string AdapterFromPrefix = "from_";
    private const string AdapterToPrefix = "to_";

    private readonly Dictionary<string, ISymbol> _adaptedMembers = new();
    private readonly Dictionary<string, ITypeSymbol> _adaptedStructs = new();
    private readonly Dictionary<string, ITypeSymbol> _adaptedArrays = new();

    internal AdapterGenerator(GeneratorExecutionContext context)
    {
        Context = context;
    }

    internal static bool HasNoArgsConstructor(ITypeSymbol type)
    {
        return type.GetMembers().OfType<IMethodSymbol>()
            .Any((m) => m.MethodKind == MethodKind.Constructor && m.Parameters.Length == 0);
    }

    internal string? GetConstructorAdapterName(ITypeSymbol type)
    {
        IMethodSymbol[] constructors = type.GetMembers().OfType<IMethodSymbol>()
            .Where((m) => m.MethodKind == MethodKind.Constructor)
            .ToArray();
        if (!constructors.Any() || constructors.Any((c) => c.Parameters.Length == 0 ||
            (c.Parameters.Length == 1 && c.Parameters[0].Type.Name == "JSCallbackArgs")))
        {
            return null;
        }

        // TODO: Look for [JSExport] attribute among multiple constructors?
        if (constructors.Length > 1)
        {
            ReportError(
                DiagnosticId.UnsupportedOverloads,
                constructors.Skip(1).First(),
                "Exported class must cannot have an overloaded constructor.");
        }

        IMethodSymbol constructor = constructors.Single();
        string ns = GetNamespace(constructor);
        string className = type.Name;
        string adapterName = $"{AdapterConstructorPrefix}{ns.Replace('.', '_')}_{className}";
        _adaptedMembers.Add(adapterName, constructor);
        return adapterName;
    }

    internal string? GetMethodAdapterName(IMethodSymbol method)
    {
        // TODO: Check full type names.
        if ((method.Parameters.Length == 0 ||
            (method.Parameters.Length == 1 &&
            method.Parameters[0].Type.Name == "JSCallbackArgs")) &&
            (method.ReturnsVoid ||
            method.ReturnType.Name == "JSValue"))
        {
            return null;
        }

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None)
            {
                ReportError(
                    DiagnosticId.UnsupportedMethodParameterType,
                    parameter,
                    "Parameters with 'ref' or 'out' modifiers are not supported in exported methods.");
            }
        }

        string ns = GetNamespace(method);
        string className = method.ContainingType.Name;
        string adapterName = $"{ns.Replace('.', '_')}_{className}_{method.Name}";
        _adaptedMembers.Add(adapterName, method);
        return adapterName;
    }

    private string GetStructAdapterName(ITypeSymbol structType, bool toJS)
    {
        string ns = GetNamespace(structType);
        string structName = structType.Name;
        string prefix = toJS ? AdapterFromPrefix : AdapterToPrefix;
        string adapterName = $"{prefix}{ns.Replace('.', '_')}_{structName}";
        if (!_adaptedStructs.ContainsKey(adapterName))
        {
            _adaptedStructs.Add(adapterName, structType);
        }
        return adapterName;
    }

    internal (string?, string?) GetPropertyAdapterNames(IPropertySymbol property)
    {
        // TODO: Check full type name.
        if (property.Type.Name == "JSValue")
        {
            return (null, null);
        }

        string ns = GetNamespace(property);
        string className = property.ContainingType.Name;

        string? getAdapterName = null;
        if (property?.GetMethod?.DeclaredAccessibility == Accessibility.Public)
        {
            getAdapterName = $"{AdapterGetPrefix}{ns.Replace('.', '_')}_{className}_{property.Name}";
            _adaptedMembers.Add(getAdapterName, property);
        }

        string? setAdapterName = null;
        if (property?.SetMethod?.DeclaredAccessibility == Accessibility.Public)
        {
            setAdapterName = $"{AdapterSetPrefix}{ns.Replace('.', '_')}_{className}_{property.Name}";
            _adaptedMembers.Add(setAdapterName, property);
        }

        return (getAdapterName, setAdapterName);
    }

    private string GetArrayAdapterName(ITypeSymbol elementType, bool toJS)
    {
        string ns = GetNamespace(elementType);
        string elementName = elementType.Name;
        string prefix = toJS ? AdapterFromPrefix : AdapterToPrefix;
        string adapterName = $"{prefix}{ns.Replace('.', '_')}_{elementName}_Array";
        if (!_adaptedArrays.ContainsKey(adapterName))
        {
            _adaptedArrays.Add(adapterName, elementType);
        }
        return adapterName;
    }

    internal void GenerateAdapters(SourceBuilder s)
    {
        foreach (KeyValuePair<string, ISymbol> nameAndSymbol in _adaptedMembers)
        {
            s++;

            string adapterName = nameAndSymbol.Key;
            ISymbol symbol = nameAndSymbol.Value;
            if (symbol is IMethodSymbol method)
            {
                if (method.MethodKind == MethodKind.Constructor)
                {
                    GenerateConstructorAdapter(ref s, adapterName, method);
                }
                else
                {
                    GenerateMethodAdapter(ref s, adapterName, method);
                }
            }
            else
            {
                GeneratePropertyAdapter(ref s, adapterName, (IPropertySymbol)symbol);
            }
        }

        foreach (KeyValuePair<string, ITypeSymbol> nameAndSymbol in _adaptedStructs)
        {
            s++;
            string adapterName = nameAndSymbol.Key;
            ITypeSymbol structSymbol = nameAndSymbol.Value;
            GenerateStructAdapter(ref s, adapterName, structSymbol);
        }

        foreach (KeyValuePair<string, ITypeSymbol> nameAndSymbol in _adaptedArrays)
        {
            s++;
            string adapterName = nameAndSymbol.Key;
            ITypeSymbol elementSymbol = nameAndSymbol.Value;
            GenerateArrayAdapter(ref s, adapterName, elementSymbol);
        }
    }

    private void GenerateConstructorAdapter(
        ref SourceBuilder s,
        string adapterName,
        IMethodSymbol constructor)
    {
        string ns = GetNamespace(constructor);
        string className = constructor.ContainingType.Name;

        s += $"private static {ns}.{className} {adapterName}(JSCallbackArgs __args)";
        s += "{";

        IReadOnlyList<IParameterSymbol> parameters = constructor.Parameters;
        for (int i = 0; i < parameters.Count; i++)
        {
            AdaptArgument(ref s, parameters[i].Type, parameters[i].Name, i);
        }

        string argumentList = string.Join(", ", parameters.Select((p) => p.Name));

        s += $"return new {ns}.{className}({argumentList});";
        s += "}";
    }

    private void GenerateMethodAdapter(
        ref SourceBuilder s,
        string adapterName,
        IMethodSymbol method)
    {
        s += $"private static JSValue {adapterName}(JSCallbackArgs __args)";
        s += "{";

        if (!method.IsStatic)
        {
            AdaptThisArg(ref s, method);
        }

        IReadOnlyList<IParameterSymbol> parameters = method.Parameters;
        for (int i = 0; i < parameters.Count; i++)
        {
            AdaptArgument(ref s, parameters[i].Type, parameters[i].Name, i);
        }

        string argumentList = string.Join(", ", parameters.Select((p) => p.Name));
        string returnAssignment = method.ReturnsVoid ? string.Empty : "var __result = ";

        string ns = GetNamespace(method);
        string className = method.ContainingType.Name;

        if (method.IsStatic)
        {
            s += $"{returnAssignment}{ns}.{className}.{method.Name}({argumentList});";
        }
        else
        {
            s += $"{returnAssignment}__obj.{method.Name}({argumentList});";
        }

        if (method.ReturnsVoid)
        {
            s += "return JSValue.Undefined;";
        }
        else
        {
            AdaptReturnValue(ref s, method.ReturnType);
        }

        s += "}";
    }

    private void GeneratePropertyAdapter(
        ref SourceBuilder s,
        string adapterName,
        IPropertySymbol property)
    {
        string ns = GetNamespace(property);
        string className = property.ContainingType.Name;

        if (adapterName.StartsWith(AdapterGetPrefix))
        {
            s += $"private static JSValue {adapterName}(JSCallbackArgs __args)";
            s += "{";

            if (property.IsStatic)
            {
                s += $"var __result = {ns}.{className}.{property.Name};";
            }
            else
            {
                AdaptThisArg(ref s, property);
                s += $"var __result = __obj.{property.Name};";
            }

            AdaptReturnValue(ref s, property.Type);
            s += "}";
        }
        else
        {
            s += $"private static JSValue {adapterName}(JSCallbackArgs __args)";
            s += "{";

            if (property.IsStatic)
            {
                AdaptArgument(ref s, property.Type, "__value", 0);
                s += $"{ns}.{className}.{property.Name} = __value;";
            }
            else
            {
                AdaptThisArg(ref s, property);
                AdaptArgument(ref s, property.Type, "__value", 0);
                s += $"__obj.{property.Name} = __value;";
            }

            s += "return JSValue.Undefined;";
            s += "}";
        }
    }

    private void GenerateStructAdapter(
        ref SourceBuilder s,
        string adapterName,
        ITypeSymbol structType)
    {
        List<ISymbol> copyableProperties = new();
        foreach (ISymbol member in structType.GetMembers()
            .Where((m) => !m.IsStatic && m.DeclaredAccessibility == Accessibility.Public))
        {
            if (member.Kind == SymbolKind.Property &&
                ((IPropertySymbol)member).GetMethod?.DeclaredAccessibility == Accessibility.Public &&
                ((IPropertySymbol)member).SetMethod?.DeclaredAccessibility == Accessibility.Public)
            {
                copyableProperties.Add(member);
            }
            else if (member.Kind == SymbolKind.Field &&
                !((IFieldSymbol)member).IsConst &&
                !((IFieldSymbol)member).IsReadOnly)
            {
                copyableProperties.Add(member);
            }
        }

        string ns = GetNamespace(structType);
        string structName = structType.Name;

        if (adapterName.StartsWith(AdapterFromPrefix))
        {
            s += $"private static JSValue {adapterName}({ns}.{structName} value)";
            s += "{";
            s += $"JSValue jsValue = Context.CreateStruct<{ns}.{structName}>();";

            foreach (ISymbol property in copyableProperties)
            {
                ITypeSymbol propertyType = (property as IPropertySymbol)?.Type ??
                    ((IFieldSymbol)property).Type;
                s += $"jsValue[\"{ToCamelCase(property.Name)}\"] = " +
                    $"{Convert($"value.{property.Name}", propertyType, null)};";
            }

            s += "return jsValue;";
            s += "}";
        }
        else
        {
            s += $"private static {ns}.{structName} {adapterName}(JSValue jsValue)";
            s += "{";
            s += $"{ns}.{structName} value = new();";

            foreach (ISymbol property in copyableProperties)
            {
                ITypeSymbol propertyType = (property as IPropertySymbol)?.Type ??
                    ((IFieldSymbol)property).Type;
                s += $"value.{property.Name} = " +
                    $"{Convert($"jsValue[\"{ToCamelCase(property.Name)}\"]", null, propertyType)};";
            }

            s += "return value;";
            s += "}";
        }
    }

    private void GenerateArrayAdapter(
        ref SourceBuilder s,
        string adapterName,
        ITypeSymbol elementType)
    {
        string ns = GetNamespace(elementType);
        string elementName = elementType.Name;

        if (adapterName.StartsWith(AdapterFromPrefix))
        {
            s += $"private static JSValue {adapterName}({ns}.{elementName}[] array)";
            s += "{";
            s += "JSArray jsArray = new JSArray(array.Length);";
            s += "for (int i = 0; i < array.Length; i++)";
            s += "{";
            s += $"jsArray[i] = {Convert("array[i]", elementType, null)};";
            s += "}";
            s += "return jsArray;";
            s += "}";
        }
        else
        {
            s += $"private static {ns}.{elementName}[] {adapterName}(JSValue value)";
            s += "{";
            s += "JSArray jsArray = (JSArray)value;";
            s += $"{ns}.{elementName}[] array = new {ns}.{elementName}[jsArray.Length];";
            s += "for (int i = 0; i < array.Length; i++)";
            s += "{";
            s += $"array[i] = {Convert("jsArray[i]", null, elementType)};";
            s += "}";
            s += "return array;";
            s += "}";
        }
    }

    private bool IsTypedArrayType(ITypeSymbol elementType)
    {
        return elementType.SpecialType switch
        {
            SpecialType.System_SByte => true,
            SpecialType.System_Byte => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_UInt16 => true,
            SpecialType.System_Int32 => true,
            SpecialType.System_UInt32 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_UInt64 => true,
            SpecialType.System_Single => true,
            SpecialType.System_Double => true,
            _ => false,
        };
    }

    private void AdaptThisArg(ref SourceBuilder s, ISymbol symbol)
    {

        string ns = GetNamespace(symbol);
        string typeName = symbol.ContainingType.Name;

        // For a method on a module class, the .NET object handle is stored in
        // module instance data instead of the JS object.
        if (symbol.ContainingType.GetAttributes().Any(
            (a) => a.AttributeClass?.Name == "JSModuleAttribute"))
        {
            s += $"if (!(JSNativeApi.GetInstanceData() is {ns}.{typeName} __obj))";
            s += "{";
            s += "return JSValue.Undefined;";
            s += "}";
        }
        else if (symbol.ContainingType.TypeKind == TypeKind.Class)
        {
            s += $"if (!(__args.ThisArg.Unwrap() is {ns}.{typeName} __obj))";
            s += "{";
            s += "return JSValue.Undefined;";
            s += "}";
        }
        else if (symbol.ContainingType.TypeKind == TypeKind.Struct)
        {
            // Structs are not wrapped; they are passed by value via an adapter method.
            string adapterName = GetStructAdapterName(symbol.ContainingType, toJS: false);
            s += $"{ns}.{typeName} __obj = {adapterName}(__args.ThisArg);";
        }
    }

    private void AdaptArgument(
        ref SourceBuilder s,
        ITypeSymbol parameterType,
        string parameterName,
        int index)
    {
        s += $"var {parameterName} = {Convert($"__args[{index}]", null, parameterType)};";
    }

    private void AdaptReturnValue(ref SourceBuilder s, ITypeSymbol returnType)
    {
        s += $"return {Convert("__result", returnType, null)};";
    }

    private string Convert(string fromExpression, ITypeSymbol? fromType, ITypeSymbol? toType)
    {
        if (fromType == null)
        {
            // Convert from JSValue to a C# type.
            (toType, bool isNullable) = SplitNullable(toType!);
            if (isNullable)
            {
                return $"({fromExpression}).IsNullOrUndefined() ? " +
                    $"({toType}?)null : {Convert(fromExpression, fromType, toType)}";
            }

            if (CanCast(toType!))
            {
                return $"({toType})({fromExpression})";
            }
            else if (toType.TypeKind == TypeKind.Class)
            {
                VerifyReferencedTypeIsExported(toType);

                return $"({toType})JSNativeApi.Unwrap({fromExpression})";

            }
            else if (toType.TypeKind == TypeKind.Struct)
            {
                if (toType is INamedTypeSymbol namedType &&
                    namedType.TypeParameters.Length == 1 &&
                    namedType.OriginalDefinition.Name == "Memory" &&
                    IsTypedArrayType(namedType.TypeArguments[0]))
                {
                    return $"((JSTypedArray<{namedType.TypeArguments[0]}>){fromExpression}).AsMemory()";
                }

                VerifyReferencedTypeIsExported(toType);

                string adapterName = GetStructAdapterName(toType, toJS: false);
                return $"{adapterName}({fromExpression})";
            }
            else if (toType.TypeKind == TypeKind.Array)
            {
                ITypeSymbol elementType = ((IArrayTypeSymbol)toType).ElementType;
                VerifyReferencedTypeIsExported(elementType);

                string adapterName = GetArrayAdapterName(elementType, toJS: false);
                return $"{adapterName}({fromExpression})";
            }
            else if (toType is INamedTypeSymbol namedType && namedType.TypeParameters.Length > 0)
            {
                string collectionTypeName = toType.OriginalDefinition.Name;
                if (collectionTypeName == "IList" ||
                    collectionTypeName == "ICollection")
                {
                    ITypeSymbol elementType = namedType.TypeArguments[0];
                    return $"(JSNativeApi.TryUnwrap({fromExpression}, out var __collection) " +
                        $"? ({toType})__collection! : ((JSArray){fromExpression})" +
                        $".As{collectionTypeName.Substring(1)}<{elementType}>(" +
                        $"(value) => {Convert("value", null, elementType)}, " +
                        $"(value) => {Convert("value", elementType, null)}))";
                }
                else if (collectionTypeName == "IReadOnlyList" ||
                    collectionTypeName == "IReadOnlyCollection" ||
                    collectionTypeName == "IEnumerable")
                {
                    ITypeSymbol elementType = namedType.TypeArguments[0];
                    return $"(JSNativeApi.TryUnwrap({fromExpression}, out var __collection) " +
                        $"? ({toType})__collection! : ((JSArray){fromExpression})" +
                        $".As{collectionTypeName.Substring(1)}<{elementType}>(" +
                        $"(value) => {Convert("value", null, elementType)}))";
                }

                // TODO: Handle other generic collection interfaces.
            }

            // TODO: Handle other kinds of conversions from JSValue.
            // TODO: Handle unwrapping external values.
            return $"default({toType})" + (toType.IsValueType ? string.Empty : "!");
        }
        else if (toType == null)
        {
            // Convert from a C# type to JSValue.
            (fromType, bool isNullable) = SplitNullable(fromType!);
            if (isNullable)
            {
                if (fromType.IsValueType)
                {
                    return $"{fromExpression}.HasValue ? " +
                        $"{Convert($"({fromExpression}).Value", fromType, toType)} : JSValue.Null";
                }
                else
                {
                    return $"{fromExpression} == null ? " +
                        $"JSValue.Null : {Convert($"{fromExpression}", fromType, toType)}";
                }
            }

            if (CanCast(fromType!))
            {
                return $"(JSValue)({fromExpression})";
            }
            else if (fromType.TypeKind == TypeKind.Class)
            {
                VerifyReferencedTypeIsExported(fromType);
                return $"Context.GetOrCreateObjectWrapper({fromExpression})";
            }
            else if (fromType.TypeKind == TypeKind.Struct)
            {
                if (fromType is INamedTypeSymbol namedType &&
                    namedType.TypeParameters.Length == 1 &&
                    namedType.OriginalDefinition.Name == "Memory" &&
                    IsTypedArrayType(namedType.TypeArguments[0]))
                {
                    return $"new JSTypedArray<{namedType.TypeArguments[0]}>({fromExpression})";
                }

                VerifyReferencedTypeIsExported(fromType);

                string adapterName = GetStructAdapterName(fromType, toJS: true);
                return $"{adapterName}({fromExpression})";
            }
            else if (fromType.TypeKind == TypeKind.Array)
            {
                ITypeSymbol elementType = ((IArrayTypeSymbol)fromType).ElementType;
                VerifyReferencedTypeIsExported(elementType);

                string adapterName = GetArrayAdapterName(elementType, toJS: true);
                return $"{adapterName}({fromExpression})";
            }
            else if (fromType is INamedTypeSymbol namedType && namedType.TypeParameters.Length > 0)
            {
                string collectionTypeName = fromType.OriginalDefinition.Name;
                if (collectionTypeName == "IList" ||
                    collectionTypeName == "ICollection")
                {
                    ITypeSymbol elementType = namedType.TypeArguments[0];
                    return $"Context.GetOrCreateCollectionWrapper({fromExpression}, " +
                        $"(value) => {Convert("value", elementType, null)}, " +
                        $"(value) => {Convert("value", null, elementType)})";
                }
                else if (collectionTypeName == "IReadOnlyList" ||
                    collectionTypeName == "IReadOnlyCollection" ||
                    collectionTypeName == "IEnumerable")
                {
                    ITypeSymbol elementType = namedType.TypeArguments[0];
                    return $"Context.GetOrCreateCollectionWrapper({fromExpression}, " +
                        $"(value) => {Convert("value", elementType, null)})";
                }

                // TODO: Handle other generic collection interfaces.
            }

            // TODO: Handle other kinds of conversions to JSValue.
            // TODO: Consider wrapping unsupported types in a value of type "external".
            return "JSValue.Undefined";
        }
        else
        {
            (toType, bool isNullable) = SplitNullable(toType!);

            // TODO: Handle multi-step conversions.
            return $"default({toType})" + (isNullable ? string.Empty : "!");
        }
    }

    private void VerifyReferencedTypeIsExported(ITypeSymbol type)
    {
        switch (type.SpecialType)
        {
            case SpecialType.System_Object:
            case SpecialType.System_String: return;
            default: break;
        }

        if (ModuleGenerator.GetJSExportAttribute(type) == null)
        {
            // TODO: Consider an option to automatically export referenced classes?
            ReportError(
                DiagnosticId.ReferenedTypeNotExported,
                type,
                $"Referenced type {type} is not exported.");
        }
    }

    private static bool CanCast(ITypeSymbol type)
    {
        (type, bool isNullable) = SplitNullable(type);
        return type.SpecialType switch
        {
            SpecialType.System_Boolean => true,
            SpecialType.System_SByte => true,
            SpecialType.System_Byte => true,
            SpecialType.System_Int16 => true,
            SpecialType.System_UInt16 => true,
            SpecialType.System_Int32 => true,
            SpecialType.System_UInt32 => true,
            SpecialType.System_Int64 => true,
            SpecialType.System_UInt64 => true,
            SpecialType.System_Single => true,
            SpecialType.System_Double => true,
            SpecialType.System_String => !isNullable,
            _ => false,
        };
    }

    private static (ITypeSymbol, bool) SplitNullable(ITypeSymbol type)
    {
        bool isNullable = false;
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
        {
            isNullable = true;

            // Handle either a Nullable<T> (value type) or a nullable reference type.
            type = type.IsValueType
                ? ((INamedTypeSymbol)type).TypeArguments[0]
                : type.OriginalDefinition;
        }
        return (type, isNullable);
    }
}
