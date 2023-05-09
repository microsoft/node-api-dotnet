// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi.Generator;

/// <summary>
/// Extension methods for code-analysis symbol interfaces to support creation of fake
/// "symbolic" <see cref="Type" /> instances and members to represent compile-time types
/// during source generation.
/// </summary>
/// <remarks>
/// Symbolic types are abstract and consist of only public abstract members (no code).
/// They are used to construct lambda expressions from which C# code can be generated
/// (because expressions don't support code-analysis symbols).
/// </remarks>
internal static class SymbolExtensions
{
    // The type cache must be thread-static (and initialized by each thread)
    // because the build server may keep the analyzer in memory and re-use it
    // across multiple compilations.

    [ThreadStatic]
    private static AssemblyBuilder? s_assemblyBuilder;

    [ThreadStatic]
    private static ModuleBuilder? s_moduleBuilder;

    [ThreadStatic]
    private static Dictionary<string, Type>? s_symbolicTypes;

    private static int s_assemblyIndex = 0;

    private static ModuleBuilder ModuleBuilder
    {
        get
        {
            if (s_moduleBuilder == null)
            {
                // Use a unique assembly name per thread.
                string assemblyName = typeof(SymbolExtensions).FullName +
                    "_" + Interlocked.Increment(ref s_assemblyIndex);
                s_assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
                    new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
                s_moduleBuilder = s_assemblyBuilder.DefineDynamicModule(
                    typeof(SymbolExtensions).Name);
            }
            return s_moduleBuilder;
        }
    }

    private static IDictionary<string, Type> SymbolicTypes
    {
        get
        {
            s_symbolicTypes ??= new Dictionary<string, Type>();
            return s_symbolicTypes;
        }
    }

    /// <summary>
    /// Clears the type cache to free up memory and get ready for another possible invocation.
    /// The cache cannot be re-used across multiple invocations.
    /// </summary>
    public static void Reset()
    {
        s_assemblyBuilder = null;
        s_moduleBuilder = null;
        s_symbolicTypes = null;
    }

