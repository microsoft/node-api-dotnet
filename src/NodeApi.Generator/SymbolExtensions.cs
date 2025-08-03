// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Microsoft.CodeAnalysis;
using TypeInfo = System.Reflection.TypeInfo;

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
        else if (typeSymbol is IPointerTypeSymbol pointerTypeSymbol)
        {
            return pointerTypeSymbol.PointedAtType.AsType(genericTypeParameters, buildType)
                .MakePointerType();
        }

        if (typeSymbol is ITypeParameterSymbol typeParameterSymbol)
        {
            if (genericTypeParameters?.Length > typeParameterSymbol.Ordinal)
            {
                return genericTypeParameters[typeParameterSymbol.Ordinal];
            }
            else if (typeParameterSymbol.ContainingSymbol is IMethodSymbol methodSymbol)
            {
#if NETFRAMEWORK || NETSTANDARD
                // There's no .NET Framework API to make a generic method parameter type.
                // But it isn't needed anyway: the calling method will not request it.
                throw new InvalidOperationException(
                    $"Unknown generic type parameter {typeParameterSymbol} " +
                    $"in method {GetTypeSymbolFullName(typeParameterSymbol.ContainingType)}" +
                    $".{methodSymbol.Name}()");
#else
                return Type.MakeGenericMethodParameter(typeParameterSymbol.Ordinal);
#endif
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unknown generic type parameter {typeParameterSymbol} " +
                    $"in type {GetTypeSymbolFullName(typeParameterSymbol.ContainingType)}");
            }
        }

        if (typeSymbol is not INamedTypeSymbol namedTypeSymbol)
        {
            throw new NotSupportedException($"Unnamed types are not supported: {typeSymbol}");
        }

        string typeFullName = GetTypeSymbolFullName(namedTypeSymbol);
        ITypeSymbol[] genericArguments = namedTypeSymbol.TypeArguments.ToArray();
        Type? systemType = GetSystemType(namedTypeSymbol);
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
        typeSymbol.ContainingType?.AsType(genericTypeParameters, buildType);

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
                BuildReferencedTypes((INamedTypeSymbol)typeSymbol);

                symbolicType = typeBuilder.CreateTypeInfo()!;
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
            typeSymbol.ContainingNamespace.IsGlobalNamespace ?
            "" : typeSymbol.ContainingNamespace.ToString()!;
        string name = (ns.Length > 0 ? ns + "." : "") + typeSymbol.Name;

        if (typeSymbol.TypeParameters.Length > 0)
        {
            name += "`" + typeSymbol.TypeParameters.Length;
        }

        return name;
    }

    private static TypeInfo BuildSymbolicEnumType(
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
        return enumBuilder.CreateTypeInfo()!;
    }

    private static TypeAttributes GetTypeAttributes(ITypeSymbol typeSymbol)
    {
        TypeAttributes attributes = TypeAttributes.Public;

        switch (typeSymbol.TypeKind)
        {
            case TypeKind.Interface:
                attributes |= TypeAttributes.Interface | TypeAttributes.Abstract;
                break;

            case TypeKind.Class:
                if (typeSymbol.IsAbstract || typeSymbol.IsStatic)
                {
                    attributes |= TypeAttributes.Abstract;
                }
                if (typeSymbol.IsSealed || typeSymbol.IsStatic)
                {
                    attributes |= TypeAttributes.Sealed;
                }
                break;

            case TypeKind.Struct:
                attributes |= TypeAttributes.Sealed | TypeAttributes.BeforeFieldInit;
                break;

            case TypeKind.Delegate:
                attributes |= TypeAttributes.Sealed;
                break;

            case TypeKind.Enum:
                attributes |= TypeAttributes.Abstract;
                break;
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
                GetTypeAttributes(typeSymbol),
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
                        attribute.NamedArguments.Select((a) => a.Value.Value).ToArray()!);
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
        BuildReferencedTypes(typeSymbol);

        // Ensure this type is only built once.
        if (SymbolicTypes.TryGetValue(typeFullName, out thisType) && thisType is not TypeBuilder)
        {
            return thisType;
        }

        try
        {
            return typeBuilder.CreateTypeInfo()!;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create type info for type: {typeSymbol.Name}", ex);
        }
    }

    /// <summary>
    /// Gets the system type for a type symbol, if the type symbol represents a type that is already
    /// loaded in the current process and therefore doesn't need to be built as a symbolic type.
    /// </summary>
    private static Type? GetSystemType(INamedTypeSymbol typeSymbol)
    {
        string typeFullName = GetTypeSymbolFullName(typeSymbol);
        Type? systemType = typeof(object).Assembly.GetType(typeFullName) ??
            typeof(JSValue).Assembly.GetType(typeFullName) ??
            typeof(BigInteger).Assembly.GetType(typeFullName) ?? // System.Runtime.Numerics
            typeof(Stack<>).Assembly.GetType(typeFullName) ?? // System.Collections
            typeof(Expression).Assembly.GetType(typeFullName) ?? // System.Linq.Expressions
            typeof(System.Collections.ObjectModel.ReadOnlyDictionary<,>)
                .Assembly.GetType(typeFullName); // System.ObjectModel
        return systemType;
    }

    /// <summary>
    /// Ensures that a type symbol's type arguments (if any), base type, interface types,
    /// property types, and method parameter and return types are built before building the
    /// target type.
    /// </summary>
    /// <param name="typeSymbol">The symbol that is about to be built as a type.</param>
    /// <param name="referencingSymbols">Optional list of types that led to referencing this one,
    /// used to prevent infinite recursion.</param>
    private static void BuildReferencedTypes(
        INamedTypeSymbol typeSymbol, IEnumerable<INamedTypeSymbol>? referencingSymbols = null)
    {
        static void BuildType(
            INamedTypeSymbol typeSymbol,
            IEnumerable<INamedTypeSymbol> referencingSymbols)
        {
            // Recursively build the types that the current type references.
            BuildReferencedTypes(typeSymbol, referencingSymbols);

            // Now build the current type (if not already built).
            string typeFullName = GetTypeSymbolFullName(typeSymbol);
            if (SymbolicTypes.TryGetValue(typeFullName, out Type? type) &&
                type is TypeBuilder typeBuilder)
            {
                try
                {
                    typeBuilder.CreateTypeInfo();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to create type info for type: {typeSymbol.Name}", ex);
                }
            }
        }

        if (referencingSymbols?.Any(
            (s) => SymbolEqualityComparer.Default.Equals(s, typeSymbol)) == true)
        {
            // Skip self-referential type symbols.
            return;
        }

        referencingSymbols = (referencingSymbols ?? []).Append(typeSymbol);

        foreach (INamedTypeSymbol typeArgSymbol in
            typeSymbol.TypeArguments.OfType<INamedTypeSymbol>())
        {
            BuildType(typeArgSymbol, referencingSymbols);
        }

        if (GetSystemType(typeSymbol) != null)
        {
            // The base type, interface type(s), and parameter types must be already loaded.
            return;
        }

        if (typeSymbol.BaseType != null)
        {
            BuildType(typeSymbol.BaseType, referencingSymbols);
        }

        foreach (INamedTypeSymbol interfaceTypeSymbol in typeSymbol.Interfaces)
        {
            BuildType(interfaceTypeSymbol, referencingSymbols);
        }

        // This also covers property and event types via their get/set/add/remove methods.
        foreach (INamedTypeSymbol? parameterTypeSymbol in typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where((m) => m.DeclaredAccessibility == Accessibility.Public)
            .SelectMany((m) => m.Parameters.Select((p) => p.Type).Append(m.ReturnType))
            .OfType<INamedTypeSymbol>()
            .Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol?>())
        {
            BuildType(parameterTypeSymbol!, referencingSymbols);
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
            .Where((m) => m.DeclaredAccessibility == Accessibility.Public ||
                (m.DeclaredAccessibility == Accessibility.Private &&
                (m is IMethodSymbol ms && ms.ExplicitInterfaceImplementations.Length > 0) ||
                (m is IPropertySymbol ps && ps.ExplicitInterfaceImplementations.Length > 0))))
        {
            if (memberSymbol is IMethodSymbol constructorSymbol &&
                constructorSymbol.MethodKind == MethodKind.Constructor)
            {
                BuildSymbolicConstructor(typeBuilder, constructorSymbol, genericTypeParameters);
            }
            else if (memberSymbol is IMethodSymbol methodSymbol &&
                (methodSymbol.MethodKind == MethodKind.Ordinary ||
                methodSymbol.MethodKind == MethodKind.ExplicitInterfaceImplementation ||
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
                TypeAttributes attributes = GetTypeAttributes(nestedTypeSymbol);
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
        // All nonstatic methods are declared virtual on symbolic types.
        // This allows any struct/class methods to implement interface methods.
        MethodAttributes attributes =
            (methodSymbol.DeclaredAccessibility == Accessibility.Public ?
                MethodAttributes.Public : MethodAttributes.Private) |
            (methodSymbol.IsStatic ? MethodAttributes.Static : MethodAttributes.Virtual) |
            (methodSymbol.IsAbstract ? MethodAttributes.Abstract : default) |
            (methodSymbol.ExplicitInterfaceImplementations.Length > 0 ?
                MethodAttributes.Final : default) |
            (isDelegateMethod ? MethodAttributes.HideBySig : default);

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
        else if (!methodSymbol.IsAbstract)
        {
            // Emit a minimal method body.
            methodBuilder.GetILGenerator().Emit(OpCodes.Ret);
        }

        if (methodSymbol.ExplicitInterfaceImplementations.Length > 0)
        {
            MethodInfo interfaceMethod =
                methodSymbol.ExplicitInterfaceImplementations[0].AsMethodInfo();
            typeBuilder.DefineMethodOverride(methodBuilder, interfaceMethod);
        }
    }

    private static void BuildSymbolicProperty(
        TypeBuilder typeBuilder,
        IPropertySymbol propertySymbol,
        Type[]? genericTypeParameters)
    {
        MethodAttributes attributes = MethodAttributes.SpecialName |
            (propertySymbol.DeclaredAccessibility == Accessibility.Public ?
                MethodAttributes.Public : MethodAttributes.Private) |
            (propertySymbol.IsStatic ? MethodAttributes.Static : MethodAttributes.Virtual) |
            (propertySymbol.IsAbstract ? MethodAttributes.Abstract : default) |
            (propertySymbol.IsVirtual ? MethodAttributes.Virtual : default) |
            (propertySymbol.ExplicitInterfaceImplementations.Length > 0 ?
                MethodAttributes.Final : default);

        PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(
            propertySymbol.Name,
            PropertyAttributes.None,
            propertySymbol.Type.AsType(genericTypeParameters, buildType: false),
            propertySymbol.Parameters.Select(
                (p) => p.Type.AsType(genericTypeParameters, buildType: false)).ToArray());

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
            if (!propertySymbol.IsAbstract) getMethodBuilder.GetILGenerator().Emit(OpCodes.Ret);
            propertyBuilder.SetGetMethod(getMethodBuilder);

            if (propertySymbol.ExplicitInterfaceImplementations.Length > 0)
            {
                MethodInfo interfaceGetMethod =
                    propertySymbol.ExplicitInterfaceImplementations[0].GetMethod!.AsMethodInfo();
                typeBuilder.DefineMethodOverride(getMethodBuilder, interfaceGetMethod);
            }
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
            if (!propertySymbol.IsAbstract) setMethodBuilder.GetILGenerator().Emit(OpCodes.Ret);
            propertyBuilder.SetSetMethod(setMethodBuilder);

            if (propertySymbol.ExplicitInterfaceImplementations.Length > 0)
            {
                MethodInfo interfaceSetMethod =
                    propertySymbol.ExplicitInterfaceImplementations[0].SetMethod!.AsMethodInfo();
                typeBuilder.DefineMethodOverride(setMethodBuilder, interfaceSetMethod);
            }
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

        Type[] parameterTypes = methodSymbol.Parameters
            .Select((p) => p.Type.AsType(type.GenericTypeArguments, buildType: true))
            .ToArray();

        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance;
        ConstructorInfo? constructorInfo = type.GetConstructors(bindingFlags)
            .FirstOrDefault((c) => c.GetParameters().Select((p) => p.ParameterType.FullName)
                .SequenceEqual(parameterTypes.Select((t) => t.FullName)));
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
#if NETFRAMEWORK || NETSTANDARD
        // .NET Framework cannot make generic method parameter types.
        IEnumerable<Type> methodTypeParameters = [];
#else
        IEnumerable<Type> methodTypeParameters = methodSymbol.TypeParameters.Select(
            (t) => t.AsType(typeParameters, buildType: true));
#endif
        typeParameters = typeParameters.Concat(methodTypeParameters).ToArray();

        Type[] parameterTypes = methodSymbol.Parameters
            .Select((p) => p.Type.AsType(typeParameters, buildType: true))
            .ToArray();
        Type returnType = methodSymbol.ReturnType.AsType(
            type.GenericTypeArguments, buildType: true);

        BindingFlags bindingFlags = BindingFlags.Public |
            (methodSymbol.IsStatic ? BindingFlags.Static : BindingFlags.Instance);
        MethodInfo? methodInfo = type.GetMethods(bindingFlags)
            .FirstOrDefault((m) => m.Name == methodSymbol.Name &&
                m.GetParameters().Select((p) => p.ParameterType.FullName)
                    .SequenceEqual(parameterTypes.Select((t) => t.FullName)) &&
                m.ReturnType.FullName == returnType.FullName);
        return methodInfo ?? throw new InvalidOperationException(
                $"Method not found: {type.Name}.{methodSymbol.Name}");
    }

    /// <summary>
    /// Gets real or symbolic property info for a property symbol.
    /// </summary>
    public static PropertyInfo AsPropertyInfo(this IPropertySymbol propertySymbol)
    {
        Type type = propertySymbol.ContainingType.AsType();

        Type[] parameterTypes = propertySymbol.Parameters.Select(
            (p) => p.Type.AsType(type.GenericTypeArguments, buildType: true)).ToArray();

        BindingFlags bindingFlags = BindingFlags.Public |
            (propertySymbol.IsStatic ? BindingFlags.Static : BindingFlags.Instance);
        PropertyInfo? propertyInfo = type.GetProperty(
            propertySymbol.Name,
            bindingFlags,
            binder: null,
            returnType: null,
            parameterTypes,
            modifiers: null);
        return propertyInfo ?? throw new InvalidOperationException(
                $"Property not found: {type.Name}.{propertySymbol.Name}");
    }
}
