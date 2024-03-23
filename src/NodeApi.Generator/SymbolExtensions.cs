// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

    private static Dictionary<string, Type> SymbolicTypes
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
        return typeSymbol.AsType(genericTypeParameters: null, buildType: true);
    }

    /// <summary>
    /// Gets either the actual type (if it is a system type) or a symbolic type
    /// for the type symbol.
    /// </summary>
    /// <param name="genericTypeParameters">Generic parameters from the containing type,
    /// if the type is a nested type and the containing type is generic.</param>
    /// <param name="buildType">True to force building (AKA emitting) the Type instance; if false
    /// then an unbuilt TypeBuilder instance may be returned. (It is a subclass of Type, but
    /// does not support some reflection operations.) Delayed type building is necessary in
    /// complex object graphs where types have circular references to each other.</param>
    private static Type AsType(
        this ITypeSymbol typeSymbol,
        Type[]? genericTypeParameters,
        bool buildType = false)
    {
        if (typeSymbol is IArrayTypeSymbol arrayTypeSymbol)
        {
            if (arrayTypeSymbol.Rank != 1)
            {
                throw new NotSupportedException("Multi-dimensional arrays are not supported.");
            }

            return arrayTypeSymbol.ElementType.AsType(genericTypeParameters, buildType)
                .MakeArrayType();
        }

        if (typeSymbol is ITypeParameterSymbol typeParameterSymbol)
        {
            if (genericTypeParameters?.Length > typeParameterSymbol.Ordinal)
            {
                return genericTypeParameters[typeParameterSymbol.Ordinal];
            }
            else
            {
#if NETFRAMEWORK
                throw new NotSupportedException(
                    "Generic type parameters are not supported in this context.");
#else
                return Type.MakeGenericMethodParameter(typeParameterSymbol.Ordinal);
#endif
            }
        }

        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            throw new NotSupportedException($"Unnamed types are not supported: {typeSymbol}");
        }

        string typeFullName = GetTypeSymbolFullName(namedTypeSymbol);
        ITypeSymbol[] genericArguments = namedTypeSymbol.TypeArguments.ToArray();
        Type? systemType = typeof(object).Assembly.GetType(typeFullName) ??
            typeof(JSValue).Assembly.GetType(typeFullName) ??
            typeof(BigInteger).Assembly.GetType(typeFullName);

        if (systemType != null)
        {
            if (genericArguments.Length > 0)
            {
                systemType = systemType.MakeGenericType(genericArguments
                    .Select((t) => t.AsType(genericTypeParameters, buildType))
                    .ToArray());
            }

            return systemType;
        }

        // Generating the containing type will also generate the nested type,
        // so it should be found in the SymbolicTypes dictionary afterward.
        typeSymbol.ContainingType?.AsType(genericTypeParameters: null, buildType);

        if (SymbolicTypes.TryGetValue(typeFullName, out Type? symbolicType))
        {
            if (genericArguments.Length > 0)
            {
                symbolicType = symbolicType.MakeGenericType(genericArguments
                    .Select((t) => t.AsType(genericTypeParameters, buildType))
                    .ToArray());
            }

            if (buildType && symbolicType is TypeBuilder typeBuilder)
            {
                BuildBaseTypeAndInterfaces((INamedTypeSymbol)typeSymbol);

                symbolicType = typeBuilder.CreateType()!;
                SymbolicTypes[typeFullName] = symbolicType;
            }

            return symbolicType;
        }

        symbolicType = typeSymbol.TypeKind switch
        {
            TypeKind.Enum => BuildSymbolicEnumType(namedTypeSymbol, typeFullName),

            TypeKind.Class or TypeKind.Interface or TypeKind.Struct or TypeKind.Delegate =>
            BuildSymbolicObjectType(
                namedTypeSymbol, typeFullName, genericTypeParameters, buildType),

            _ => throw new NotSupportedException(
                $"Type kind not supported: {typeSymbol.TypeKind}"),
        };

        // Update the map entry to refer to the built type instead of the type builder.
        SymbolicTypes[typeFullName] = symbolicType;

        if (genericArguments.Length > 0)
        {
            symbolicType = symbolicType.MakeGenericType(genericArguments
                .Select((t) => t.AsType(genericTypeParameters, buildType))
                .ToArray());
        }

        return symbolicType;
    }

    /// <summary>
    /// Gets the full name of a type symbol. It is the same as <see cref="Type.FullName" />,
    /// but this is used before the Type instance is built from the type symbol.
    /// </summary>
    private static string GetTypeSymbolFullName(INamedTypeSymbol typeSymbol)
    {
        string ns = typeSymbol.ContainingType != null ?
            GetTypeSymbolFullName(typeSymbol.ContainingType) :
            typeSymbol.ContainingNamespace.ToString()!;
        string name = (ns.Length > 0 ? ns + "." : "") + typeSymbol.Name;

        if (typeSymbol.TypeParameters.Length > 0)
        {
            name += "`" + typeSymbol.TypeParameters.Length;
        }

        return name;
    }

    private static Type BuildSymbolicEnumType(
        INamedTypeSymbol typeSymbol,
        string typeFullName)
    {
        Type underlyingType = typeSymbol.EnumUnderlyingType!.AsType();
        EnumBuilder enumBuilder = ModuleBuilder.DefineEnum(
            typeFullName, TypeAttributes.Public, underlyingType);
        foreach (IFieldSymbol fieldSymbol in typeSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            enumBuilder.DefineLiteral(fieldSymbol.Name, fieldSymbol.ConstantValue);
        }
        return enumBuilder.CreateType()!;
    }

    private static TypeAttributes GetTypeAttributes(TypeKind typeKind)
    {
        TypeAttributes attributes = TypeAttributes.Public;
        if (typeKind == TypeKind.Interface)
        {
            attributes |= TypeAttributes.Interface;
        }
        else if (typeKind == TypeKind.Delegate)
        {
            attributes |= TypeAttributes.Sealed;
        }
        if (typeKind != TypeKind.Enum)
        {
            attributes |= TypeAttributes.Abstract;
        }
        return attributes;
    }

    private static Type BuildSymbolicObjectType(
        INamedTypeSymbol typeSymbol,
        string typeFullName,
        Type[]? genericTypeParameters,
        bool buildType)
    {
        TypeBuilder typeBuilder;
        Type? baseType = typeSymbol.BaseType?.AsType(genericTypeParameters, buildType);

        // A base type might have had a reference to this type and therefore already defined it.
        if (SymbolicTypes.TryGetValue(typeFullName, out Type? thisType))
        {
            if (thisType is not TypeBuilder)
            {
                // The type is already fully built.
                return thisType;
            }

            typeBuilder = (TypeBuilder)thisType;
        }
        else
        {
            typeBuilder = ModuleBuilder.DefineType(
                name: typeFullName,
                GetTypeAttributes(typeSymbol.TypeKind),
                parent: baseType);

            if (typeSymbol.TypeParameters.Length > 0)
            {
                genericTypeParameters ??= [];
                genericTypeParameters = typeBuilder.DefineGenericParameters(
                    typeSymbol.TypeParameters.Select((p) => p.Name).ToArray());
            }

            // Add the type builder to the map while building it, to support circular references.
            SymbolicTypes.Add(typeFullName, typeBuilder);

            BuildSymbolicTypeMembers(typeSymbol, typeBuilder, genericTypeParameters);

            // Preserve JS attributes, which might be referenced by the marshaller.
            foreach (AttributeData attribute in typeSymbol.GetAttributes())
            {
                if (attribute.AttributeClass!.ContainingNamespace.ToString()!.StartsWith(
                        typeof(JSExportAttribute).Namespace!))
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
        }

        if (!buildType)
        {
            return typeBuilder;
        }

        // Ensure the base type and interfaces are built before building the derived type.
        BuildBaseTypeAndInterfaces(typeSymbol);

        // Ensure this type is only built once.
        if (SymbolicTypes.TryGetValue(typeFullName, out thisType) && thisType is not TypeBuilder)
        {
            return thisType;
        }

        return typeBuilder.CreateType()!;
    }

    private static void BuildBaseTypeAndInterfaces(INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.BaseType != null)
        {
            BuildBaseTypeAndInterfaces(typeSymbol.BaseType);

            string baseTypeFullName = GetTypeSymbolFullName(typeSymbol.BaseType!);
            if (SymbolicTypes.TryGetValue(baseTypeFullName, out Type? baseType) &&
                baseType is TypeBuilder baseTypeBuilder)
            {
                baseTypeBuilder.CreateType();
            }
        }

        foreach (INamedTypeSymbol interfaceTypeSymbol in typeSymbol.Interfaces)
        {
            BuildBaseTypeAndInterfaces(interfaceTypeSymbol);

            string interfaceTypeFullName = GetTypeSymbolFullName(interfaceTypeSymbol);
            if (SymbolicTypes.TryGetValue(interfaceTypeFullName, out Type? interfaceType) &&
                interfaceType is TypeBuilder interfaceTypeBuilder)
            {
                interfaceTypeBuilder.CreateType();
            }
        }
    }

    private static void BuildSymbolicTypeMembers(
        ITypeSymbol typeSymbol,
        TypeBuilder typeBuilder,
        Type[]? genericTypeParameters)
    {
        foreach (Type interfaceType in typeSymbol.Interfaces.Select(
            (i) => i.AsType(genericTypeParameters)))
        {
            typeBuilder.AddInterfaceImplementation(interfaceType);
        }

        foreach (ISymbol memberSymbol in typeSymbol.GetMembers()
            .Where((m) => m.DeclaredAccessibility == Accessibility.Public))
        {
            if (memberSymbol is IMethodSymbol constructorSymbol &&
                constructorSymbol.MethodKind == MethodKind.Constructor)
            {
                BuildSymbolicConstructor(typeBuilder, constructorSymbol, genericTypeParameters);
            }
            else if (memberSymbol is IMethodSymbol methodSymbol &&
                (methodSymbol.MethodKind == MethodKind.Ordinary ||
                methodSymbol.MethodKind == MethodKind.DelegateInvoke))
            {
                BuildSymbolicMethod(typeBuilder, methodSymbol, genericTypeParameters);
            }
            else if (memberSymbol is IPropertySymbol propertySymbol)
            {
                BuildSymbolicProperty(typeBuilder, propertySymbol, genericTypeParameters);
            }
            else if (memberSymbol is IEventSymbol eventSymbol)
            {
                // TODO: Events
            }
            else if (memberSymbol is IFieldSymbol fieldSymbol)
            {
                // TODO: Fields (at least const fields)
            }
            else if (memberSymbol is INamedTypeSymbol nestedTypeSymbol)
            {
                TypeAttributes attributes = GetTypeAttributes(nestedTypeSymbol.TypeKind);
                attributes &= ~TypeAttributes.Public;
                attributes |= TypeAttributes.NestedPublic;

                TypeBuilder nestedTypeBuilder = typeBuilder.DefineNestedType(
                    nestedTypeSymbol.Name,
                    attributes,
                    parent: nestedTypeSymbol.TypeKind == TypeKind.Enum ? typeof(Enum) : null);

                // TODO: Handle nested enum members (fields).
                BuildSymbolicTypeMembers(
                    nestedTypeSymbol, nestedTypeBuilder, genericTypeParameters);
            }
        }
    }

    private static ConstructorBuilder BuildSymbolicConstructor(
        TypeBuilder typeBuilder,
        IMethodSymbol constructorSymbol,
        Type[]? genericTypeParameters)
    {
        bool isDelegateConstructor = typeBuilder.BaseType == typeof(MulticastDelegate);
        ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | (isDelegateConstructor ?
                MethodAttributes.RTSpecialName | MethodAttributes.HideBySig : default),
            CallingConventions.HasThis,
            constructorSymbol.Parameters.Select(
                (p) => p.Type.AsType(genericTypeParameters, buildType: false)).ToArray());

        IReadOnlyList<IParameterSymbol> parameters = constructorSymbol.Parameters;
        for (int i = 0; i < parameters.Count; i++)
        {
            // The parameter index is offset by 1.
            constructorBuilder.DefineParameter(i + 1, ParameterAttributes.None, parameters[i].Name);
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
        TypeBuilder typeBuilder,
        IMethodSymbol methodSymbol,
        Type[]? genericTypeParameters)
    {
        bool isDelegateMethod = typeBuilder.BaseType == typeof(MulticastDelegate);
        MethodAttributes attributes = MethodAttributes.Public | (methodSymbol.IsStatic ?
            MethodAttributes.Static : MethodAttributes.Virtual | (isDelegateMethod ?
            MethodAttributes.HideBySig : MethodAttributes.Abstract));
        MethodBuilder methodBuilder = typeBuilder.DefineMethod(
            methodSymbol.Name,
            attributes,
            methodSymbol.IsStatic ? CallingConventions.Standard : CallingConventions.HasThis);

        GenericTypeParameterBuilder[]? genericMethodParameters = null;
        if (methodSymbol.TypeParameters.Length > 0)
        {
            genericMethodParameters = methodBuilder.DefineGenericParameters(
                methodSymbol.TypeParameters.Select((p) => p.Name).ToArray());
        }

        Type[]? genericParameters =
            genericTypeParameters == null ? genericMethodParameters :
            genericMethodParameters == null ? genericTypeParameters :
            genericTypeParameters.Concat(genericMethodParameters).ToArray();

        methodBuilder.SetReturnType(
            methodSymbol.ReturnType.AsType(genericParameters, buildType: false));
        methodBuilder.SetParameters(methodSymbol.Parameters.Select(
            (p) => p.Type.AsType(genericParameters, buildType: false)).ToArray());
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
        TypeBuilder typeBuilder,
        IPropertySymbol propertySymbol,
        Type[]? genericTypeParameters)
    {
        PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(
            propertySymbol.Name,
            PropertyAttributes.None,
            propertySymbol.Type.AsType(genericTypeParameters, buildType: false),
            propertySymbol.Parameters.Select(
                (p) => p.Type.AsType(genericTypeParameters, buildType: false)).ToArray());

        MethodAttributes attributes = MethodAttributes.SpecialName | MethodAttributes.Public |
            (propertySymbol.IsStatic ? MethodAttributes.Static :
                MethodAttributes.Abstract | MethodAttributes.Virtual);

        if (propertySymbol.GetMethod != null)
        {
            MethodBuilder getMethodBuilder = typeBuilder.DefineMethod(
                propertySymbol.GetMethod.Name,
                attributes,
                propertySymbol.IsStatic ? CallingConventions.Standard : CallingConventions.HasThis,
                propertySymbol.GetMethod.ReturnType.AsType(genericTypeParameters, buildType: false),
                propertySymbol.GetMethod.Parameters.Select(
                    (p) => p.Type.AsType(genericTypeParameters, buildType: false)).ToArray());
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
                propertySymbol.SetMethod.ReturnType.AsType(genericTypeParameters, buildType: false),
                propertySymbol.SetMethod.Parameters.Select(
                    (p) => p.Type.AsType(genericTypeParameters, buildType: false)).ToArray());
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

        // Ensure constructor parameter types are built.
        foreach (IParameterSymbol parameter in methodSymbol.Parameters)
        {
            parameter.Type.AsType(type.GenericTypeArguments, buildType: true);
        }

        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance;
        ConstructorInfo? constructorInfo = type.GetConstructors(bindingFlags)
            .FirstOrDefault((c) => c.GetParameters().Select((p) => p.Name).SequenceEqual(
                methodSymbol.Parameters.Select((p) => p.Name)));
        return constructorInfo ?? throw new InvalidOperationException(
                $"Constructor not found for type: {type.Name}");
    }

    /// <summary>
    /// Gets real or symbolic method info for a method symbol.
    /// </summary>
    public static MethodInfo AsMethodInfo(this IMethodSymbol methodSymbol)
    {
        Type type = methodSymbol.ContainingType.AsType();

        // Ensure method parameter and return types are built.
        Type[] typeParameters = type.GetGenericArguments();
        foreach (IParameterSymbol parameter in methodSymbol.Parameters)
        {
            IEnumerable<Type> methodTypeParameters = methodSymbol.TypeParameters.Select(
                (t) => t.AsType(typeParameters, buildType: true));
            parameter.Type.AsType(
                typeParameters.Concat(methodTypeParameters).ToArray(), buildType: true);
        }
        methodSymbol.ReturnType.AsType(type.GenericTypeArguments, buildType: true);

        BindingFlags bindingFlags = BindingFlags.Public |
            (methodSymbol.IsStatic ? BindingFlags.Static : BindingFlags.Instance);
        MethodInfo? methodInfo = type.GetMethods(bindingFlags)
            .FirstOrDefault((m) => m.Name == methodSymbol.Name &&
                m.GetParameters().Select((p) => p.Name).SequenceEqual(
                    methodSymbol.Parameters.Select((p) => p.Name)));
        return methodInfo ?? throw new InvalidOperationException(
                $"Method not found: {type.Name}.{methodSymbol.Name}");
    }

    /// <summary>
    /// Gets real or symbolic property info for a property symbol.
    /// </summary>
    public static PropertyInfo AsPropertyInfo(this IPropertySymbol propertySymbol)
    {
        Type type = propertySymbol.ContainingType.AsType();

        // Ensure the property type is built.
        propertySymbol.Type.AsType(type.GenericTypeArguments, buildType: true);

        BindingFlags bindingFlags = BindingFlags.Public |
            (propertySymbol.IsStatic ? BindingFlags.Static : BindingFlags.Instance);
        PropertyInfo? propertyInfo = type.GetProperty(propertySymbol.Name, bindingFlags);
        return propertyInfo ?? throw new InvalidOperationException(
                $"Property not found: {type.Name}.{propertySymbol.Name}");
    }
}
