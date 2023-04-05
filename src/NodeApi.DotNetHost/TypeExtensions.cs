// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Reflection;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Helper methods for getting public properties and methods, used for building dynamic
/// marshaling expressions.
/// </summary>
/// <remarks>
/// The extension methods here throw <see cref="MissingMemberException" /> if the requested
/// member is not found, which allows for easier diagnosis compared to the null return
/// values from the regular reflection APIs.
/// </remarks>
internal static class TypeExtensions
{
    private const BindingFlags ExactPublic = BindingFlags.Public | BindingFlags.ExactBinding;
    private const BindingFlags PublicStatic = ExactPublic | BindingFlags.Static;
    private const BindingFlags PublicInstance = ExactPublic | BindingFlags.Instance;

#if !NETFRAMEWORK
    private static readonly Type[] s_oneGenericMethodParam = new Type[]
    {
        Type.MakeGenericMethodParameter(0),
    };
    private static readonly Type[] s_twoGenericMethodParams = new Type[]
    {
        Type.MakeGenericMethodParameter(0),
        Type.MakeGenericMethodParameter(1),
    };
#endif

    public static object CreateInstance(
        this Type type, Type[]? types = null, object?[]? args = null)
        => type.GetInstanceConstructor(types ?? Array.Empty<Type>()).Invoke(args);

    public static ConstructorInfo GetInstanceConstructor(this Type type, Type[] types)
        => type.GetConstructor(types) ??
            throw new MissingMemberException($"Constructor not found on type {type.Name}.");

    public static PropertyInfo GetStaticProperty(this Type type, string name)
        => type.GetProperty(name, PublicStatic) ??
            throw new MissingMemberException(
                $"Static property {name} not found on type {type.Name}.");

    public static PropertyInfo GetInstanceProperty(this Type type, string name)
        => type.GetProperty(name, PublicInstance) ??
            throw new MissingMemberException(
                $"Instance property {name} not found on type {type.Name}.");

    public static PropertyInfo GetIndexer(this Type type, Type indexType)
        => type.GetProperty("Item", PublicInstance, null, null, new[] { indexType }, null) ??
            throw new MissingMemberException(
                $"Indexer not found on type {type.Name}.");

    public static MethodInfo GetStaticMethod(this Type type, string name)
        => type.GetMethod(name, PublicStatic) ??
            throw new MissingMemberException(
                $"Static method {name} not found on type {type.Name}.");

    public static MethodInfo GetStaticMethod(this Type type, string name, Type[] types)
        => type.GetMethod(name, PublicStatic, binder: null, types, modifiers: null) ??
            throw new MissingMemberException(
                $"Static method {name}({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");

    public static MethodInfo GetInstanceMethod(this Type type, string name)
        => type.GetMethod(name, PublicInstance) ??
            throw new MissingMemberException(
                $"Instance method {name} not found on type {type.Name}.");

    public static MethodInfo GetInstanceMethod(this Type type, string name, Type[] types)
        => type.GetMethod(name, PublicInstance, binder: null, types, modifiers: null) ??
            throw new MissingMemberException(
                $"Instance method {name}({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");

    public static MethodInfo GetExplicitConversion(
    this Type declaringType, Type fromType, Type toType)
    => declaringType.GetMethods(PublicStatic)
        .Where((m) => m.Name == "op_Explicit" && m.ReturnType == toType &&
            m.GetParameters()[0].ParameterType == fromType).SingleOrDefault() ??
        throw new MissingMemberException(
            $"Explicit conversion method for {fromType.Name}->{toType.Name} " +
            $"not found on type {declaringType.Name}.");

    public static MethodInfo GetImplicitConversion(
        this Type declaringType, Type fromType, Type toType)
        => declaringType.GetMethods(PublicStatic)
            .Where((m) => m.Name == "op_Implicit" && m.ReturnType == toType &&
                m.GetParameters()[0].ParameterType == fromType).SingleOrDefault() ??
            throw new MissingMemberException(
                $"Explicit conversion method for {fromType.Name}->{toType.Name} " +
                $"not found on type {declaringType.Name}.");

#if NETFRAMEWORK

    public static MethodInfo GetStaticMethod(
        this Type type, string name, Type[] types, Type genericArg)
    {
        return type.GetMethods(PublicStatic)
            .Where((m) => m.Name == name && m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == 1 &&
                ParameterTypesEqual(m.GetParameters(), types))
            .Select((m) => m.MakeGenericMethod(genericArg))
            .SingleOrDefault() ??
            throw new MissingMemberException(
                $"Static method {name}<>({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");
    }

