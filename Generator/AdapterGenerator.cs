using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace NodeApi.Generator;

/// <summary>
/// Generates adapter methods for C# members exported to JavaScript.
/// </summary>
internal class AdapterGenerator : SourceGenerator
{
    private const string AdapterGetPrefix = "get_";
    private const string AdapterSetPrefix = "set_";
    private const string AdapterConstructorPrefix = "new_";

    private readonly List<KeyValuePair<string, ISymbol>> _adaptedSymbols = new();

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
        _adaptedSymbols.Add(new KeyValuePair<string, ISymbol>(adapterName, constructor));
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
        _adaptedSymbols.Add(new KeyValuePair<string, ISymbol>(adapterName, method));
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
            _adaptedSymbols.Add(new KeyValuePair<string, ISymbol>(getAdapterName, property));
        }

        string? setAdapterName = null;
        if (property?.SetMethod?.DeclaredAccessibility == Accessibility.Public)
        {
            setAdapterName = $"{AdapterSetPrefix}{ns.Replace('.', '_')}_{className}_{property.Name}";
            _adaptedSymbols.Add(new KeyValuePair<string, ISymbol>(setAdapterName, property));
        }

        return (getAdapterName, setAdapterName);
    }

    internal void GenerateAdapters(SourceBuilder s)
    {
        foreach (KeyValuePair<string, ISymbol> nameAndSymbol in _adaptedSymbols)
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
    }

    private static void GenerateConstructorAdapter(
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

    private static void GenerateMethodAdapter(
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

    private static void GeneratePropertyAdapter(
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

    private static void AdaptThisArg(ref SourceBuilder s, ISymbol symbol)
    {

        string ns = GetNamespace(symbol);
        string className = symbol.ContainingType.Name;

        // For a method on a module class, the .NET object handle is stored in
        // module instance data instead of the JS object.
        if (symbol.ContainingType.GetAttributes().Any(
            (a) => a.AttributeClass?.Name == "JSModuleAttribute"))
        {
            s += $"if (!(JSNativeApi.GetInstanceData() is {ns}.{className} __obj))";
            s += "{";
            s += "return JSValue.Undefined;";
            s += "}";
        }
        else
        {
            s += $"if (!(__args.ThisArg.Unwrap() is {ns}.{className} __obj))";
            s += "{";
            s += "return JSValue.Undefined;";
            s += "}";
        }
    }

    private static void AdaptArgument(
        ref SourceBuilder s,
        ITypeSymbol parameterType,
        string parameterName,
        int index)
    {
        s += $"var {parameterName} = ({parameterType})__args[{index}];";
    }

    private static void AdaptReturnValue(ref SourceBuilder s, ITypeSymbol _/*returnType*/)
    {
        s += $"return (JSValue)__result;";
    }
}
