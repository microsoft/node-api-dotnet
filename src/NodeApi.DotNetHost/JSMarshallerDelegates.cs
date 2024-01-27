// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Contains definitions and factory methods for lambda expression delegates used in JS marshalling.
/// </summary>
/// <remarks>
/// When constructing a lambda expression, the <see cref="Expression.Lambda()" /> method takes
/// an optional first parameter that is the delegate type of the resulting expression. Any JS
/// marshalling code in this library should always use this helper class to explicitly supply the
/// delegate type when constructing a lambda expression.
/// <para />
/// If that delegate type parameter is not supplied, then the expression library will automatically
/// select an appropriate overload of the generic `Action` or `Func` delegate, or will _generate_
/// a new delegate type if the generic delegates aren't suitable. But generating a delegate type
/// there can be expensive, and can cause problems because the delegate types are generated in a
/// non-collectible assembly load context, while the referenced JS types may be in a collectible
/// assembly load context.
/// <para />
/// This class avoids dynamic generation of delegate types in more but not all cases for JS
/// marshalling, and when it does generate them the delegate types are emitted in the current
/// assembly load context, which may be collectible.
/// <para />
/// This class is similar in purpose to the internal `DelegateHelpers` class in System.Linq.Expressions
/// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Linq.Expressions/src/System/Linq/Expressions/Compiler/DelegateHelpers.cs
/// but optimized for use with JS marshalling.
/// </remarks>
internal class JSMarshallerDelegates
{
    private readonly AssemblyBuilder _assemblyBuilder;
    private readonly ModuleBuilder _moduleBuilder;
    private int _index = 0;

    public JSMarshallerDelegates()
    {
        _assemblyBuilder = JSMarshaller.CreateAssemblyBuilder(typeof(JSMarshallerDelegates));
        _moduleBuilder = _assemblyBuilder.DefineDynamicModule(typeof(JSMarshallerDelegates).Name);
    }

    private delegate JSValue FromJS<T>(T thisParameter, JSCallbackArgs args);

    public static Type GetFromJSDelegateType(Type thisParameterType)
    {
        return typeof(FromJS<>).MakeGenericType(thisParameterType);
    }

    private delegate void ToJSVoid();
    private delegate void ToJSVoid<T1>(T1 arg1);
    private delegate void ToJSVoid<T1, T2>(T1 arg1, T2 arg2);
    private delegate void ToJSVoid<T1, T2, T3>(T1 arg1, T2 arg2, T3 arg3);
    private delegate void ToJSVoid<T1, T2, T3, T4>(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    private delegate void ToJSVoid<T1, T2, T3, T4, T5>(
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);

    private delegate TResult ToJS<TResult>();
    private delegate TResult ToJS<T1, TResult>(T1 arg1);
    private delegate TResult ToJS<T1, T2, TResult>(T1 arg1, T2 arg2);
    private delegate TResult ToJS<T1, T2, T3, TResult>(T1 arg1, T2 arg2, T3 arg3);
    private delegate TResult ToJS<T1, T2, T3, T4, TResult>(
        T1 arg1, T2 arg2, T3 arg3, T4 arg4);
    private delegate TResult ToJS<T1, T2, T3, T4, T5, TResult>(
        T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
    private const int MaxGenericDelegateParameters = 5;

    public Type GetToJSDelegateType(Type returnType, params ParameterExpression[] parameters)
    {
        if (parameters.Length > MaxGenericDelegateParameters ||
            parameters.Any((p) => p.IsByRef))
        {
            return MakeCustomDelegate(
                parameters.Select((p) => p.IsByRef ? p.Type.MakeByRefType() : p.Type).ToArray(),
                returnType);
        }

        if (returnType == typeof(void))
        {
            return parameters.Length switch
            {
                0 => typeof(ToJSVoid),
                1 => typeof(ToJSVoid<>).MakeGenericType(parameters[0].Type),
                2 => typeof(ToJSVoid<,>).MakeGenericType(parameters[0].Type, parameters[1].Type),
                3 => typeof(ToJSVoid<,,>).MakeGenericType(
                    parameters[0].Type, parameters[1].Type, parameters[2].Type),
                4 => typeof(ToJSVoid<,,,>).MakeGenericType(
                    parameters[0].Type, parameters[1].Type, parameters[2].Type, parameters[3].Type),
                5 => typeof(ToJSVoid<,,,,>).MakeGenericType(
                    parameters[0].Type,
                    parameters[1].Type,
                    parameters[2].Type,
                    parameters[3].Type,
                    parameters[4].Type),
                _ => throw new NotSupportedException("Method has too many parameters."),
            };
        }

        return parameters.Length switch
        {
            0 => typeof(ToJS<>).MakeGenericType(returnType),
            1 => typeof(ToJS<,>).MakeGenericType(parameters[0].Type, returnType),
            2 => typeof(ToJS<,,>).MakeGenericType(
                parameters[0].Type, parameters[1].Type, returnType),
            3 => typeof(ToJS<,,,>).MakeGenericType(
                parameters[0].Type, parameters[1].Type, parameters[2].Type, returnType),
            4 => typeof(ToJS<,,,,>).MakeGenericType(
                parameters[0].Type,
                parameters[1].Type,
                parameters[2].Type,
                parameters[3].Type,
                returnType),
            5 => typeof(ToJS<,,,,,>).MakeGenericType(
                parameters[0].Type,
                parameters[1].Type,
                parameters[2].Type,
                parameters[3].Type,
                parameters[4].Type,
                returnType),
            _ => throw new NotSupportedException("Method has too many parameters."),
        };
    }

    private static readonly Type[] s_delegateCtorSignature = { typeof(object), typeof(IntPtr) };

    private TypeInfo MakeCustomDelegate(Type[] parameterTypes, Type returnType)
    {
        // TODO: Consider caching custom delegate types?

        const TypeAttributes typeAttributes =
            TypeAttributes.Class |
            TypeAttributes.Public |
            TypeAttributes.Sealed |
            TypeAttributes.AnsiClass |
            TypeAttributes.AutoClass;
        const MethodAttributes ctorAttributes =
            MethodAttributes.RTSpecialName |
            MethodAttributes.HideBySig |
            MethodAttributes.Public;
        const MethodImplAttributes implAttributes =
            MethodImplAttributes.Runtime |
            MethodImplAttributes.Managed;
        const MethodAttributes invokeAttributes =
            MethodAttributes.Public |
            MethodAttributes.HideBySig |
            MethodAttributes.NewSlot |
            MethodAttributes.Virtual;

        int index = Interlocked.Increment(ref _index);
        string typeName = $"Delegate{parameterTypes.Length + 1}${index}";

        TypeBuilder builder = _moduleBuilder.DefineType(
            typeName, typeAttributes, parent: typeof(MulticastDelegate));
        CallingConventions callingConvention = CallingConventions.Standard;
        builder.DefineConstructor(ctorAttributes, callingConvention, s_delegateCtorSignature)
            .SetImplementationFlags(implAttributes);
        builder.DefineMethod("Invoke", invokeAttributes, returnType, parameterTypes)
            .SetImplementationFlags(implAttributes);
        return builder.CreateTypeInfo()!;
    }
}