    /// <summary>
    /// Gets either the actual type (if it is a system type) or a symbolic type
    /// for the type symbol.
    /// </summary>
    public static Type AsType(this ITypeSymbol typeSymbol)
    {
        if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            if (arrayTypeSymbol.Rank != 1)
            {
                throw new NotSupportedException("Multi-dimensional arrays are not supported.");
            }

            return arrayTypeSymbol.ElementType.AsType().MakeArrayType();
        }

        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            throw new NotSupportedException($"Unnamed types are not supported: {typeSymbol}");
        }

        string typeFullName = typeSymbol.ContainingNamespace + "." + typeSymbol.Name;
        ITypeSymbol[] genericArguments = namedTypeSymbol.TypeArguments.ToArray();
        typeFullName += genericArguments.Length > 0 ?
            "`" + genericArguments.Length : string.Empty;
        Type? systemType = typeof(object).Assembly.GetType(typeFullName) ??
            typeof(JSValue).Assembly.GetType(typeFullName);

        if (systemType != null)
        {
            if (genericArguments.Length > 0)
            {
                systemType = systemType.MakeGenericType(
                    genericArguments.Select(AsType).ToArray());
            }

            return systemType;
        }

        if (SymbolicTypes.TryGetValue(typeFullName, out Type? symbolicType))
        {
            if (genericArguments.Length > 0)
            {
                symbolicType = symbolicType.MakeGenericType(
                    genericArguments.Select(AsType).ToArray());
            }

            return symbolicType;
        }

        symbolicType = typeSymbol.TypeKind switch
        {
            TypeKind.Enum => BuildSymbolicEnumType(typeSymbol, typeFullName),

            TypeKind.Class or TypeKind.Interface or TypeKind.Struct or TypeKind.Delegate =>
                BuildSymbolicObjectType(typeSymbol, typeFullName),

            _ => throw new NotSupportedException($"Type kind not supported: {typeSymbol.TypeKind}"),
        };

        // Update the map entry to refer to the built type instead of the type builder.
        SymbolicTypes[typeFullName] = symbolicType;

        if (genericArguments.Length > 0)
        {
            symbolicType = symbolicType.MakeGenericType(
                genericArguments.Select(AsType).ToArray());
        }

        return symbolicType;
    }

    private static Type BuildSymbolicEnumType(
        ITypeSymbol typeSymbol,
        string typeFullName)
    {
        Type underlyingType = ((INamedTypeSymbol)typeSymbol).EnumUnderlyingType!.AsType();
        EnumBuilder enumBuilder = ModuleBuilder.DefineEnum(
            typeFullName, TypeAttributes.Public, underlyingType);
        foreach (IFieldSymbol fieldSymbol in typeSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            enumBuilder.DefineLiteral(fieldSymbol.Name, fieldSymbol.ConstantValue);
        }
        return enumBuilder.CreateType()!;
    }

    private static Type BuildSymbolicObjectType(
        ITypeSymbol typeSymbol,
        string typeFullName)
    {
        TypeAttributes attributes = TypeAttributes.Public | TypeAttributes.Abstract;
        if (typeSymbol.TypeKind == TypeKind.Interface)
        {
            attributes |= TypeAttributes.Interface;
        }
        else if (typeSymbol.TypeKind == TypeKind.Delegate)
        {
            attributes |= TypeAttributes.Sealed;
        }

        TypeBuilder typeBuilder = ModuleBuilder.DefineType(
            name: typeFullName,
            attributes,
            parent: typeSymbol.BaseType?.AsType());

        // Add the type builder to the map while building it, to support circular references.
        SymbolicTypes.Add(typeFullName, typeBuilder);

        foreach (Type interfaceType in typeSymbol.Interfaces.Select(AsType))
        {
            typeBuilder.AddInterfaceImplementation(interfaceType);
        }

        foreach (ISymbol memberSymbol in typeSymbol.GetMembers()
            .Where((m) => m.DeclaredAccessibility == Accessibility.Public))
        {
            if (memberSymbol is IMethodSymbol constructorSymbol &&
                constructorSymbol.MethodKind == MethodKind.Constructor)
            {
                BuildSymbolicConstructor(typeBuilder, constructorSymbol);
            }
            else if (memberSymbol is IMethodSymbol methodSymbol &&
                (methodSymbol.MethodKind == MethodKind.Ordinary ||
                methodSymbol.MethodKind == MethodKind.DelegateInvoke))
            {
                BuildSymbolicMethod(typeBuilder, methodSymbol);
            }
            else if (memberSymbol is IPropertySymbol propertySymbol)
            {
                BuildSymbolicProperty(typeBuilder, propertySymbol);
            }
            else if (memberSymbol is IEventSymbol eventSymbol)
            {
                // TODO: Events
            }
            else if (memberSymbol is IFieldSymbol fieldSymbol)
            {
                // TODO: Fields (at least const fields)
            }
        }

        // Preserve JS attributes, which might be referenced by the marshaller.
        foreach (AttributeData attribute in typeSymbol.GetAttributes())
        {
            if (attribute.AttributeClass!.ContainingAssembly.Name ==
                typeof(JSExportAttribute).Assembly.GetName().Name)
            {
                Type attributeType = attribute.AttributeClass.AsType();
                ConstructorInfo constructor = attributeType.GetConstructor(
                    attribute.ConstructorArguments.Select((a) => a.Type!.AsType()).ToArray()) ??
                    throw new MissingMemberException(
                        $"Constructor not found for attribute: {attributeType.Name}");
                CustomAttributeBuilder attributeBuilder = new(
                    constructor,
                    attribute.ConstructorArguments.Select((a) => a.Value).ToArray(),
                    attribute.NamedArguments.Select((a) =>
                        GetAttributeProperty(attributeType, a.Key)).ToArray(),
                    attribute.NamedArguments.Select((a) => a.Value.Value).ToArray());
                typeBuilder.SetCustomAttribute(attributeBuilder);
            }
        }

        static PropertyInfo GetAttributeProperty(Type type, string name)
            => type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) ??
                throw new MissingMemberException(
                    $"Property {name} not found on attribute {type.Name}.");
        return typeBuilder.CreateType()!;
    }

    private static ConstructorBuilder BuildSymbolicConstructor(
        TypeBuilder typeBuilder, IMethodSymbol constructorSymbol)
    {
        bool isDelegateConstructor = typeBuilder.BaseType == typeof(MulticastDelegate);
        ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | (isDelegateConstructor ?
                MethodAttributes.RTSpecialName | MethodAttributes.HideBySig : default),
            CallingConventions.HasThis,
            constructorSymbol.Parameters.Select((p) => p.Type.AsType()).ToArray());

        IReadOnlyList<IParameterSymbol> parameters = constructorSymbol.Parameters;
        for (int i = 0; i < parameters.Count; i++)
        {
            constructorBuilder.DefineParameter(i, ParameterAttributes.None, parameters[i].Name);
        }

        if (isDelegateConstructor)
        {
            // Delegate constructors are implemented by the runtime.
            constructorBuilder.SetImplementationFlags(
                MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
        }
        else
        {
            // Constructors cannot be abstract; emit a minimal body.
            constructorBuilder.GetILGenerator().Emit(OpCodes.Ret);
        }

        return constructorBuilder;
    }

    private static void BuildSymbolicMethod(
        TypeBuilder typeBuilder, IMethodSymbol methodSymbol)
    {
        bool isDelegateMethod = typeBuilder.BaseType == typeof(MulticastDelegate);
        MethodAttributes attributes = MethodAttributes.Public | (methodSymbol.IsStatic ?
            MethodAttributes.Static : MethodAttributes.Virtual | (isDelegateMethod ?
            MethodAttributes.HideBySig : MethodAttributes.Abstract));
        MethodBuilder methodBuilder = typeBuilder.DefineMethod(
            methodSymbol.Name,
            attributes,
            methodSymbol.IsStatic ? CallingConventions.Standard : CallingConventions.HasThis,
            methodSymbol.ReturnType.AsType(),
            methodSymbol.Parameters.Select((p) => p.Type.AsType()).ToArray());
        BuildSymbolicParameters(methodBuilder, methodSymbol.Parameters);

        if (isDelegateMethod)
        {
            // Delegate invoke methods are implemented by the runtime.
            methodBuilder.SetImplementationFlags(
                MethodImplAttributes.Runtime | MethodImplAttributes.Managed);
        }
        else if (methodSymbol.IsStatic)
        {
            // Static methods cannot be abstract; emit a minimal body.
            methodBuilder.GetILGenerator().Emit(OpCodes.Ret);
        }
    }

    private static void BuildSymbolicProperty(
        TypeBuilder typeBuilder, IPropertySymbol propertySymbol)
    {
        PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(
            propertySymbol.Name,
            PropertyAttributes.None,
            propertySymbol.Type.AsType(),
            propertySymbol.Parameters.Select((p) => p.Type.AsType()).ToArray());

        MethodAttributes attributes = MethodAttributes.SpecialName | MethodAttributes.Public |
            (propertySymbol.IsStatic ? MethodAttributes.Static :
                MethodAttributes.Abstract | MethodAttributes.Virtual);

        if (propertySymbol.GetMethod != null)
        {
            MethodBuilder getMethodBuilder = typeBuilder.DefineMethod(
                propertySymbol.GetMethod.Name,
                attributes,
                propertySymbol.IsStatic ? CallingConventions.Standard : CallingConventions.HasThis,
                propertySymbol.GetMethod.ReturnType.AsType(),
                propertySymbol.GetMethod.Parameters.Select((p) => p.Type.AsType()).ToArray());
            BuildSymbolicParameters(getMethodBuilder, propertySymbol.GetMethod.Parameters);
            if (propertySymbol.IsStatic) getMethodBuilder.GetILGenerator().Emit(OpCodes.Ret);
            propertyBuilder.SetGetMethod(getMethodBuilder);
        }

        if (propertySymbol.SetMethod != null)
        {
            MethodBuilder setMethodBuilder = typeBuilder.DefineMethod(
                propertySymbol.SetMethod.Name,
                attributes,
                propertySymbol.IsStatic ? CallingConventions.Standard : CallingConventions.HasThis,
                propertySymbol.SetMethod.ReturnType.AsType(),
                propertySymbol.SetMethod.Parameters.Select((p) => p.Type.AsType()).ToArray());
            BuildSymbolicParameters(setMethodBuilder, propertySymbol.SetMethod.Parameters);
            if (propertySymbol.IsStatic) setMethodBuilder.GetILGenerator().Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(setMethodBuilder);
        }
    }

    private static void BuildSymbolicParameters(
        MethodBuilder methodBuilder, IReadOnlyList<IParameterSymbol> parameters)
    {
        for (int i = 0; i < parameters.Count; i++)
        {
            ParameterAttributes attributes = parameters[i].RefKind switch
            {
                RefKind.In => ParameterAttributes.In,
                RefKind.Out => ParameterAttributes.Out,
                RefKind.Ref => ParameterAttributes.In | ParameterAttributes.Out,
                _ => ParameterAttributes.None,
            };
            // Position here is offset by one because the return parameter is at 0.
            methodBuilder.DefineParameter(i + 1, attributes, parameters[i].Name);
        }
    }

    /// <summary>
    /// Gets real or symbolic constructor info for a method symbol.
    /// </summary>
    public static ConstructorInfo AsConstructorInfo(this IMethodSymbol methodSymbol)
    {
        if (methodSymbol.MethodKind != MethodKind.Constructor)
        {
            throw new ArgumentException("Method is not a constructor.", nameof(methodSymbol));
        }

        Type type = methodSymbol.ContainingType.AsType();
        ConstructorInfo? constructorInfo = type.GetConstructor(
            methodSymbol.Parameters.Select((p) => p.Type.AsType()).ToArray());
        return constructorInfo ?? throw new InvalidOperationException(
                $"Constructor not found for type: {type.Name}");
    }

    /// <summary>
    /// Gets real or symbolic method info for a method symbol.
    /// </summary>
    public static MethodInfo AsMethodInfo(this IMethodSymbol methodSymbol)
    {
        Type type = methodSymbol.ContainingType.AsType();
        BindingFlags bindingFlags = BindingFlags.Public |
            (methodSymbol.IsStatic ? BindingFlags.Static : BindingFlags.Instance);
        MethodInfo? methodInfo = type.GetMethod(
            methodSymbol.Name,
            bindingFlags,
            binder: null,
            methodSymbol.Parameters.Select((p) => p.Type.AsType()).ToArray(),
            modifiers: null);
        return methodInfo ?? throw new InvalidOperationException(
                $"Method not found: {type.Name}.{methodSymbol.Name}");
    }

    /// <summary>
    /// Gets real or symbolic property info for a property symbol.
    /// </summary>
    public static PropertyInfo AsPropertyInfo(this IPropertySymbol propertySymbol)
    {
        Type type = propertySymbol.ContainingType.AsType();
        BindingFlags bindingFlags = BindingFlags.Public |
            (propertySymbol.IsStatic ? BindingFlags.Static : BindingFlags.Instance);
        PropertyInfo? propertyInfo = type.GetProperty(propertySymbol.Name, bindingFlags);
        return propertyInfo ?? throw new InvalidOperationException(
                $"Property not found: {type.Name}.{propertySymbol.Name}");
    }
}