    public static MethodInfo GetStaticMethod(
        this Type type, string name, Type[] types, Type genericArgOne, Type genericArgTwo)
    {
        return type.GetMethods(PublicStatic)
            .Where((m) => m.Name == name && m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == 2 &&
                ParameterTypesEqual(m.GetParameters(), types))
            .Select((m) => m.MakeGenericMethod(genericArgOne, genericArgTwo))
            .SingleOrDefault() ??
            throw new MissingMemberException(
                $"Static method {name}<,>({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");
    }

    public static MethodInfo GetInstanceMethod(
        this Type type, string name, Type[] types, Type genericArg)
    {
        return type.GetMethods(PublicInstance)
            .Where((m) => m.Name == name && m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == 1 &&
                ParameterTypesEqual(m.GetParameters(), types))
            .Select((m) => m.MakeGenericMethod(genericArg))
            .SingleOrDefault() ??
            throw new MissingMemberException(
                $"Instance method {name}<>({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");
    }

    public static MethodInfo GetInstanceMethod(
        this Type type, string name, Type[] types, Type genericArgOne, Type genericArgTwo)
    {
        return type.GetMethods(PublicInstance)
            .Where((m) => m.Name == name && m.IsGenericMethodDefinition &&
                m.GetGenericArguments().Length == 2 &&
                ParameterTypesEqual(m.GetParameters(), types))
            .Select((m) => m.MakeGenericMethod(genericArgOne, genericArgTwo))
            .SingleOrDefault() ??
            throw new MissingMemberException(
                $"Instance method {name}<,>({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");
    }

    private static bool ParameterTypesEqual(ParameterInfo[] parameters, Type[] types)
    {
        // Reflection in .NET Framework does not support generic perameters as well.
        // So it's necessary to scan all the methods and try to match parameter types
        // by comparing generic type definitions.

        if (parameters.Length != types.Length) return false;

        for (int i = 0; i <  parameters.Length; i++)
        {
            Type parameterType = parameters[i].ParameterType;
            if (parameterType.IsGenericType)
            {
                if (!parameterType.GetGenericTypeDefinition().Equals(types[i]))
                {
                    return false;
                }
            }
            else if (!parameterType.Equals(types[i]))
            {
                return false;
            }
        }

        return true;
    }

#else // !NETFRAMEWORK

    public static MethodInfo GetStaticMethod(
        this Type type, string name, Type[] types, Type genericArg)
        => type.GetMethod(name, PublicStatic, binder: null, MakeGenericTypes(types, s_oneGenericMethodParam), modifiers: null)
            ?.MakeGenericMethod(genericArg) ??
            throw new MissingMemberException(
                $"Static method {name}<T>({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");

    public static MethodInfo GetStaticMethod(
        this Type type, string name, Type[] types, Type genericArgOne, Type genericArgTwo)
        => type.GetMethod(name, PublicStatic, binder: null, MakeGenericTypes(types, s_twoGenericMethodParams), modifiers: null)
            ?.MakeGenericMethod(genericArgOne, genericArgTwo) ??
            throw new MissingMemberException(
                $"Static method {name}<K,V>({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");

    public static MethodInfo GetInstanceMethod(
        this Type type, string name, Type[] types, Type genericArg)
        => type.GetMethod(name, PublicInstance, binder: null, MakeGenericTypes(types, s_oneGenericMethodParam), modifiers: null)
            ?.MakeGenericMethod(genericArg) ??
            throw new MissingMemberException(
                $"Instance method {name}<T>({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");

    public static MethodInfo GetInstanceMethod(
        this Type type, string name, Type[] types, Type genericArgOne, Type genericArgTwo)
        => type.GetMethod(name, PublicInstance, binder: null, MakeGenericTypes(types, s_twoGenericMethodParams), modifiers: null)
            ?.MakeGenericMethod(genericArgOne, genericArgTwo) ??
            throw new MissingMemberException(
                $"Instance method {name}<K,V>({string.Join(", ", types.Select((t) => t.Name))}) " +
                $"not found on type {type.Name}.");

    private static Type[] MakeGenericTypes(Type[] types, Type[] typeArgs)
    {
        return types
            .Select((t, i) =>
            {
                if (!t.IsGenericTypeDefinition) return t;

                if (typeArgs.Length == 1)
                {
                    return t.MakeGenericType(typeArgs);
                }

                if (t.GetGenericArguments().Length == typeArgs.Length)
                {
                    return t.MakeGenericType(typeArgs);
                }
                else
                {
                    // This assumption only works if type arguments are alternating as in
                    // JSRuntimeContext.GetOrCreateCollectionWrapper!
                    return t.MakeGenericType(typeArgs[(i + 1) % typeArgs.Length]);
                }
            })
            .ToArray();
    }

#endif // !NETFRAMEWORK
}
