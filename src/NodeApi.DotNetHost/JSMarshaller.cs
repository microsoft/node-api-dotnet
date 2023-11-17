// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Generates expressions and delegates that support marshaling between .NET and JS.
/// </summary>
/// <remarks>
/// Most marshaling logic is generated in the form of lambda expressions. The expressions
/// can then be compiled into delegates at runtime, or written out as source code by a
/// compile time source-generator to support faster startup and AOT compiled binaries.
/// <para/>
/// All methods on this class are thread-safe.
/// </remarks>
public class JSMarshaller
{
    /// <summary>
    /// Prefix applied to the the name of out parameters when building expressions. Expressions
    /// do not distinguish between ref and out parameters, but the source generator needs to use
    /// the correct keyword.
    /// </summary>
    public const string OutParameterPrefix = "__out_";

    /// <summary>
    /// When a method has `ref` and/or `out` parameters, the results are returned as an object
    /// with properties for each of the `ref`/`out` parameters along with a `result` property
    /// for the actual return value (if not void).
    /// </summary>
    public const string ResultPropertyName = "result";

    [ThreadStatic]
    private static JSMarshaller? s_current;

    public static JSMarshaller Current
    {
        get => s_current ??
            throw new InvalidOperationException("No current JSMarshaller instance.");
        internal set => s_current = value;
    }

    public JSMarshaller()
    {
        _interfaceMarshaller = new(() => new JSInterfaceMarshaller(),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _delegates = new(() => new JSMarshallerDelegates(),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private readonly Lazy<JSInterfaceMarshaller> _interfaceMarshaller;
    private readonly Lazy<JSMarshallerDelegates> _delegates;

    private readonly ConcurrentDictionary<Type, Delegate> _fromJSDelegates = new();
    private readonly ConcurrentDictionary<Type, Delegate> _toJSDelegates = new();
    private readonly ConcurrentDictionary<Type, LambdaExpression> _fromJSExpressions = new();
    private readonly ConcurrentDictionary<Type, LambdaExpression> _toJSExpressions = new();
    private readonly ConcurrentDictionary<MethodInfo, Delegate> _jsMethodDelegates = new();

    private static readonly ParameterExpression s_argsParameter =
        Expression.Parameter(typeof(JSCallbackArgs), "__args");
    private static readonly IEnumerable<ParameterExpression> s_argsArray =
        new[] { s_argsParameter };

    // Cache some reflected members that are frequently referenced in expressions.

    private static readonly PropertyInfo s_context =
        typeof(JSRuntimeContext).GetStaticProperty(nameof(JSRuntimeContext.Current))!;

    private static readonly PropertyInfo s_moduleContext =
        typeof(JSModuleContext).GetStaticProperty(nameof(JSModuleContext.Current))!;

    private static readonly PropertyInfo s_valueItem =
        typeof(JSValue).GetIndexer(typeof(string))!;

    private static readonly MethodInfo s_isNullOrUndefined =
        typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.IsNullOrUndefined))!;

    private static readonly PropertyInfo s_callbackArg =
        typeof(JSCallbackArgs).GetIndexer(typeof(int))!;

    private static readonly MethodInfo s_unwrap =
        typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.Unwrap))!;

    private static readonly MethodInfo s_tryUnwrap =
        typeof(JSNativeApi).GetStaticMethod(
            nameof(JSNativeApi.TryUnwrap), new[] { typeof(JSValue) })!;

    private static readonly MethodInfo s_getOrCreateObjectWrapper =
        typeof(JSRuntimeContext).GetInstanceMethod(nameof(JSRuntimeContext.GetOrCreateObjectWrapper))!;

    private static readonly MethodInfo s_asVoidPromise =
        typeof(TaskExtensions).GetStaticMethod(
            nameof(TaskExtensions.AsPromise), new[] { typeof(Task) })!;

    /// <summary>
    /// Gets or sets a value indicating whether the marshaller automatically converts
    /// casing between TitleCase .NET member names and camelCase JavaScript member names.
    /// </summary>
    public bool AutoCamelCase { get; set; }

    private string ToCamelCase(string name)
    {
        if (!AutoCamelCase) return name;

        StringBuilder sb = new(name);
        sb[0] = char.ToLowerInvariant(sb[0]);
        return sb.ToString();
    }

    /// <summary>
    /// Converts a value to a JS value.
    /// </summary>
    public JSValue From<T>(T value)
    {
        JSValue.From<T> converter = GetToJSValueDelegate<T>();
        return converter(value);
    }

    /// <summary>
    /// Converts a JS value to a requested type.
    /// </summary>
    public T To<T>(JSValue value)
    {
        JSValue.To<T> converter = GetFromJSValueDelegate<T>();
        return converter(value);
    }

    /// <summary>
    /// Checks whether a type is converted to a JavaScript built-in type.
    /// </summary>
    internal static bool IsConvertedType(Type type)
    {
        if (type.IsPrimitive ||
            type == typeof(string) ||
            type == typeof(Array) ||
            type == typeof(Task) ||
            type == typeof(DateTime))
        {
            return true;
        }

        if (type.IsGenericType)
        {
            type = type.GetGenericTypeDefinition();
        }

        if (type.IsGenericTypeDefinition &&
            (type == typeof(CancellationToken) ||
            type == typeof(IEnumerable<>) ||
            type == typeof(IAsyncEnumerable<>) ||
            type == typeof(ICollection<>) ||
            type == typeof(IReadOnlyCollection<>) ||
            type == typeof(ISet<>) ||
#if !NETFRAMEWORK
            type == typeof(IReadOnlySet<>) ||
#endif
            type == typeof(IList<>) ||
            type == typeof(IReadOnlyList<>) ||
            type == typeof(IDictionary<,>) ||
            type == typeof(IReadOnlyDictionary<,>)))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Converts from a JS value to a specified type.
    /// </summary>
    /// <typeparam name="T">The type the value will be converted to.</typeparam>
    /// <exception cref="NotSupportedException">The type cannot be converted.</exception>
    public T FromJS<T>(JSValue value) => GetFromJSValueDelegate<T>()(value);

    /// <summary>
    /// Converts from a specified type to a JS value.
    /// </summary>
    /// <typeparam name="T">The type the value will be converted from.</typeparam>
    /// <exception cref="NotSupportedException">The type cannot be converted.</exception>
    public JSValue ToJS<T>(T value) => GetToJSValueDelegate<T>()(value);

    /// <summary>
    /// Gets a delegate that converts from a JS value to a specified type.
    /// </summary>
    /// <typeparam name="T">The type the value will be converted to.</typeparam>
    /// <exception cref="NotSupportedException">The type cannot be converted.</exception>
    /// <remarks>
    /// Type conversion delegates are built on created and then cached, so it is efficient
    /// to call this method multiple times for the same type.
    /// </remarks>
    public JSValue.To<T> GetFromJSValueDelegate<T>()
    {
        return (JSValue.To<T>)_fromJSDelegates.GetOrAdd(typeof(T), (toType) =>
        {
            LambdaExpression fromJSExpression = GetFromJSValueExpression(toType);
            return fromJSExpression.Compile();
        });
    }

    /// <summary>
    /// Gets a delegate that converts from a specified type to a JS value.
    /// </summary>
    /// <typeparam name="T">The type the value will be converted from.</typeparam>
    /// <exception cref="NotSupportedException">The type cannot be converted.</exception>
    /// <remarks>
    /// Type conversion delegates are built on demand and then cached, so it is efficient
    /// to call this method multiple times for the same type.
    /// </remarks>
    public JSValue.From<T> GetToJSValueDelegate<T>()
    {
        return (JSValue.From<T>)_toJSDelegates.GetOrAdd(typeof(T), (fromType) =>
        {
            LambdaExpression toJSExpression = GetToJSValueExpression(fromType);
            return toJSExpression.Compile();
        });
    }

    /// <summary>
    /// Gets a delegate for a .NET adapter to a JS method. When invoked, the adapter will
    /// marshal the arguments to JS, invoke the JS method, then marshal the return value
    /// (or exception) back to .NET.
    /// </summary>
    /// <remarks>
    /// The delegate has an extra initial argument of type <see cref="JSValue"/> that is
    /// the JS object on which the method will be invoked.
    /// </remarks>
    public Delegate GetToJSMethodDelegate(MethodInfo method)
    {
        return _jsMethodDelegates.GetOrAdd(method, (method) =>
        {
            LambdaExpression jsMethodExpression = BuildToJSMethodExpression(method);
            return jsMethodExpression.Compile();
        });
    }

    /// <summary>
    /// Gets a delegate for a .NET adapter to a JS method, using the current thread
    /// JS marshaller instance.
    /// </summary>
    public static Delegate StaticGetToJSMethodDelegate(MethodInfo method)
        => Current.GetToJSMethodDelegate(method);

    /// <summary>
    /// Gets a lambda expression that converts from a JS value to a specified type.
    /// </summary>
    /// <param name="toType">The type the value will be converted to.</param>
    /// <exception cref="NotSupportedException">The type cannot be converted.</exception>
    /// <remarks>
    /// Type conversion expressions are built on demand and then cached, so it is efficient
    /// to call this method multiple times for the same type.
    /// </remarks>
    public LambdaExpression GetFromJSValueExpression(Type toType)
    {
        if (toType is null) throw new ArgumentNullException(nameof(toType));

        try
        {
            if (toType == typeof(Task) ||
                (toType.IsGenericType && toType.GetGenericTypeDefinition() == typeof(Task<>)))
            {
                return _fromJSExpressions.GetOrAdd(toType, BuildConvertFromJSPromiseExpression);
            }
            else
            {
                return _fromJSExpressions.GetOrAdd(toType, BuildConvertFromJSValueExpression);
            }
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build expression for conversion from JS value.", toType, ex);
        }
    }

    /// <summary>
    /// Gets a lambda expression that converts from a specified type to a JS value.
    /// </summary>
    /// <param name="fromType">The type the value will be converted from.</param>
    /// <exception cref="NotSupportedException">The type cannot be converted.</exception>
    /// <remarks>
    /// Type conversion expressions are built on demand and then cached, so it is efficient
    /// to call this method multiple times for the same type.
    /// </remarks>
    public LambdaExpression GetToJSValueExpression(Type fromType)
    {
        if (fromType is null) throw new ArgumentNullException(nameof(fromType));

        try
        {
            if (fromType == typeof(Task) ||
                (fromType.IsGenericType && fromType.GetGenericTypeDefinition() == typeof(Task<>)))
            {
                return _fromJSExpressions.GetOrAdd(fromType, BuildConvertToJSPromiseExpression);
            }
            else
            {
                return _toJSExpressions.GetOrAdd(fromType, BuildConvertToJSValueExpression);
            }
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build expression for conversion to JS value.", fromType, ex);
        }
    }

    /// <summary>
    /// Builds a lambda expression for a JS callback adapter that constructs an instance of
    /// a .NET class. When invoked, the expression will marshal the constructor arguments
    /// from JS, invoke the constructor, then return the new instance of the class.
    /// </summary>
    /// <remarks>
    /// The returned expression takes a <see cref="JSCallbackArgs"/> parameter and returns an
    /// instance of the class as an external JS value. The lambda expression may be converted to
    /// a delegate with <see cref="LambdaExpression.Compile()"/>, and used as the constructor
    /// callback parameter for a <see cref="JSClassBuilder{T}"/>.
    /// </remarks>
#pragma warning disable CA1822 // Mark members as static
    public Expression<JSCallback> BuildFromJSConstructorExpression(ConstructorInfo constructor)
    {
        if (constructor is null) throw new ArgumentNullException(nameof(constructor));

        /*
         * ConstructorClass(JSCallbackArgs __args)
         * {
         *     var param0Name = (Param0Type)__args[0];
         *     ...
         *     var __result = new ConstructorClass(param0, ...);
         *     return JSValue.CreateExternal(__result);
         * }
         */

        ParameterInfo[] parameters = constructor.GetParameters();
        ParameterExpression[] argVariables = new ParameterExpression[parameters.Length];
        IEnumerable<ParameterExpression> variables;
        List<Expression> statements = new(parameters.Length + 2);

        for (int i = 0; i < parameters.Length; i++)
        {
            argVariables[i] = Expression.Variable(parameters[i].ParameterType, parameters[i].Name);
            statements.Add(Expression.Assign(argVariables[i],
                BuildArgumentExpression(i, parameters[i])));
        }

        ParameterExpression resultVariable = Expression.Variable(
            constructor.DeclaringType!, "__result");
        variables = argVariables.Append(resultVariable);
        statements.Add(Expression.Assign(resultVariable,
            Expression.New(constructor, argVariables)));

        MethodInfo createExternalMethod = typeof(JSValue)
            .GetStaticMethod(nameof(JSValue.CreateExternal));
        statements.Add(Expression.Call(
            createExternalMethod, resultVariable));

        return (Expression<JSCallback>)Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables, statements),
            name: "new_" + FullTypeName(constructor.DeclaringType!),
            parameters: s_argsArray);

    }
#pragma warning restore CA1822 // Mark members as static

    /// <summary>
    /// Builds a lambda expression for a JS callback adapter to a .NET method. When invoked,
    /// the expression will marshal the arguments from JS, invoke the method, then marshal the
    /// return value (or exception) back to JS.
    /// </summary>
    /// <remarks>
    /// The returned expression takes a single <see cref="JSCallbackArgs"/> parameter and
    /// returns a <see cref="JSValue"/>. For instance methods, the `this` argument for the JS
    /// callback args must be a JS object that wraps a .NET object matching the method's
    /// declaring type. The lambda expression may be converted to a <see cref="JSCallback"/>
    /// delegate with <see cref="LambdaExpression.Compile()"/>.
    /// </remarks>
    public Expression<JSCallback> BuildFromJSMethodExpression(MethodInfo method)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));
        else if (method.IsGenericMethodDefinition) throw new ArgumentException(
            "Construct a generic method definition from the method first.", nameof(method));

        try
        {
            return method.IsStatic
                ? BuildFromJSStaticMethodExpression(method)
                : BuildFromJSInstanceMethodExpression(method);
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build JS callback adapter expression for .NET method.", method, ex);
        }
    }

    /// <summary>
    /// Builds a lambda expression for a JS callback adapter to a .NET property getter. When
    /// invoked, the expression will get the property value and marshal it back to JS.
    /// </summary>
    /// <remarks>
    /// The returned expression takes a single <see cref="JSCallbackArgs"/> parameter and
    /// returns a <see cref="JSValue"/>. For instance methods, the `this` argument for the JS
    /// callback args must be a JS object that wraps a .NET object matching the property's
    /// declaring type. The lambda expression may be converted to a <see cref="JSCallback"/>
    /// delegate with <see cref="LambdaExpression.Compile()"/>.
    /// </remarks>
    public Expression<JSCallback> BuildFromJSPropertyGetExpression(PropertyInfo property)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));

        MethodInfo? getMethod = property.GetMethod ??
            throw new ArgumentException("Property does not have a get method.");
        try
        {
            return getMethod.IsStatic
                ? BuildFromJSStaticPropertyGetExpression(property)
                : BuildFromJSInstancePropertyGetExpression(property);
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build JS callback adapter for .NET property getter.", property, ex);
        }
    }

    /// <summary>
    /// Builds a lambda expression for a JS callback adapter to a .NET property setter. When
    /// invoked, the delegate will marshal the value from JS and set the property.
    /// </summary>
    /// <remarks>
    /// The returned expression takes a single <see cref="JSCallbackArgs"/> parameter and
    /// returns a <see cref="JSValue"/>. For instance methods, the `this` argument for the JS
    /// callback args must be a JS object that wraps a .NET object matching the property's
    /// declaring type. The lambda expression may be converted to a <see cref="JSCallback"/>
    /// delegate with <see cref="LambdaExpression.Compile()"/>.
    /// </remarks>
    public Expression<JSCallback> BuildFromJSPropertySetExpression(PropertyInfo property)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));

        MethodInfo? setMethod = property.SetMethod ??
            throw new ArgumentException("Property does not have a set method.");
        try
        {
            return setMethod.IsStatic
                ? BuildFromJSStaticPropertySetExpression(property)
                : BuildFromJSInstancePropertySetExpression(property);
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build JS callback adapter for .NET property setter.", property, ex);
        }
    }

    /// <summary>
    /// Builds a lambda expression for a .NET adapter to a JS method. When invoked, the
    /// delegate will marshal the arguments to JS, invoke the method on the JS class or
    /// instance, then marshal the return value (or exception) back to .NET.
    /// </summary>
    /// <remarks>
    /// The expression has an extra initial argument of type <see cref="JSValue"/> that is
    /// the JS object on which the method will be invoked. For static methods that is the
    /// JS class object; for instance methods it is the JS instance. The lambda expression
    /// may be converted to a delegate with <see cref="LambdaExpression.Compile()"/>.
    /// </remarks>
    public LambdaExpression BuildToJSMethodExpression(MethodInfo method)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));
        else if (method.IsGenericMethodDefinition) throw new ArgumentException(
            "Construct a generic method definition from the method first.", nameof(method));

        try
        {
            string name = method.Name;
            if (method.DeclaringType!.IsInterface)
            {
                name = method.DeclaringType.Namespace + '.' +
                    method.DeclaringType.Name + '.' + name;
            }

            ParameterInfo[] allMethodParameters = method.GetParameters();
            ParameterInfo[] methodParameters = allMethodParameters
                .Where((p) => !(p.IsOut && !p.IsIn)).ToArray(); // Exclude out-only parameters

            ParameterExpression[] parameters =
                new ParameterExpression[allMethodParameters.Length + 1];
            ParameterExpression thisParameter = Expression.Parameter(typeof(JSValue), "__this");
            parameters[0] = thisParameter;
            for (int i = 0; i < allMethodParameters.Length; i++)
            {
                parameters[i + 1] = Parameter(allMethodParameters[i]);
            }

            /*
             * ReturnType MethodName(JSValue __this, Arg0Type arg0, ...)
             * {
             *     JSValue __result = __this.CallMethod("methodName", (JSValue)arg0, ...);
             *     return (ReturnType)__result;
             * }
             */

            // If the method is an explicit interface implementation, parse off the simple name.
            // Then convert to JSValue for use as a JS property name.
            int dotIndex = method.Name.LastIndexOf('.');
            Expression methodName = Expression.Convert(
                Expression.Constant(ToCamelCase(
                    dotIndex >= 0 ? method.Name.Substring(dotIndex + 1) : method.Name)),
                typeof(JSValue),
                typeof(JSValue).GetImplicitConversion(typeof(string), typeof(JSValue)));

            Expression ParameterToJSValue(int index) => InlineOrInvoke(
                GetToJSValueExpression(methodParameters[index].ParameterType),
                parameters[index + 1],
                nameof(BuildToJSMethodExpression));

            // Switch on parameter count to avoid allocating an array if < 4 parameters.
            // (Expression trees don't support stackalloc.)
            Expression callExpression;
            if (methodParameters.Length == 0)
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.CallMethod),
                        new[] { typeof(JSValue), typeof(JSValue) }),
                    thisParameter,
                    methodName);
            }
            else if (methodParameters.Length == 1)
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.CallMethod),
                         new[] { typeof(JSValue), typeof(JSValue), typeof(JSValue) }),
                    thisParameter,
                    methodName,
                    ParameterToJSValue(0));
            }
            else if (methodParameters.Length == 2)
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.CallMethod),
                         new[] { typeof(JSValue), typeof(JSValue), typeof(JSValue),
                             typeof(JSValue) }),
                    thisParameter,
                    methodName,
                    ParameterToJSValue(0),
                    ParameterToJSValue(1));
            }
            else if (methodParameters.Length == 3)
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.CallMethod),
                         new[] { typeof(JSValue), typeof(JSValue), typeof(JSValue),
                             typeof(JSValue), typeof(JSValue) }),
                    thisParameter,
                    methodName,
                    ParameterToJSValue(0),
                    ParameterToJSValue(1),
                    ParameterToJSValue(2));
            }
            else
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.CallMethod),
                         new[] { typeof(JSValue), typeof(JSValue), typeof(JSValue[]) }),
                    new Expression[]
                    {
                        thisParameter,
                        methodName,
                        Expression.NewArrayInit(
                            typeof(JSValue),
                            methodParameters.Select((_, i) => ParameterToJSValue(i))),
                    });
            }


            ParameterExpression resultVariable = Expression.Parameter(typeof(JSValue), "__result");
            List<Expression> statements = new();

            if (allMethodParameters.Any((p) => p.IsOut))
            {
                statements.Add(Expression.Assign(resultVariable, callExpression));

                foreach (ParameterInfo outParameter in allMethodParameters.Where((p) => p.IsOut))
                {
                    // Convert and assign values to out parameters.
                    string? outParameterName = Parameter(outParameter).Name;
                    statements.Add(Expression.Assign(
                        parameters.Single((p) => p.Name == outParameterName),
                        InlineOrInvoke(
                            GetFromJSValueExpression(outParameter.ParameterType),
                            Expression.Property(
                                resultVariable,
                                s_valueItem,
                                Expression.Constant(outParameter.Name)),
                            nameof(BuildToJSMethodExpression))));
                }

                string resultName = ResultPropertyName;
                if (allMethodParameters.Any(
                    (p) => p.Name == resultName && (p.IsOut || p.ParameterType.IsByRef)))
                {
                    resultName = '_' + resultName;
                }

                if (method.ReturnType != typeof(void))
                {
                    // Get the return value from the results object.
                    statements.Add(Expression.Assign(resultVariable, Expression.Property(
                        resultVariable, s_valueItem, Expression.Constant(resultName))));
                    statements.Add(InlineOrInvoke(
                        GetFromJSValueExpression(method.ReturnType),
                        resultVariable,
                        nameof(BuildToJSMethodExpression)));
                }
            }
            else if (method.ReturnType == typeof(void))
            {
                statements.Add(callExpression);
            }
            else
            {
                statements.Add(Expression.Assign(resultVariable, callExpression));
                statements.Add(InlineOrInvoke(
                    GetFromJSValueExpression(method.ReturnType),
                    resultVariable,
                    nameof(BuildToJSMethodExpression)));
            }

            return Expression.Lambda(
                _delegates.Value.GetToJSDelegateType(method.ReturnType, parameters),
                Expression.Block(method.ReturnType, new[] { resultVariable }, statements),
                name,
                parameters);
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build .NET adapter for JS method.", method, ex);
        }
    }

    /// <summary>
    /// Builds a lambda expression for a .NET adapter to a JS function that is callable
    /// as a .NET delegate. When invoked, the adapter will marshal the arguments to JS, invoke
    /// the JS function, then marshal the return value (or exception) back to .NET.
    /// </summary>
    /// <remarks>
    /// The expression has an extra initial argument of type <see cref="JSValue"/> that is
    /// the JS function that will be invoked. The lambda expression may be converted to a
    /// delegate with <see cref="LambdaExpression.Compile()"/>.
    /// </remarks>
    public LambdaExpression BuildToJSFunctionExpression(MethodInfo method)
    {
        if (method is null) throw new ArgumentNullException(nameof(method));
        else if (method.IsGenericMethodDefinition) throw new ArgumentException(
            "Construct a generic method definition from the method first.", nameof(method));

        try
        {
            ParameterInfo[] methodParameters = method.GetParameters();
            ParameterExpression[] parameters = new ParameterExpression[methodParameters.Length + 1];
            ParameterExpression thisParameter = Expression.Parameter(typeof(JSValue), "__this");
            parameters[0] = thisParameter;
            for (int i = 0; i < methodParameters.Length; i++)
            {
                parameters[i + 1] = Parameter(methodParameters[i]);
            }

            ParameterExpression resultVariable = Expression.Parameter(typeof(JSValue), "__result");

            /*
             * ReturnType DelegateName(JSValue __this, Arg0Type arg0, ...)
             * {
             *     JSValue __result = __this.Call(thisArg: default(JSValue), (JSValue)arg0, ...);
             *     return (ReturnType)__result;
             * }
             */

            Expression ParameterToJSValue(int index) => InlineOrInvoke(
                GetToJSValueExpression(methodParameters[index].ParameterType),
                parameters[index + 1],
                nameof(BuildToJSMethodExpression));

            // Switch on parameter count to avoid allocating an array if < 4 parameters.
            // (Expression trees don't support stackalloc.)
            Expression callExpression;
            if (methodParameters.Length == 0)
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.Call),
                        new[] { typeof(JSValue), typeof(JSValue) }),
                    thisParameter,
                    Expression.Default(typeof(JSValue)));
            }
            else if (methodParameters.Length == 1)
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.Call),
                         new[] { typeof(JSValue), typeof(JSValue), typeof(JSValue) }),
                    thisParameter,
                    Expression.Default(typeof(JSValue)),
                    ParameterToJSValue(0));
            }
            else if (methodParameters.Length == 2)
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.Call),
                         new[] { typeof(JSValue), typeof(JSValue), typeof(JSValue),
                             typeof(JSValue) }),
                    thisParameter,
                    Expression.Default(typeof(JSValue)),
                    ParameterToJSValue(0),
                    ParameterToJSValue(1));
            }
            else if (methodParameters.Length == 3)
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.Call),
                         new[] { typeof(JSValue), typeof(JSValue), typeof(JSValue),
                             typeof(JSValue), typeof(JSValue) }),
                    thisParameter,
                    Expression.Default(typeof(JSValue)),
                    ParameterToJSValue(0),
                    ParameterToJSValue(1),
                    ParameterToJSValue(2));
            }
            else
            {
                callExpression = Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.Call),
                         new[] { typeof(JSValue), typeof(JSValue), typeof(JSValue[]) }),
                    new Expression[]
                    {
                        thisParameter,
                        Expression.Default(typeof(JSValue)),
                        Expression.NewArrayInit(
                            typeof(JSValue),
                            methodParameters.Select((_, i) => ParameterToJSValue(i))),
                    });
            }

            Expression[] statements;
            if (method.ReturnType == typeof(void))
            {
                statements = new[] { callExpression };
            }
            else
            {
                statements = new[]
                {
                    Expression.Assign(resultVariable, callExpression),
                    InlineOrInvoke(
                        GetFromJSValueExpression(method.ReturnType),
                        resultVariable,
                        nameof(BuildToJSMethodExpression)),
                };
            }

            return Expression.Lambda(
                _delegates.Value.GetToJSDelegateType(method.ReturnType, parameters),
                Expression.Block(method.ReturnType, new[] { resultVariable }, statements),
                $"to_{FullMethodName(method)}",
                parameters);
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build .NET adapter to JS delegate.", method.DeclaringType!, ex);
        }
    }

    /// <summary>
    /// Builds a lambda expression for a .NET adapter from a JS function that calls a
    /// .NET delegate. When invoked, the adapter will marshal the arguments from JS, invoke
    /// the .NET delegate, then marshal the return value (or exception) back to JS.
    /// </summary>
    /// <remarks>
    /// The expression has an extra initial argument of the .NET delegate type that is
    /// the delegate that will be invoked. The lambda expression may be converted to a
    /// delegate with <see cref="LambdaExpression.Compile()"/>.
    /// </remarks>
    public LambdaExpression BuildFromJSFunctionExpression(MethodInfo method)
    {
        try
        {
            ParameterExpression thisParameter = Expression.Parameter(
                method.DeclaringType!, "__this");

            List<ParameterExpression> argVariables = new();
            IEnumerable<ParameterExpression> variables;
            ParameterInfo[] parameters = method.GetParameters();
            List<Expression> statements = new(parameters.Length + 2);

            /*
             * JSValue DelegateName(DelegateType __this, JSCallbackArgs __args)
             * {
             *     var param0Name = (Param0Type)__args[0];
             *     ...
             *     var __result = __this.Invoke(param0, ...);
             *     return (JSValue)__result;
             * }
             */

            for (int i = 0; i < parameters.Length; i++)
            {
                argVariables.Add(Expression.Variable(
                    parameters[i].ParameterType, parameters[i].Name));
                statements.Add(Expression.Assign(argVariables[i],
                    BuildArgumentExpression(i, parameters[i])));
            }

            if (method.ReturnType == typeof(void))
            {
                variables = argVariables;
                statements.Add(Expression.Call(thisParameter, method, argVariables));
                statements.Add(Expression.Default(typeof(JSValue)));
            }
            else
            {
                ParameterExpression resultVariable = Expression.Variable(
                    method.ReturnType, "__result");
                variables = argVariables.Append(resultVariable);
                statements.Add(Expression.Assign(resultVariable,
                    Expression.Call(thisParameter, method, argVariables)));
                statements.Add(BuildResultExpression(resultVariable, method.ReturnType));
            }

            return Expression.Lambda(
                JSMarshallerDelegates.GetFromJSDelegateType(method.DeclaringType!),
                body: Expression.Block(typeof(JSValue), variables, statements),
                $"from_{FullMethodName(method)}",
                parameters: new[] { thisParameter, s_argsParameter });
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build .NET adapter from JS delegate.", method.DeclaringType!, ex);
        }
    }

    private LambdaExpression BuildToJSFunctionExpression(Type delegateType)
    {
        MethodInfo invokeMethod = delegateType.GetMethod(nameof(Action.Invoke))!;
        LambdaExpression methodExpression = BuildToJSFunctionExpression(invokeMethod);

        // When invoking a JS function via a .NET delegate, use the synchronization context
        // to ensure the call is made on the JS thread. Ordinary method calls from .NET to
        // JS do not do this, but callback delegates are more likely to be invoked asynchronously
        // from a background thread.

        /*
         * var __valueRef = new JSReference(__value);
         * var __syncContext = JSSynchronizationContext.Current;
         * return (...args) => __syncContext.Run(() =>
         *     __valueRef.GetValue().Value.Call(...args));
         */
        ParameterExpression valueParameter = Expression.Parameter(typeof(JSValue), "__value");
        ParameterExpression valueRefVariable = Expression.Variable(
            typeof(JSReference), "__valueRef");
        ParameterExpression syncContextVariable = Expression.Variable(
            typeof(JSSynchronizationContext), "__syncContext");

        Expression assignRefExpression = Expression.Assign(
            valueRefVariable,
            Expression.New(
                typeof(JSReference).GetInstanceConstructor(
                    new[] { typeof(JSValue), typeof(bool) })!,
                valueParameter,
                Expression.Constant(false)));
        Expression assignSyncContextExpression = Expression.Assign(
            syncContextVariable,
            Expression.Property(null, typeof(JSSynchronizationContext).GetStaticProperty(
                nameof(JSSynchronizationContext.Current))));
        Expression getValueExpression = Expression.Property(
            Expression.Call(
                valueRefVariable,
                typeof(JSReference).GetInstanceMethod(nameof(JSReference.GetValue))),
            "Value");

        ParameterExpression[] parameters = invokeMethod.GetParameters()
            .Select(Parameter).ToArray();

        // Select either the Action or Func<> overload of JSSynchronizationContext.Run,
        // depending on whether the delegate returns a value.
        Type runDelegateType;
        MethodInfo runMethod;
        if (invokeMethod.ReturnType == typeof(void))
        {
            runDelegateType = typeof(Action);
            runMethod = typeof(JSSynchronizationContext).GetInstanceMethod(
                nameof(JSSynchronizationContext.Run), new[] { typeof(Action) });
        }
        else
        {
            runDelegateType = typeof(Func<>).MakeGenericType(invokeMethod.ReturnType);
            runMethod = typeof(JSSynchronizationContext).GetInstanceMethod(
                nameof(JSSynchronizationContext.Run),
                new[] { typeof(Func<>) },
                genericArg: invokeMethod.ReturnType);
        }

        LambdaExpression innerLambdaExpression = Expression.Lambda(
            runDelegateType,
            Expression.Invoke(
                methodExpression,
                parameters.Prepend(getValueExpression)));
        Expression lambdaExpression = Expression.Lambda(
            delegateType,
            Expression.Call(syncContextVariable, runMethod, innerLambdaExpression),
            parameters);

        parameters = new[] { valueParameter };
        return Expression.Lambda(
            _delegates.Value.GetToJSDelegateType(delegateType, parameters),
            Expression.Block(
                delegateType,
                new[] { valueRefVariable, syncContextVariable },
                new[]
                {
                    assignRefExpression,
                    assignSyncContextExpression,
                    lambdaExpression,
                }),
            $"to_{FullTypeName(delegateType)}",
            parameters);
    }

    private LambdaExpression BuildFromJSFunctionExpression(Type delegateType)
    {
        MethodInfo invokeMethod = delegateType.GetMethod(nameof(Action.Invoke))!;
        LambdaExpression methodExpression = BuildFromJSFunctionExpression(invokeMethod);

        /*
         * JSValue.CreateFunction(name, (__args) => __value.Invoke(...args));
         */

        ParameterExpression valueParameter = Expression.Parameter(delegateType, "__value");
        MethodInfo createFunctionMethod = typeof(JSValue).GetStaticMethod(
            nameof(JSValue.CreateFunction),
            new[] { typeof(string), typeof(JSCallback), typeof(object) });

        Expression lambdaExpression = Expression.Lambda(
            typeof(JSCallback),
            Expression.Invoke(methodExpression, valueParameter, s_argsParameter),
            s_argsArray);

        ParameterExpression[] parameters = new[] { valueParameter };
        return Expression.Lambda(
            _delegates.Value.GetToJSDelegateType(typeof(JSValue), parameters),
            Expression.Block(
                typeof(JSValue),
                new Expression[]
                {
                    Expression.Call(
                        createFunctionMethod,
                        Expression.Constant(methodExpression.Name),
                        lambdaExpression,
                        Expression.Default(typeof(object))),
                }),
            $"from_{FullTypeName(delegateType)}",
            parameters);
    }

    /// <summary>
    /// Builds a lambda expression for a .NET adapter to a JS property getter. When invoked,
    /// the delegate will get the property value from the JS class or instance, marshal the
    /// value from JS, and return it.
    /// </summary>
    /// <remarks>
    /// The expression has an extra initial argument of type <see cref="JSValue"/> that is
    /// the JS object on which the property will be accessed. For static properties that is the
    /// JS class object; for instance properties it is the JS instance. The lambda expression
    /// may be converted to a delegate with <see cref="LambdaExpression.Compile()"/>.
    /// </remarks>
    public LambdaExpression BuildToJSPropertyGetExpression(PropertyInfo property)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));

        try
        {
            string name = "get_" + property.Name;
            if (property.DeclaringType!.IsInterface)
            {
                name = property.DeclaringType.Namespace + '.' +
                    property.DeclaringType.Name + '.' + name;
            }

            ParameterExpression thisParameter = Expression.Parameter(typeof(JSValue), "__this");
            ParameterExpression resultVariable = Expression.Variable(typeof(JSValue), "__result");

            /*
             * PropertyType get_PropertyName(JSValue __this)
             * {
             *     JSValue __result = __this.GetProperty("propertyName");
             *     return (PropertyType)__result;
             * }
             */

            Expression propertyName = Expression.Convert(
                Expression.Constant(ToCamelCase(property.Name)),
                typeof(JSValue),
                typeof(JSValue).GetImplicitConversion(typeof(string), typeof(JSValue)));

            Expression getStatement = Expression.Assign(
                resultVariable,
                Expression.Call(
                    typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.GetProperty),
                        new[] { typeof(JSValue), typeof(JSValue) }),
                    thisParameter,
                    propertyName));
            Expression returnStatement = InlineOrInvoke(
                GetFromJSValueExpression(property.PropertyType),
                resultVariable,
                nameof(BuildToJSPropertyGetExpression));

            return Expression.Lambda(
                _delegates.Value.GetToJSDelegateType(property.PropertyType, thisParameter),
                Expression.Block(
                    property.PropertyType,
                    new[] { resultVariable },
                    new[] { getStatement, returnStatement }),
                name,
                new[] { thisParameter });
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build .NET adapter for JS property getter.", property, ex);
        }
    }


    /// <summary>
    /// Builds a lambda expression for a .NET adapter to a JS property setter. When invoked,
    /// the delegate will marshal the value to JS and set the property on the JS class or
    /// instance.
    /// </summary>
    /// <remarks>
    /// The expression has an extra initial argument of type <see cref="JSValue"/> that is
    /// the JS object on which the property will be accessed. For static properties that is the
    /// JS class object; for instance properties it is the JS instance. The lambda expression
    /// may be converted to a delegate with <see cref="LambdaExpression.Compile()"/>.
    /// </remarks>
    public LambdaExpression BuildToJSPropertySetExpression(PropertyInfo property)
    {
        if (property is null) throw new ArgumentNullException(nameof(property));

        try
        {
            string name = "set_" + property.Name;
            if (property.DeclaringType!.IsInterface)
            {
                name = property.DeclaringType.Namespace + '.' +
                    property.DeclaringType.Name + '.' + name;
            }

            ParameterExpression thisParameter =
                Expression.Parameter(typeof(JSValue), "__this");
            ParameterExpression valueParameter =
                Expression.Parameter(property.PropertyType, "__value");
            ParameterExpression jsValueVariable =
                Expression.Variable(typeof(JSValue), "__jsValue");

            /*
             * void set_PropertyName(JSValue __this, PropertyType __value)
             * {
             *     JSValue __jsValue = (JSValue)__value;
             *     __this.SetProperty("propertyName", __jsValue);
             * }
             */

            Expression propertyName = Expression.Convert(
                Expression.Constant(ToCamelCase(property.Name)),
                typeof(JSValue),
                typeof(JSValue).GetImplicitConversion(typeof(string), typeof(JSValue)));

            Expression convertStatement = Expression.Assign(
                jsValueVariable,
                InlineOrInvoke(
                    GetToJSValueExpression(property.PropertyType),
                    valueParameter,
                    nameof(BuildToJSPropertySetExpression)));
            Expression setStatement = Expression.Call(
                typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.SetProperty),
                    new[] { typeof(JSValue), typeof(JSValue), typeof(JSValue) }),
                thisParameter,
                propertyName,
                jsValueVariable);

            ParameterExpression[] parameters = { thisParameter, valueParameter };
            return Expression.Lambda(
                _delegates.Value.GetToJSDelegateType(typeof(void), parameters),
                Expression.Block(
                    typeof(void),
                    new[] { jsValueVariable },
                    new[] { convertStatement, setStatement }),
                name,
                parameters);
        }
        catch (Exception ex)
        {
            throw new JSMarshallerException(
                "Failed to build .NET adapter for JS property setter.", property, ex);
        }
    }

    /// <summary>
    /// Builds a lambda expression that generates a callback descriptor that resolves and invokes
    /// the best-matching overload from a set of overloaded constructors.
    /// </summary>
    public Expression<Func<JSCallbackDescriptor>> BuildConstructorOverloadDescriptorExpression(
        ConstructorInfo[] constructors)
    {
        if (constructors.Length == 0)
        {
            throw new ArgumentException(
                "Constructors array must have at least one item.", nameof(constructors));
        }

        /*
         * var overloads = new JSCallbackOverload[constructors.Length];
         * overloads[0] = new JSCallbackOverload(
         *   new Type[] { ... }, // Constructor overload parameter types
         *   (args) => { ... }); // Constructor overload lambda
         * ...                   // Additional overloads
         * return JSCallbackOverload.CreateDescriptor(overloads);
         */

        ParameterExpression overloadsVariable =
            Expression.Variable(typeof(JSCallbackOverload[]), "overloads");
        var statements = new Expression[constructors.Length + 2];
        statements[0] = Expression.Assign(
            overloadsVariable, Expression.NewArrayBounds(
                typeof(JSCallbackOverload), Expression.Constant(constructors.Length)));

        ConstructorInfo overloadConstructor = typeof(JSCallbackOverload).GetInstanceConstructor(
            new[] { typeof(Type[]), typeof(JSCallback) });

        for (int i = 0; i < constructors.Length; i++)
        {
            // TODO: Default parameter values

            Type[] parameterTypes = constructors[i].GetParameters()
                .Select(p => p.ParameterType).ToArray();
            statements[i + 1] = Expression.Assign(
                Expression.ArrayAccess(overloadsVariable, Expression.Constant(i)),
                Expression.New(
                    overloadConstructor,
                    Expression.NewArrayInit(typeof(Type),
                        parameterTypes.Select(t => Expression.Constant(t, typeof(Type)))),
                    BuildFromJSConstructorExpression(constructors[i])));
        }

        MethodInfo createDescriptorMethod = typeof(JSCallbackOverload).GetStaticMethod(
            nameof(JSCallbackOverload.CreateDescriptor));
        statements[statements.Length - 1] = Expression.Call(
            createDescriptorMethod, overloadsVariable);

        return (Expression<Func<JSCallbackDescriptor>>)Expression.Lambda(
            typeof(Func<JSCallbackDescriptor>),
            Expression.Block(
                typeof(JSCallbackDescriptor),
                new[] { overloadsVariable },
                statements),
            name: "new_" + FullTypeName(constructors[0].DeclaringType!),
            Array.Empty<ParameterExpression>());
    }

    /// <summary>
    /// Builds a callback descriptor that resolves and invokes the best-matching overload from
    /// a set of overloaded constructors.
    /// </summary>
    public JSCallbackDescriptor BuildConstructorOverloadDescriptor(ConstructorInfo[] constructors)
    {
        JSCallbackOverload[] overloads = new JSCallbackOverload[constructors.Length];
        for (int i = 0; i < constructors.Length; i++)
        {
            ParameterInfo[] parameters = constructors[i].GetParameters();
            Type[] parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

            object?[]? defaultValues = null;
            if (parameters.Any((p) => p.HasDefaultValue))
            {
                defaultValues = parameters.Where((p) => p.HasDefaultValue)
                    .Select((p) => p.DefaultValue)
                    .ToArray();
            }
            JSCallback constructorDelegate =
                BuildFromJSConstructorExpression(constructors[i]).Compile();
            overloads[i] = new JSCallbackOverload(parameterTypes, constructorDelegate);
        }
        return JSCallbackOverload.CreateDescriptor(overloads);
    }

    /// <summary>
    /// Builds a lambda expression that generates a callback descriptor that resolves and invokes
    /// the best-matching overload from a set of overloaded methods.
    /// </summary>
    public Expression<Func<JSCallbackDescriptor>> BuildMethodOverloadDescriptorExpression(
        MethodInfo[] methods)
    {
        if (methods.Length == 0)
        {
            throw new ArgumentException(
                "Methods array must have at least one item.", nameof(methods));
        }

        /*
         * var overloads = new JSCallbackOverload[methods.Length];
         * overloads[0] = new JSCallbackOverload(
         *   new Type[] { ... }, // Method overload parameter types
         *   (args) => { ... }); // Method overload lambda
         * ...                   // Additional overloads
         * return JSCallbackOverload.CreateDescriptor(overloads);
         */

        ParameterExpression overloadsVariable =
            Expression.Variable(typeof(JSCallbackOverload[]), "overloads");
        var statements = new Expression[methods.Length + 2];
        statements[0] = Expression.Assign(
            overloadsVariable, Expression.NewArrayBounds(
                typeof(JSCallbackOverload), Expression.Constant(methods.Length)));

        ConstructorInfo overloadConstructor = typeof(JSCallbackOverload).GetInstanceConstructor(
            new[] { typeof(Type[]), typeof(JSCallback) });

        for (int i = 0; i < methods.Length; i++)
        {
            // TODO: Default parameter values

            Type[] parameterTypes = methods[i].GetParameters()
                .Select(p => p.ParameterType).ToArray();
            statements[i + 1] = Expression.Assign(
                Expression.ArrayAccess(overloadsVariable, Expression.Constant(i)),
                Expression.New(
                    overloadConstructor,
                    Expression.NewArrayInit(typeof(Type),
                        parameterTypes.Select(t => Expression.Constant(t, typeof(Type)))),
                    BuildFromJSMethodExpression(methods[i])));
        }

        MethodInfo createDescriptorMethod = typeof(JSCallbackOverload).GetStaticMethod(
            nameof(JSCallbackOverload.CreateDescriptor));
        statements[statements.Length - 1] = Expression.Call(
            createDescriptorMethod, overloadsVariable);

        return (Expression<Func<JSCallbackDescriptor>>)Expression.Lambda(
            typeof(Func<JSCallbackDescriptor>),
            Expression.Block(
                typeof(JSCallbackDescriptor),
                new[] { overloadsVariable },
                statements),
            name: FullMethodName(methods[0]),
            Array.Empty<ParameterExpression>());
    }

    /// <summary>
    /// Builds a callback descriptor that resolves and invokes the best-matching overload from
    /// a set of overloaded methods.
    /// </summary>
    public JSCallbackDescriptor BuildMethodOverloadDescriptor(MethodInfo[] methods)
    {
        JSCallbackOverload[] overloads = new JSCallbackOverload[methods.Length];
        for (int i = 0; i < methods.Length; i++)
        {
            ParameterInfo[] parameters = methods[i].GetParameters();
            Type[] parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

            object?[]? defaultValues = null;
            if (parameters.Any((p) => p.HasDefaultValue))
            {
                defaultValues = parameters.Where((p) => p.HasDefaultValue)
                    .Select((p) => p.DefaultValue)
                    .ToArray();
            }

            JSCallback methodDelegate =
                BuildFromJSMethodExpression(methods[i]).Compile();
            overloads[i] = new JSCallbackOverload(parameterTypes, defaultValues, methodDelegate);
        }
        return JSCallbackOverload.CreateDescriptor(overloads);
    }

    private Expression<JSCallback> BuildFromJSStaticMethodExpression(MethodInfo method)
    {
        /*
         * JSValue MethodClass_MethodName(JSCallbackArgs __args)
         * {
         *     var param0Name = (Param0Type)__args[0];
         *     ...
         *     var __result = MethodClass.MethodName(param0, ...);
         *     return (JSValue)__result;
         * }
         */

        List<ParameterExpression> argVariables = new();
        List<ParameterExpression> variables = new();
        ParameterInfo[] parameters = method.GetParameters();
        List<Expression> statements = new(parameters.Length + 2);

        for (int i = 0; i < parameters.Length; i++)
        {
            argVariables.Add(Variable(parameters[i]));
            statements.Add(Expression.Assign(argVariables[i],
                BuildArgumentExpression(i, parameters[i])));
        }

        ParameterExpression? resultVariable = null;
        if (method.ReturnType == typeof(void))
        {
            statements.Add(Expression.Call(method, argVariables));
        }
        else
        {
            resultVariable = Expression.Variable(method.ReturnType, "__result");
            variables.Add(resultVariable);
            statements.Add(Expression.Assign(resultVariable,
                Expression.Call(method, argVariables)));
        }

        if (parameters.Any((p) => p.IsOut))
        {
            ParameterExpression resultsVariable = Expression.Variable(typeof(JSValue), "__results");
            variables.Add(resultsVariable);
            statements.AddRange(BuildOutParamsObject(
                method, parameters, argVariables, resultVariable, resultsVariable));
            statements.Add(resultsVariable);
        }
        else if (method.ReturnType != typeof(void))
        {
            statements.Add(BuildResultExpression(resultVariable!, method.ReturnType));
        }
        else
        {
            statements.Add(Expression.Default(typeof(JSValue)));
        }

        return (Expression<JSCallback>)Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables.Concat(argVariables), statements),
            name: FullMethodName(method),
            parameters: s_argsArray);
    }

    private Expression<JSCallback> BuildFromJSInstanceMethodExpression(MethodInfo method)
    {
        /*
         * JSValue MethodClass_MethodName(JSCallbackArgs __args)
         * {
         *     ObjectType __this = (ObjectType)__args.ThisArg;
         *     var param0Name = (Param0Type)__args[0];
         *     ...
         *     var __result = __this.MethodName(param0, ...);
         *     return (JSValue)__result;
         * }
         */

        List<ParameterExpression> argVariables = new();
        List<ParameterExpression> variables = new();
        ParameterExpression thisVariable = Expression.Variable(method.DeclaringType!, "__this");
        variables.Add(thisVariable);
        LabelTarget returnTarget = Expression.Label(typeof(JSValue));
        ParameterInfo[] parameters = method.GetParameters();
        List<Expression> statements = new(parameters.Length + 5);

        statements.AddRange(BuildThisArgumentExpressions(
            method.DeclaringType!, thisVariable, returnTarget));

        for (int i = 0; i < parameters.Length; i++)
        {
            argVariables.Add(Variable(parameters[i]));
            statements.Add(Expression.Assign(argVariables[i],
                BuildArgumentExpression(i, parameters[i])));
        }

        ParameterExpression? resultVariable = null;
        if (method.ReturnType == typeof(void))
        {
            statements.Add(Expression.Call(thisVariable, method, argVariables));
        }
        else
        {
            resultVariable = Expression.Variable(method.ReturnType, "__result");
            variables.Add(resultVariable);
            statements.Add(Expression.Assign(resultVariable,
                Expression.Call(thisVariable, method, argVariables)));
        }

        if (parameters.Any((p) => p.IsOut))
        {
            ParameterExpression resultsVariable = Expression.Variable(typeof(JSValue), "__results");
            variables.Add(resultsVariable);
            statements.AddRange(BuildOutParamsObject(
                method, parameters, argVariables, resultVariable, resultsVariable));
            statements.Add(Expression.Label(returnTarget, resultsVariable));
        }
        else if (method.ReturnType != typeof(void))
        {
            statements.Add(Expression.Label(returnTarget,
                BuildResultExpression(resultVariable!, method.ReturnType)));
        }
        else
        {
            statements.Add(Expression.Label(returnTarget, Expression.Default(typeof(JSValue))));
        }

        return (Expression<JSCallback>)Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables.Concat(argVariables), statements),
            name: FullMethodName(method),
            parameters: s_argsArray);
    }

    private IEnumerable<Expression> BuildOutParamsObject(
        MethodInfo method,
        ParameterInfo[] parameters,
        List<ParameterExpression> argVariables,
        ParameterExpression? resultVariable,
        ParameterExpression resultsVariable)
    {
        IEnumerable<ParameterInfo> outParameters = parameters
            .Where((p) => p.IsOut || p.ParameterType.IsByRef);

        if (method.ReturnType == typeof(bool) &&
            method.Name.StartsWith("Try", StringComparison.Ordinal) &&
            outParameters.Count() == 1)
        {
            // A method with Try* pattern simply returns the out-value or undefined
            // instead of an object with the bool and out-value properties.
            yield return Expression.Assign(
                resultsVariable,
                Expression.Condition(resultVariable!,
                    BuildResultExpression(
                        argVariables.Last(), outParameters.Single().ParameterType),
                    Expression.Default(typeof(JSValue))));
            yield break;
        }

        // Create an object to hold the ref/out parameters and return value.
        yield return Expression.Assign(resultsVariable, Expression.Call(
            null, typeof(JSValue).GetStaticMethod(nameof(JSValue.CreateObject))));

        foreach (ParameterInfo outParameter in outParameters)
        {
            // Convert and assign the ref/out parameters to properties on the results object.
            yield return Expression.Assign(
                Expression.Property(
                    resultsVariable, s_valueItem,
                    Expression.Constant(outParameter.Name)),
                BuildResultExpression(
                    argVariables.Single((a) => a.Name == outParameter.Name),
                    outParameter.ParameterType));
        }

        if (method.ReturnType != typeof(void))
        {
            string resultName = ResultPropertyName;
            if (parameters.Any((p) => p.Name == resultName && (p.IsOut || p.ParameterType.IsByRef)))
            {
                resultName = '_' + resultName;
            }

            // Convert and assign the return value to a property on the results object.
            yield return Expression.Assign(
                Expression.Property(
                    resultsVariable, s_valueItem, Expression.Constant(resultName)),
                BuildResultExpression(resultVariable!, method.ReturnType));
        }
    }

    private Expression<JSCallback> BuildFromJSStaticPropertyGetExpression(PropertyInfo property)
    {
        /*
         * JSValue get_PropertyClass_PropertyName(JSCallbackArgs __args)
         * {
         *     var __result = PropertyClass.PropertyName;
         *     return (JSValue)__result;
         * }
         */

        ParameterExpression resultVariable = Expression.Variable(property.PropertyType, "__result");
        List<ParameterExpression> variables = new(1)
        {
            resultVariable
        };
        var statements = new Expression[]
        {
            Expression.Assign(resultVariable, Expression.Property(null, property)),
            BuildResultExpression(resultVariable, property.PropertyType),
        };
        return (Expression<JSCallback>)Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables, statements),
            name: FullMethodName(property.GetMethod!),
            parameters: s_argsArray);
    }

    private Expression<JSCallback> BuildFromJSInstancePropertyGetExpression(PropertyInfo property)
    {
        /*
         * JSValue get_PropertyClass_PropertyName(JSCallbackArgs __args)
         * {
         *     ObjectType __this = (ObjectType)__args.ThisArg;
         *     var __result = __this.PropertyName;
         *     return (JSValue)__result;
         * }
         */

        ParameterExpression thisVariable = Expression.Variable(property.DeclaringType!, "__this");
        ParameterExpression resultVariable = Expression.Variable(property.PropertyType, "__result");
        List<ParameterExpression> variables = new(3)
        {
            thisVariable,
            resultVariable
        };
        LabelTarget returnTarget = Expression.Label(typeof(JSValue));
        List<Expression> statements = new(5);

        Expression propertyExpression;
        if (property.GetMethod!.GetParameters().Length == 0)
        {
            propertyExpression = Expression.Property(thisVariable, property);
        }
        else
        {
            ParameterInfo indexParameter = property.GetMethod.GetParameters()[0];
            propertyExpression = Expression.Property(
                thisVariable, property, BuildArgumentExpression(0, indexParameter));
        }

        statements.AddRange(BuildThisArgumentExpressions(
            property.DeclaringType!, thisVariable, returnTarget));
        statements.Add(Expression.Assign(resultVariable, propertyExpression));
        statements.Add(Expression.Label(returnTarget,
            BuildResultExpression(resultVariable, property.PropertyType)));

        return (Expression<JSCallback>)Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables, statements),
            name: FullMethodName(property.GetMethod!),
            parameters: s_argsArray);
    }

    private Expression<JSCallback> BuildFromJSStaticPropertySetExpression(PropertyInfo property)
    {
        /*
         * JSValue get_PropertyClass_PropertyName(JSCallbackArgs __args)
         * {
         *     var __value = (PropertyType)__args[0];
         *     __obj.PropertyName = __value;
         *     return JSValue.Undefined;
         * }
         */

        ParameterExpression valueVariable = Expression.Variable(property.PropertyType, "__value");
        var variables = new ParameterExpression[] { valueVariable };
        var statements = new Expression[]
        {
            Expression.Assign(valueVariable,
                    BuildArgumentExpression(0, property.PropertyType)),
            Expression.Call(property.SetMethod!, valueVariable),
            Expression.Default(typeof(JSValue)),
        };
        return (Expression<JSCallback>)Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables, statements),
            name: FullMethodName(property.SetMethod!),
            parameters: s_argsArray);
    }

    private Expression<JSCallback> BuildFromJSInstancePropertySetExpression(PropertyInfo property)
    {
        /*
         * JSValue get_PropertyClass_PropertyName(JSCallbackArgs __args)
         * {
         *     ObjectType __this = (ObjectType)__args.ThisArg;
         *     __this.PropertyName = __value;
         *     return JSValue.Undefined;
         * }
         */

        ParameterExpression thisVariable = Expression.Variable(property.DeclaringType!, "__this");
        ParameterExpression valueVariable = Expression.Variable(property.PropertyType, "__value");
        List<ParameterExpression> variables = new(3)
        {
            thisVariable,
            valueVariable
        };
        LabelTarget returnTarget = Expression.Label(typeof(JSValue));
        List<Expression> statements = new(6);

        MethodCallExpression setExpression;
        if (property.SetMethod!.GetParameters().Length == 1)
        {
            setExpression = Expression.Call(thisVariable, property.SetMethod, valueVariable);
        }
        else
        {
            ParameterInfo indexParameter = property.SetMethod.GetParameters()[0];
            setExpression = Expression.Call(
                thisVariable,
                property.SetMethod,
                BuildArgumentExpression(0, indexParameter),
                valueVariable);
        }

        statements.AddRange(BuildThisArgumentExpressions(
            property.DeclaringType!, thisVariable, returnTarget));
        statements.Add(Expression.Assign(valueVariable,
                    BuildArgumentExpression(0, property.PropertyType)));
        statements.Add(setExpression);
        statements.Add(Expression.Label(returnTarget, Expression.Default(typeof(JSValue))));

        return (Expression<JSCallback>)Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables, statements),
            name: FullMethodName(property.SetMethod!),
            parameters: s_argsArray);
    }

    private IEnumerable<Expression> BuildThisArgumentExpressions(
        Type type,
        ParameterExpression thisVariable,
        LabelTarget returnTarget)
    {
        // Note System.Linq.Expressions does not support more recent C# language
        // features, so the as/if/assignment expressions below cannot be combined.
        // https://github.com/dotnet/csharplang/issues/2545
        // https://github.com/dotnet/csharplang/discussions/158

        if (type.GetCustomAttributes<JSModuleAttribute>().Any())
        {
            // For a method on a module class, the .NET object is stored in the module context.
            // `ThisArg` is ignored for module-level methods.

            /*
             * ObjectType? __this = JSRuntimeContext.Current.Module as ObjectType;
             * if (__this == null) return JSValue.Undefined;
             */

            PropertyInfo moduleProperty = typeof(JSModuleContext).GetProperty(
                nameof(JSModuleContext.Module))!;
            yield return Expression.Assign(
                thisVariable,
                Expression.TypeAs(
                    Expression.Property(
                        Expression.Property(null, s_moduleContext),
                        moduleProperty),
                    type));
            yield return Expression.IfThen(
                Expression.Equal(thisVariable, Expression.Constant(null)),
                Expression.Return(returnTarget, Expression.Default(typeof(JSValue))));
        }
        else if (type.IsClass || type.IsInterface)
        {
            // For normal instance methods, the .NET object is wrapped by the JS object.

            /*
             * ObjectType? __this = __args.ThisArg.Unwrap() as ObjectType;
             * if (__this == null) return JSValue.Undefined;
             */

            PropertyInfo thisArgProperty = typeof(JSCallbackArgs).GetProperty(
                nameof(JSCallbackArgs.ThisArg))!;
            yield return Expression.Assign(
                thisVariable,
                Expression.TypeAs(
                    Expression.Call(
                        s_unwrap,
                        Expression.Property(s_argsParameter, thisArgProperty),
                        Expression.Constant(type.Name)),
                    type));
            yield return Expression.IfThen(
                Expression.Equal(thisVariable, Expression.Constant(null)),
                Expression.Return(returnTarget, Expression.Default(typeof(JSValue))));
        }
        else if (type.IsValueType)
        {
            // Structs are not wrapped; they are passed by value via a conversion method.

            /*
             * ObjectType __this = to_ObjectType(__args.ThisArg);
             */

            LambdaExpression convert = GetFromJSValueExpression(type);
            PropertyInfo thisArgProperty = typeof(JSCallbackArgs).GetProperty(
                nameof(JSCallbackArgs.ThisArg))!;
            yield return Expression.Assign(
                thisVariable,
                Expression.Invoke(
                    convert,
                    Expression.Property(s_argsParameter, thisArgProperty)));
        }
        else
        {
            throw new InvalidOperationException($"Invalid type for this arg: {type}");
        }
    }

    private Expression BuildArgumentExpression(int index, ParameterInfo parameter)
    {
        if (parameter.IsOut && !parameter.IsIn)
        {
            return Expression.Default(parameter.ParameterType.IsByRef ?
                parameter.ParameterType.GetElementType()! : parameter.ParameterType);
        }

        return BuildArgumentExpression(index, parameter.ParameterType);
    }

    private Expression BuildArgumentExpression(int index, Type parameterType)
    {
        if (parameterType.IsByRef)
        {
            parameterType = parameterType.GetElementType()!;
        }

        Expression argExpression = Expression.Property(
            s_argsParameter, s_callbackArg, Expression.Constant(index));

        Type? nullableType = null;
        if (!parameterType.IsValueType)
        {
            nullableType = parameterType;
        }
        else if (parameterType.IsGenericType &&
            parameterType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            nullableType = parameterType;
            parameterType = parameterType.GenericTypeArguments[0];
        }

        Expression convertExpression = InlineOrInvoke(
            GetFromJSValueExpression(parameterType),
            argExpression,
            nameof(BuildArgumentExpression));

        if (nullableType != null)
        {
            convertExpression = Expression.Condition(
                Expression.Call(s_isNullOrUndefined, argExpression),
                Expression.Constant(null, nullableType),
                Expression.Convert(convertExpression, nullableType));
        }

        return convertExpression;
    }

    private Expression BuildResultExpression(
        Expression resultVariable,
        Type resultType)
    {
        if (resultType == typeof(Task))
        {
            return Expression.Call(
                GetCastToJSValueMethod(typeof(JSPromise))!,
                Expression.Call(s_asVoidPromise, resultVariable));
        }

        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            Type asyncResultType = resultType;
            resultType = resultType.GenericTypeArguments[0];
            MethodInfo asPromiseMethod = typeof(TaskExtensions).GetStaticMethod(
                nameof(TaskExtensions.AsPromise),
                new[] { typeof(Task<>), typeof(JSValue.From<>) },
                resultType);
            return Expression.Call(
                GetCastToJSValueMethod(typeof(JSPromise))!,
                Expression.Call(
                    asPromiseMethod,
                    resultVariable,
                    GetToJSValueExpression(resultType)));
        }

        Type? nullableType = null;
        if (resultType.IsGenericType && resultType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            nullableType = resultType;
            resultType = resultType.GenericTypeArguments[0];
        }

        Expression resultExpression = nullableType == null ? resultVariable :
            Expression.Property(resultVariable, nullableType.GetProperty("Value")!);

        if (resultType != typeof(JSValue))
        {
            resultExpression = InlineOrInvoke(
                GetToJSValueExpression(resultType),
                resultExpression,
                nameof(BuildResultExpression));
        }

        if (nullableType != null)
        {
            resultExpression = Expression.Condition(
                Expression.Property(resultVariable, nullableType.GetProperty("HasValue")!),
                resultExpression,
                Expression.Default(typeof(JSValue)));
        }

        return resultExpression;
    }

    private LambdaExpression BuildConvertFromJSValueExpression(Type toType)
    {
        if (toType.IsByRef)
        {
            toType = toType.GetElementType()!;
        }

        Type delegateType = typeof(JSValue.To<>).MakeGenericType(toType);
        string delegateName = "to_" + FullTypeName(toType);

        ParameterExpression valueParameter = Expression.Parameter(typeof(JSValue), "value");

        Type? nullableType = null;
        if (toType.IsGenericType && toType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            nullableType = toType;
            toType = toType.GenericTypeArguments[0];
            delegateName = "to_" + FullTypeName(toType) + "_Nullable";
        }

        List<ParameterExpression> variables = new();
        IEnumerable<Expression> statements;

        MethodInfo? castMethod = GetCastFromJSValueMethod(toType);
        if (castMethod != null)
        {
            // Cast the JSValue to the target type using the explicit conversion method.
            statements = new[]
            {
                Expression.Convert(valueParameter, toType, castMethod),
            };
        }
        else if (toType == typeof(JSValue))
        {
            statements = new[] { valueParameter };
        }
        else if (toType == typeof(object) || !(toType.IsPublic || toType.IsNestedPublic))
        {
            // Marshal unknown or nonpublic type as external, so at least it can be round-tripped.
            // Also accept a wrapped value - this handles the case when an instance of a specific
            // public type is passed to JS and then passed back to .NET as `object` type.

            /*
             * (T)(value.TryUnwrap() ?? value.TryGetValueExternal());
             */
            MethodInfo getExternalMethod =
                typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.TryGetValueExternal));
            statements = new[]
            {
                Expression.Convert(
                    Expression.Coalesce(
                        Expression.Call(s_tryUnwrap, valueParameter),
                        Expression.Call(getExternalMethod, valueParameter)),
                    toType),
            };
        }
        else if (toType.IsEnum)
        {
            // Cast the JSValue to the enum underlying type, then to the enum type.
            Type underlyingType = toType.GetEnumUnderlyingType();
            castMethod = GetCastFromJSValueMethod(underlyingType);
            statements = new[]
            {
                Expression.Convert(
                    Expression.Convert(valueParameter, underlyingType, castMethod),
                    toType),
            };
        }
        else if (toType.IsArray)
        {
            Type elementType = toType.GetElementType()!;
            delegateName = "to_" + FullTypeName(elementType) + "_Array";
            statements = BuildFromJSToArrayExpressions(elementType, variables, valueParameter);
        }
        else if (toType.IsValueType)
        {
            Type? genericTypeDefinition = toType.IsGenericType ?
                toType.GetGenericTypeDefinition() : null;
            Type[]? genericArguments = toType.IsGenericType ?
                toType.GetGenericArguments() : null;

            if (genericTypeDefinition == typeof(Memory<>) ||
                genericTypeDefinition == typeof(ReadOnlyMemory<>))
            {
                Type elementType = toType.GenericTypeArguments[0];
                if (!IsTypedArrayType(elementType))
                {
                    throw new NotSupportedException(
                        $"Typed-array element type not supported: {elementType}");
                }

                Type typedArrayType = typeof(JSTypedArray<>).MakeGenericType(elementType);
                MethodInfo asTypedArray = typedArrayType.GetExplicitConversion(
                    typeof(JSValue), typedArrayType);
                PropertyInfo memory = typedArrayType.GetInstanceProperty("Memory");
                statements = new[]
                {
                    Expression.Property(Expression.Call(asTypedArray, valueParameter), memory),
                };
            }
            else if (genericTypeDefinition == typeof(KeyValuePair<,>))
            {
                /*
                 * new KeyValuePair<TKey, TValue>((TKey)value[0], (TValue)value[1])
                 */
                statements = new[]
                {
                    Expression.New(
                        toType.GetConstructor(genericArguments!)!,
                        InlineOrInvoke(
                            BuildConvertFromJSValueExpression(genericArguments![0]),
                            Expression.Call(
                                typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.GetElement)),
                                valueParameter, Expression.Constant(0)),
                            nameof(BuildConvertFromJSValueExpression)),
                        InlineOrInvoke(
                            BuildConvertFromJSValueExpression(genericArguments![1]),
                            Expression.Call(
                                typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.GetElement)),
                                valueParameter, Expression.Constant(1)),
                            nameof(BuildConvertFromJSValueExpression))),
                };
            }
            else if (toType == typeof(ValueTuple))
            {
                statements = new[] { Expression.Default(typeof(ValueTuple)) };
            }
            else if (genericTypeDefinition?.Name.StartsWith("ValueTuple`") == true)
            {
                /*
                 * new ValueTuple((T1)value[0], (T2)value[1], ...)
                 */
                Expression TupleItem(int index) => InlineOrInvoke(
                    BuildConvertFromJSValueExpression(genericArguments![index]),
                    Expression.Call(
                        typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.GetElement)),
                        valueParameter, Expression.Constant(index)),
                    nameof(BuildConvertFromJSValueExpression));
                statements = new[]
                {
                    Expression.New(
                        toType.GetConstructor(genericArguments!)!,
                        genericArguments!.Select((t, i) => TupleItem(i)).ToArray()),
                };
            }
            else if (toType == typeof(DateTime))
            {
                MethodInfo asJSDate = typeof(JSDate).GetExplicitConversion(
                    typeof(JSValue), typeof(JSDate));
                MethodInfo toDateTime = typeof(JSDate).GetInstanceMethod(nameof(JSDate.ToDateTime));
                statements = new[]
                {
                    Expression.Call(Expression.Call(asJSDate, valueParameter), toDateTime),
                };
            }
            else if (toType == typeof(CancellationToken))
            {
                MethodInfo toAbortSignal = typeof(JSAbortSignal).GetExplicitConversion(
                    typeof(JSValue), typeof(JSAbortSignal));
                MethodInfo toCancellationToken = typeof(JSAbortSignal).GetExplicitConversion(
                    typeof(JSAbortSignal), typeof(CancellationToken));

                statements = new[]
                {
                    Expression.Call(
                        toCancellationToken,
                        Expression.Call(toAbortSignal, valueParameter)),
                };
            }
            else
            {
                statements = BuildFromJSToStructExpressions(toType, variables, valueParameter);
            }
        }
        else if (toType.IsClass)
        {
            if (toType == typeof(Stream))
            {
                MethodInfo adapterConversion =
                    typeof(NodeStream).GetExplicitConversion(typeof(JSValue), typeof(NodeStream));
                statements = new[]
                {
                    Expression.Coalesce(
                        Expression.TypeAs(Expression.Call(s_tryUnwrap, valueParameter), toType),
                        Expression.Convert(valueParameter, typeof(NodeStream), adapterConversion)),
                };
            }
            else if (toType.BaseType == typeof(MulticastDelegate))
            {
                statements = new[]
                {
                    Expression.Invoke(
                        BuildToJSFunctionExpression(toType),
                        valueParameter),
                };
            }
            else if (toType.IsGenericType && toType.Name.StartsWith("Tuple`"))
            {
                /*
                 * new Tuple((T1)value[0], (T2)value[1], ...)
                 */
                Type[]? genericArguments = toType.GetGenericArguments();
                Expression TupleItem(int index) => InlineOrInvoke(
                    BuildConvertFromJSValueExpression(genericArguments![index]),
                    Expression.Call(
                        typeof(JSNativeApi).GetStaticMethod(nameof(JSNativeApi.GetElement)),
                        valueParameter, Expression.Constant(index)),
                    nameof(BuildConvertFromJSValueExpression));
                statements = new[]
                {
                    Expression.New(
                        toType.GetConstructor(genericArguments!)!,
                        genericArguments!.Select((t, i) => TupleItem(i)).ToArray()),
                };
            }
            else
            {
                statements = new[]
                {
                    Expression.Convert(
                        Expression.Call(s_unwrap, valueParameter, Expression.Constant(toType.Name)),
                        toType),
                };
            }
        }
        else if (toType.IsInterface && toType.Namespace == typeof(ICollection<>).Namespace)
        {
            statements = BuildFromJSToCollectionExpressions(toType, variables, valueParameter);
        }
        else if (toType.IsInterface)
        {
            // It could be either a wrapped .NET object passed back from JS or a JS object
            // that implements a .NET interface. For the latter case, dynamically build
            // a class that implements the interface by proxying member access to JS.
            Type adapterType = _interfaceMarshaller.Value.Implement(toType, this);
            ConstructorInfo adapterConstructor =
                adapterType.GetConstructor(new[] { typeof(JSValue) })!;
            statements = new[]
            {
                Expression.Coalesce(
                    Expression.TypeAs(Expression.Call(s_tryUnwrap, valueParameter), toType),
                    Expression.New(adapterConstructor, valueParameter)),
            };
        }
        else
        {
            throw new NotImplementedException(
                $"Conversion from {nameof(JSValue)} to {toType.FullName} is not implemented.");
        }

        if (nullableType != null)
        {
            MethodInfo isNullOrUndefinedMethod =
                typeof(JSNativeApi).GetMethod(nameof(JSNativeApi.IsNullOrUndefined))!;
            statements = new Expression[]
            {
                Expression.Condition(
                    Expression.Call(isNullOrUndefinedMethod, valueParameter),
                    Expression.Constant(null, nullableType),
                    Expression.Convert(
                        Expression.Block(toType, variables, statements),
                        nullableType)),
            };
            toType = nullableType;
        }

        return Expression.Lambda(
            delegateType,
            body: Expression.Block(nullableType ?? toType, variables, statements),
            name: delegateName,
            parameters: new[] { valueParameter });
    }

    private LambdaExpression BuildConvertToJSValueExpression(Type fromType)
    {
        if (fromType.IsByRef)
        {
            fromType = fromType.GetElementType()!;
        }

        Type delegateType = typeof(JSValue.From<>).MakeGenericType(fromType);
        string delegateName = "from_" + FullTypeName(fromType);

        ParameterExpression valueParameter = Expression.Parameter(fromType, "value");

        Type? nullableType = null;
        if (fromType.IsGenericType && fromType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            nullableType = fromType;
            fromType = fromType.GenericTypeArguments[0];
            delegateName = "from_" + FullTypeName(fromType) + "_Nullable";
        }

        Expression valueExpression = nullableType == null ? valueParameter :
            Expression.Property(valueParameter, nullableType.GetProperty("Value")!);

        List<ParameterExpression> variables = new();
        IEnumerable<Expression> statements;

        MethodInfo? castMethod = GetCastToJSValueMethod(fromType);
        if (castMethod != null)
        {
            // Cast the the source type to JSValue using the implicit conversion method.
            statements = new[]
            {
                Expression.Convert(valueExpression, typeof(JSValue), castMethod),
            };
        }
        else if (fromType == typeof(JSValue))
        {
            statements = new[] { valueParameter };
        }
        else if (fromType == typeof(object) || !fromType.IsPublic)
        {
            // Marshal unknown or nonpublic type as external, so at least it can be round-tripped.
            Expression objectExpression = fromType.IsValueType ?
                Expression.Convert(valueExpression, typeof(object)) : valueExpression;
            MethodInfo createExternalMethod =
                typeof(JSValue).GetStaticMethod(nameof(JSValue.CreateExternal));
            statements = new[]
            {
                Expression.Call(createExternalMethod, objectExpression),
            };
        }
        else if (fromType.IsEnum)
        {
            // Cast the enum type to the underlying type, then to JSValue.
            Type underlyingType = fromType.GetEnumUnderlyingType();
            castMethod = GetCastToJSValueMethod(underlyingType);
            statements = new[]
            {
                Expression.Convert(
                    Expression.Convert(valueExpression, underlyingType),
                    typeof(JSValue),
                    castMethod),
            };
        }
        else if (fromType.IsArray)
        {
            Type elementType = fromType.GetElementType()!;
            delegateName = "from_" + FullTypeName(elementType) + "_Array";
            statements = BuildToJSFromArrayExpressions(elementType, variables, valueExpression);
        }
        else if (fromType.IsValueType)
        {
            Type? genericTypeDefinition = fromType.IsGenericType ?
                fromType.GetGenericTypeDefinition() : null;
            Type[]? genericArguments = fromType.IsGenericType ?
                fromType.GetGenericArguments() : null;

            if (genericTypeDefinition == typeof(Memory<>) ||
                genericTypeDefinition == typeof(ReadOnlyMemory<>))
            {
                Type elementType = fromType.GenericTypeArguments[0];
                if (!IsTypedArrayType(elementType))
                {
                    throw new NotSupportedException(
                        $"Typed-array element type not supported: {elementType}");
                }

                Type typedArrayType = typeof(JSTypedArray<>).MakeGenericType(elementType);
                ConstructorInfo constructor = typedArrayType.GetConstructor(new[] { fromType })!;
                MethodInfo asJSValue = typedArrayType.GetImplicitConversion(
                    typedArrayType, typeof(JSValue));
                statements = new[]
                {
                    Expression.Call(asJSValue, Expression.New(constructor, valueParameter)),
                };
            }
            else if (genericTypeDefinition == typeof(KeyValuePair<,>))
            {
                /*
                 * new JSArray(new JSValue[] { (JSValue)value.Key, (JSValue)value.Value })
                 */
                statements = new[]
                {
                    Expression.Convert(
                        Expression.New(
                            typeof(JSArray).GetInstanceConstructor(new[] { typeof(JSValue[]) }),
                            Expression.NewArrayInit(typeof(JSValue),
                                InlineOrInvoke(
                                    BuildConvertToJSValueExpression(genericArguments![0]),
                                    Expression.Property(valueExpression, "Key"),
                                    nameof(BuildConvertToJSValueExpression)),
                                InlineOrInvoke(
                                    BuildConvertToJSValueExpression(genericArguments![1]),
                                    Expression.Property(valueExpression, "Value"),
                                    nameof(BuildConvertToJSValueExpression)))),
                        typeof(JSValue)),
                };
            }
            else if (fromType == typeof(ValueTuple))
            {
                // An empty tuple is marshalled as an empty array.
                statements = new[]
                {
                    Expression.Convert(
                        Expression.New(typeof(JSArray).GetInstanceConstructor([])),
                        typeof(JSValue)),
                };
            }
            else if (genericTypeDefinition?.Name.StartsWith("ValueTuple`") == true)
            {
                /*
                 * new JSArray(new JSValue[] { (JSValue)value.Item1, (JSValue)value.Item2... })
                 */
                Expression TupleItem(int index) => InlineOrInvoke(
                    BuildConvertToJSValueExpression(genericArguments![index]),
                    Expression.Field(valueExpression, "Item" + (index + 1)),
                    nameof(BuildConvertToJSValueExpression));
                statements = new[]
                {
                    Expression.Convert(
                        Expression.New(
                            typeof(JSArray).GetInstanceConstructor(new[] { typeof(JSValue[]) }),
                            Expression.NewArrayInit(typeof(JSValue),
                                genericArguments!.Select((_, i) =>  TupleItem(i)).ToArray())),
                        typeof(JSValue)),
                };
            }
            else if (fromType == typeof(DateTime))
            {
                MethodInfo fromDateTime = typeof(JSDate).GetStaticMethod(
                    nameof(JSDate.FromDateTime));
                MethodInfo asJSValue = typeof(JSDate).GetImplicitConversion(
                    typeof(JSDate), typeof(JSValue));
                statements = new[]
                {
                    Expression.Call(asJSValue, Expression.Call(fromDateTime, valueParameter)),
                };
            }
            else if (fromType == typeof(CancellationToken))
            {
                MethodInfo toAbortSignal = typeof(JSAbortSignal).GetExplicitConversion(
                    nullableType ?? fromType, typeof(JSAbortSignal));
                MethodInfo asJSValue = typeof(JSAbortSignal).GetImplicitConversion(
                    typeof(JSAbortSignal), typeof(JSValue));

                statements = new[]
                {
                    Expression.Call(asJSValue, Expression.Call(toAbortSignal, valueParameter)),
                };
            }
            else
            {
                statements = BuildToJSFromStructExpressions(fromType, variables, valueExpression);
            }
        }
        else if (fromType.IsClass)
        {
            if (fromType.BaseType == typeof(MulticastDelegate))
            {
                statements = new[]
                {
                    Expression.Invoke(
                        BuildFromJSFunctionExpression(fromType),
                        valueParameter),
                };
            }
            else if (fromType == typeof(Tuple))
            {
                // An empty tuple is marshalled as an empty array.
                statements = new[]
                {
                    Expression.Convert(
                        Expression.New(typeof(JSArray).GetInstanceConstructor([])),
                        typeof(JSValue)),
                };
            }
            else if (fromType.IsGenericType && fromType.Name.StartsWith("Tuple`") == true)
            {
                /*
                 * new JSArray(new JSValue[] { (JSValue)value.Item1, (JSValue)value.Item2... })
                 */
                Type[]? genericArguments = fromType.GetGenericArguments();
                Expression TupleItem(int index) => InlineOrInvoke(
                    BuildConvertToJSValueExpression(genericArguments![index]),
                    Expression.Property(valueExpression, "Item" + (index + 1)),
                    nameof(BuildConvertToJSValueExpression));
                statements = new[]
                {
                    Expression.Convert(
                        Expression.New(
                            typeof(JSArray).GetInstanceConstructor(new[] { typeof(JSValue[]) }),
                            Expression.NewArrayInit(typeof(JSValue),
                                genericArguments!.Select((_, i) =>  TupleItem(i)).ToArray())),
                        typeof(JSValue)),
                };
            }
            else
            {
                MethodInfo getOrCreateObjectWrapper =
                    s_getOrCreateObjectWrapper.MakeGenericMethod(fromType);
                statements = new[]
                {
                    Expression.Call(
                        Expression.Property(null, s_context),
                        getOrCreateObjectWrapper,
                        valueExpression),
                };
            }
        }
        else if (fromType.IsInterface && fromType.Namespace == typeof(ICollection<>).Namespace)
        {
            statements = BuildToJSFromCollectionExpressions(fromType, variables, valueExpression);
        }
        else if (fromType.IsInterface)
        {
            // The object may extend JSInterface if it is a generated interface proxy class.
            // Otherwise it is a C# class implementing the interface, that needs a class wrapper.
            MethodInfo getOrCreateObjectWrapper =
                s_getOrCreateObjectWrapper.MakeGenericMethod(fromType);
            statements = new[]
            {
                /*
                 * JSInterface.GetJSValue(value) ??
                 *     JSRuntimeContext.Current.GetOrCreateObjectWrapper(value)
                 */
                Expression.Coalesce(
                    Expression.Call(
                        typeof(JSInterface).GetStaticMethod(nameof(JSInterface.GetJSValue)),
                        valueExpression),
                    Expression.Call(
                        Expression.Property(null, s_context),
                        getOrCreateObjectWrapper,
                        valueExpression)),
            };
        }
        else
        {
            throw new NotImplementedException(
                $"Conversion to {nameof(JSValue)} from {fromType.FullName} is not implemented.");
        }

        if (nullableType != null)
        {
            statements = new[]
            {
                Expression.Condition(
                    Expression.Property(valueParameter, nullableType.GetProperty("HasValue")!),
                    Expression.Block(typeof(JSValue), variables, statements),
                    Expression.Default(typeof(JSValue))),
            };
        }

        return Expression.Lambda(
            delegateType,
            body: Expression.Block(typeof(JSValue), variables, statements),
            name: delegateName,
            parameters: new[] { valueParameter });
    }

    private LambdaExpression BuildConvertFromJSPromiseExpression(Type toType)
    {
        Type delegateType = typeof(JSValue.To<>).MakeGenericType(toType);
        string delegateName = "to_" + FullTypeName(toType);

        ParameterExpression valueParameter = Expression.Parameter(typeof(JSValue), "value");
        Expression asTaskExpression;

        if (toType == typeof(Task))
        {
            /*
             * ((JSPromise)value).AsTask()
             */
            asTaskExpression = Expression.Call(
                typeof(TaskExtensions).GetStaticMethod(
                    nameof(TaskExtensions.AsTask), new[] { typeof(JSPromise) }),
                Expression.Convert(
                    valueParameter,
                    typeof(JSPromise),
                    typeof(JSPromise).GetExplicitConversion(typeof(JSValue), typeof(JSPromise))));
        }
        else if (toType.IsGenericType && toType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            /*
             * ((JSPromise)value).AsTask<T>((value) => (T)value)
             */
            Type resultType = toType.GenericTypeArguments[0];
            asTaskExpression = Expression.Call(
                typeof(TaskExtensions).GetStaticMethod(
                    nameof(TaskExtensions.AsTask),
                    new[] { typeof(JSPromise), typeof(JSValue.To<>) }, resultType),
                Expression.Convert(
                    valueParameter,
                    typeof(JSPromise),
                    typeof(JSPromise).GetExplicitConversion(typeof(JSValue), typeof(JSPromise))),
                    GetFromJSValueExpression(resultType));
        }
        else
        {
            throw new ArgumentException($"Invalid task type: {toType}", nameof(toType));
        }

        ParameterExpression[] variables = new[] { valueParameter };
        return Expression.Lambda(
            delegateType,
            body: Expression.Block(toType, variables, new[] { asTaskExpression }),
            name: delegateName,
            parameters: variables);
    }


    private LambdaExpression BuildConvertToJSPromiseExpression(Type fromType)
    {
        Type delegateType = typeof(JSValue.From<>).MakeGenericType(fromType);
        string delegateName = "from_" + FullTypeName(fromType);

        ParameterExpression valueParameter = Expression.Parameter(fromType, "value");
        Expression asPromiseExpression;

        if (fromType == typeof(Task))
        {
            /*
             * (JSValue)value.AsPromise()
             */
            asPromiseExpression = Expression.Convert(
                Expression.Call(
                    typeof(TaskExtensions).GetStaticMethod(
                        nameof(TaskExtensions.AsPromise), new[] { typeof(Task) }),
                    valueParameter),
                typeof(JSValue),
                typeof(JSPromise).GetImplicitConversion(typeof(JSPromise), typeof(JSValue)));
        }
        else if (fromType.IsGenericType && fromType.GetGenericTypeDefinition() == typeof(Task<>))
        {
            /*
             * (JSValue)value.AsPromise<T>((value) => (JSValue)value)
             */
            Type resultType = fromType.GenericTypeArguments[0];
            asPromiseExpression = Expression.Convert(
                Expression.Call(
                    typeof(TaskExtensions).GetStaticMethod(
                        nameof(TaskExtensions.AsPromise),
                        new[] { typeof(Task), typeof(JSValue.From<>) }),
                    valueParameter,
                    GetToJSValueExpression(resultType)),
                typeof(JSValue),
                typeof(JSPromise).GetImplicitConversion(typeof(JSPromise), typeof(JSValue)));
        }
        else
        {
            throw new ArgumentException($"Invalid task type: {fromType}", nameof(fromType));
        }

        ParameterExpression[] variables = new[] { valueParameter };
        return Expression.Lambda(
            delegateType,
            body: Expression.Block(typeof(JSValue), variables, new[] { asPromiseExpression }),
            name: delegateName,
            parameters: variables);
    }

    private IEnumerable<Expression> BuildFromJSToArrayExpressions(
        Type elementType,
        List<ParameterExpression> variables,
        ParameterExpression valueVariable)
    {
        /*
         * JSArray jsArray = (JSArray)value;
         * ElementType[] array = new ElementType[jsArray.Length];
         * jsArray.CopyTo(array, 0, (item) => (ElementType)item);
         * return array;
         */
        ParameterExpression jsArrayVariable = Expression.Parameter(typeof(JSArray), "jsArray");
        ParameterExpression arrayVariable = Expression.Parameter(
            elementType.MakeArrayType(), "array");
        variables.Add(jsArrayVariable);
        variables.Add(arrayVariable);

        MethodInfo castMethod = typeof(JSArray).GetExplicitConversion(
            typeof(JSValue), typeof(JSArray));
        yield return Expression.Assign(
            jsArrayVariable,
            Expression.Convert(valueVariable, typeof(JSArray), castMethod));

        PropertyInfo jsArrayLengthProperty = typeof(JSArray).GetProperty(nameof(JSArray.Length))!;
        yield return Expression.Assign(
            arrayVariable,
            Expression.NewArrayBounds(
                elementType, Expression.Property(jsArrayVariable, jsArrayLengthProperty)));

        LambdaExpression fromJSExpression = GetFromJSValueExpression(elementType);
        MethodInfo arrayCopyMethod = typeof(JSArray)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where((m) => m.IsGenericMethodDefinition && m.Name == nameof(JSArray.CopyTo))
            .Single().MakeGenericMethod(elementType);
        yield return Expression.Call(
            jsArrayVariable,
            arrayCopyMethod,
            arrayVariable,
            Expression.Constant(0),
            fromJSExpression);

        yield return arrayVariable;
    }

    private IEnumerable<Expression> BuildToJSFromArrayExpressions(
        Type elementType,
        List<ParameterExpression> variables,
        Expression valueExpression)
    {
        /*
         * JSArray jsArray = new JSArray(value.Length);
         * jsArray.CopyFrom(value, 0, (item) => (JSValue)item);
         * return jsArray;
         */
        ParameterExpression jsArrayVariable = Expression.Variable(typeof(JSArray), "jsArray");
        variables.Add(jsArrayVariable);

        PropertyInfo arrayLengthProperty = typeof(Array).GetProperty(nameof(Array.Length))!;
        ConstructorInfo jsArrayConstructor = typeof(JSArray).GetConstructor(new[] { typeof(int) })!;
        yield return Expression.Assign(
            jsArrayVariable,
            Expression.New(jsArrayConstructor,
                Expression.Property(valueExpression, arrayLengthProperty)));

        MethodInfo arrayCopyMethod = typeof(JSArray).GetInstanceMethod(nameof(JSArray.CopyFrom))
            .MakeGenericMethod(elementType);
        yield return Expression.Call(
            jsArrayVariable,
            arrayCopyMethod,
            valueExpression,
            Expression.Constant(0),
            GetToJSValueExpression(elementType));

        MethodInfo cast = GetCastToJSValueMethod(typeof(JSArray))!;
        yield return Expression.Convert(jsArrayVariable, typeof(JSValue), cast);
    }

    private IEnumerable<Expression> BuildFromJSToStructExpressions(
        Type toType,
        List<ParameterExpression> variables,
        ParameterExpression valueVariable)
    {
        /*
         * StructName obj = default;
         * obj.Property0 = (Property0Type)value["property0"];
         * ... 
         * return obj;
         */
        ParameterExpression objVariable = Expression.Variable(toType, "obj");
        variables.Add(objVariable);

        yield return Expression.Assign(objVariable, Expression.Default(toType));

        foreach (PropertyInfo property in toType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.SetMethod == null || property.SetMethod.GetParameters().Length > 1)
            {
                // Skip indexed properties, where the setter takes one or more parameters. 
                continue;
            }

            Expression propertyName = Expression.Constant(ToCamelCase(property.Name));
            yield return Expression.Assign(
                Expression.Property(objVariable, property),
                InlineOrInvoke(
                    GetFromJSValueExpression(property.PropertyType),
                    Expression.Property(valueVariable, s_valueItem, propertyName),
                    nameof(BuildFromJSToStructExpressions)));
        }

        yield return objVariable;
    }

    private IEnumerable<Expression> BuildToJSFromStructExpressions(
        Type fromType,
        List<ParameterExpression> variables,
        Expression valueExpression)
    {
        /*
         * JSValue jsValue = JSRuntimeContext.Current.CreateStruct<StructName>();
         * jsValue["property0"] = (JSValue)value.Property0;
         * ...
         * return jsValue;
         */
        ParameterExpression jsValueVariable = Expression.Variable(typeof(JSValue), "jsValue");
        variables.Add(jsValueVariable);

        if (fromType.GetCustomAttribute(typeof(JSImportAttribute)) != null)
        {
            // Imported structs are assumed to be plain JS objects (not JS classes).
            MethodInfo createObjectMethod = typeof(JSValue)
                .GetStaticMethod(nameof(JSValue.CreateObject));
            yield return Expression.Assign(
                jsValueVariable, Expression.Call(createObjectMethod));
        }
        else
        {
            MethodInfo createStructMethod = typeof(JSRuntimeContext)
                .GetInstanceMethod(nameof(JSRuntimeContext.CreateStruct))
                !.MakeGenericMethod(fromType);
            yield return Expression.Assign(
                jsValueVariable,
                Expression.Call(Expression.Property(null, s_context), createStructMethod));
        }

        foreach (PropertyInfo property in fromType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetMethod == null || property.GetMethod.GetParameters().Length > 0)
            {
                // Skip indexed properties, where the getter takes one or more parameters. 
                continue;
            }

            Expression propertyName = Expression.Constant(ToCamelCase(property.Name));
            yield return Expression.Assign(
                Expression.Property(jsValueVariable, s_valueItem, propertyName),
                InlineOrInvoke(
                    GetToJSValueExpression(property.PropertyType),
                    Expression.Property(valueExpression, property),
                    nameof(BuildToJSFromStructExpressions)));
        }

        yield return jsValueVariable;
    }

    private IEnumerable<Expression> BuildFromJSToCollectionExpressions(
        Type toType,
        ICollection<ParameterExpression> variables,
        Expression valueExpression)
    {
        Type elementType = toType.GenericTypeArguments[0];
        Type typeDefinition = toType.GetGenericTypeDefinition();

        if (typeDefinition == typeof(IList<>) ||
            typeDefinition == typeof(ICollection<>) ||
#if !NETFRAMEWORK
            typeDefinition == typeof(IReadOnlySet<>) ||
#endif
            typeDefinition == typeof(ISet<>))
        {
            /*
             * JSNativeApi.TryUnwrap(value) as ICollection<T> ??
             *     ((JSArray)value).AsCollection<T>(
             *         (value) => (T)value,
             *         (value) => (JSValue)value);
             */
            Type jsCollectionType = typeDefinition.Name.Contains("Set") ?
                typeof(JSSet) : typeof(JSArray);
            MethodInfo asCollectionMethod = typeof(JSCollectionExtensions).GetStaticMethod(
#if NETFRAMEWORK
                "As" + typeDefinition.Name.Substring(1, typeDefinition.Name.IndexOf('`') - 1),
#else
                string.Concat("As",
                    typeDefinition.Name.AsSpan(1, typeDefinition.Name.IndexOf('`') - 1)),
#endif
                new[] { jsCollectionType, typeof(JSValue.To<>), typeof(JSValue.From<>) },
                elementType);
            MethodInfo asJSCollectionMethod = jsCollectionType.GetExplicitConversion(
                typeof(JSValue), jsCollectionType);
            yield return Expression.Coalesce(
                Expression.TypeAs(Expression.Call(s_tryUnwrap, valueExpression), toType),
                Expression.Call(
                    asCollectionMethod,
                    Expression.Convert(valueExpression, jsCollectionType, asJSCollectionMethod),
                    GetFromJSValueExpression(elementType),
                    GetToJSValueExpression(elementType)));
        }
        else if (typeDefinition == typeof(IReadOnlyList<>) ||
            typeDefinition == typeof(IReadOnlyCollection<>) ||
            typeDefinition == typeof(IEnumerable<>) ||
            typeDefinition == typeof(IAsyncEnumerable<>))
        {
            /*
             * JSNativeApi.TryUnwrap(value) as IReadOnlyCollection<T> ??
             *     ((JSArray)value).AsReadOnlyCollection<T>((value) => (T)value);
             */
            Type jsCollectionType = typeDefinition == typeof(IEnumerable<>) ?
                typeof(JSIterable) : typeDefinition == typeof(IAsyncEnumerable<>) ?
                typeof(JSAsyncIterable) : typeof(JSArray);
            MethodInfo asCollectionMethod = typeof(JSCollectionExtensions).GetStaticMethod(
#if NETFRAMEWORK
                "As" + typeDefinition.Name.Substring(1, typeDefinition.Name.IndexOf('`') - 1),
#else
                string.Concat("As",
                    typeDefinition.Name.AsSpan(1, typeDefinition.Name.IndexOf('`') - 1)),
#endif
                new[] { jsCollectionType, typeof(JSValue.To<>) },
                elementType);
            MethodInfo asJSCollectionMethod = jsCollectionType.GetExplicitConversion(
                typeof(JSValue), jsCollectionType);
            yield return Expression.Coalesce(
                Expression.TypeAs(Expression.Call(s_tryUnwrap, valueExpression), toType),
                Expression.Call(
                    asCollectionMethod,
                    Expression.Convert(valueExpression, jsCollectionType, asJSCollectionMethod),
                    GetFromJSValueExpression(elementType)));
        }
        else if (typeDefinition == typeof(IDictionary<,>))
        {
            Type keyType = elementType;
            Type valueType = toType.GenericTypeArguments[1];

            /*
             * JSNativeApi.TryUnwrap(value) as IDictionary<TKey, TValue> ??
             *     ((JSMap)value).AsDictionary<TKey, TValue>(
             *         (key) => (TKey)key,
             *         (value) => (TValue)value,
             *         (key) => (JSValue)key);
             *         (value) => (JSValue)value);
             */
            MethodInfo asDictionaryMethod = typeof(JSCollectionExtensions).GetStaticMethod(
                nameof(JSCollectionExtensions.AsDictionary))!.MakeGenericMethod(keyType, valueType);
            MethodInfo asJSMapMethod = typeof(JSMap).GetExplicitConversion(
                typeof(JSValue), typeof(JSMap));
            yield return Expression.Coalesce(
                Expression.TypeAs(Expression.Call(s_tryUnwrap, valueExpression), toType),
                Expression.Call(
                    asDictionaryMethod,
                    Expression.Convert(valueExpression, typeof(JSMap), asJSMapMethod),
                    GetFromJSValueExpression(keyType),
                    GetFromJSValueExpression(valueType),
                    GetToJSValueExpression(keyType),
                    GetToJSValueExpression(valueType)));
        }
        else if (typeDefinition == typeof(IReadOnlyDictionary<,>))
        {
            Type keyType = elementType;
            Type valueType = toType.GenericTypeArguments[1];

            /*
             * JSNativeApi.TryUnwrap(value) as IReadOnlyDictionary<TKey, TValue> ??
             *     ((JSMap)value).AsReadOnlyDictionary<TKey, TValue>(
             *         (key) => (TKey)key,
             *         (value) => (TValue)value,
             *         (key) => (JSValue)key);
             */
            MethodInfo asDictionaryMethod = typeof(JSCollectionExtensions).GetStaticMethod(
                nameof(JSCollectionExtensions.AsReadOnlyDictionary))
                !.MakeGenericMethod(keyType, valueType);
            MethodInfo asJSMapMethod = typeof(JSMap).GetExplicitConversion(
                typeof(JSValue), typeof(JSMap));
            yield return Expression.Coalesce(
                Expression.TypeAs(Expression.Call(s_tryUnwrap, valueExpression), toType),
                Expression.Call(
                    asDictionaryMethod,
                    Expression.Convert(valueExpression, typeof(JSMap), asJSMapMethod),
                    GetFromJSValueExpression(keyType),
                    GetFromJSValueExpression(valueType),
                    GetToJSValueExpression(keyType)));
        }
        else
        {
            // Marshal the unknown collection type as undefined.
            // Throwing an exception here might be helpful in some cases, but in other
            // cases it may block use of the rest of the (supported) members of the type.
            yield return Expression.Default(toType);
        }
    }

    private IEnumerable<Expression> BuildToJSFromCollectionExpressions(
        Type fromType,
        ICollection<ParameterExpression> variables,
        Expression valueExpression)
    {
        Type elementType = fromType.GenericTypeArguments[0];
        Type typeDefinition = fromType.GetGenericTypeDefinition();

        if (typeDefinition == typeof(IList<>) ||
            typeDefinition == typeof(ICollection<>) ||
#if !NETFRAMEWORK
            typeDefinition == typeof(IReadOnlySet<>) ||
#endif
            typeDefinition == typeof(ISet<>))
        {
            /*
             * JSRuntimeContext.Current.GetOrCreateCollectionWrapper(
             *     value, (value) => (JSValue)value, (value) => (ElementType)value);
             */
            MethodInfo wrapMethod = typeof(JSRuntimeContext).GetInstanceMethod(
                    nameof(JSRuntimeContext.GetOrCreateCollectionWrapper),
                    new[] { typeDefinition, typeof(JSValue.From<>), typeof(JSValue.To<>) },
                    elementType);
            yield return Expression.Call(
                Expression.Property(null, s_context),
                wrapMethod,
                valueExpression,
                GetToJSValueExpression(elementType),
                GetFromJSValueExpression(elementType));
        }
        else if (typeDefinition == typeof(IReadOnlyList<>) ||
            typeDefinition == typeof(IReadOnlyCollection<>) ||
            typeDefinition == typeof(IEnumerable<>) ||
            typeDefinition == typeof(IAsyncEnumerable<>))
        {
            /*
             * JSRuntimeContext.Current.GetOrCreateCollectionWrapper(
             *     value, (value) => (JSValue)value);
             */
            MethodInfo wrapMethod = typeof(JSRuntimeContext).GetInstanceMethod(
                    nameof(JSRuntimeContext.GetOrCreateCollectionWrapper),
                    new[] { typeDefinition, typeof(JSValue.From<>) },
                    elementType);
            yield return Expression.Call(
                Expression.Property(null, s_context),
                wrapMethod,
                valueExpression,
                GetToJSValueExpression(elementType));
        }
        else if (typeDefinition == typeof(System.Collections.ObjectModel.ReadOnlyCollection<>))
        {
            /*
             * JSRuntimeContext.Current.GetOrCreateCollectionWrapper(
             *     (IReadOnlyCollection<T>)value, (value) => (JSValue)value);
             */
            MethodInfo wrapMethod = typeof(JSRuntimeContext).GetInstanceMethod(
                    nameof(JSRuntimeContext.GetOrCreateCollectionWrapper),
                    new[] { typeof(IReadOnlyCollection<>), typeof(JSValue.From<>) },
                    elementType);
            yield return Expression.Call(
                Expression.Property(null, s_context),
                wrapMethod,
                valueExpression,
                GetToJSValueExpression(elementType));
        }
        else if (typeDefinition == typeof(IDictionary<,>))
        {
            Type keyType = elementType;
            Type valueType = fromType.GenericTypeArguments[1];

            /*
             * JSRuntimeContext.Current.GetOrCreateCollectionWrapper(
             *     value,
             *     (key) => (JSValue)key,
             *     (value) => (JSValue)value,
             *     (key) => (KeyType)key,
             *     (value) => (ValueType)value);
             */
            MethodInfo wrapMethod = typeof(JSRuntimeContext).GetInstanceMethod(
                    nameof(JSRuntimeContext.GetOrCreateCollectionWrapper),
                    new[]
                    {
                        typeDefinition,
                        typeof(JSValue.From<>),
                        typeof(JSValue.From<>),
                        typeof(JSValue.To<>),
                        typeof(JSValue.To<>),
                    },
                    keyType,
                    valueType);
            yield return Expression.Call(
                Expression.Property(null, s_context),
                wrapMethod,
                valueExpression,
                GetToJSValueExpression(keyType),
                GetToJSValueExpression(valueType),
                GetFromJSValueExpression(keyType),
                GetFromJSValueExpression(valueType));
        }
        else if (typeDefinition == typeof(IReadOnlyDictionary<,>))
        {
            Type keyType = elementType;
            Type valueType = fromType.GenericTypeArguments[1];

            /*
             * JSRuntimeContext.Current.GetOrCreateCollectionWrapper(
             *     value,
             *     (key) => (JSValue)key,
             *     (value) => (JSValue)value,
             *     (key) => (KeyType)key)
             */
            MethodInfo wrapMethod = typeof(JSRuntimeContext).GetInstanceMethod(
                    nameof(JSRuntimeContext.GetOrCreateCollectionWrapper),
                    new[]
                    {
                        typeDefinition,
                        typeof(JSValue.From<>),
                        typeof(JSValue.From<>),
                        typeof(JSValue.To<>),
                    },
                    keyType,
                    valueType);
            yield return Expression.
/* Unmerged change from project 'NodeApi.DotNetHost(net6.0)'
Before:
                var typeArgs = string.Join("_", type.GenericTypeArguments.Select(FullTypeName));
After:
                string typeArgs = string.Join("_", type.GenericTypeArguments.Select(FullTypeName));
*/

/* Unmerged change from project 'NodeApi.DotNetHost(net472)'
Before:
                var typeArgs = string.Join("_", type.GenericTypeArguments.Select(FullTypeName));
After:
                string typeArgs = string.Join("_", type.GenericTypeArguments.Select(FullTypeName));
*/
Call(
                Expression.Property(null, s_context),
                wrapMethod,
                valueExpression,
                GetToJSValueExpression(keyType),
                GetToJSValueExpression(valueType),
                GetFromJSValueExpression(keyType));
        }
        else
        {
            // Marshal the unknown collection type as null.
            // Throwing an exception here might be helpful in some cases, but in other
            // cases it may block use of the rest of the (supported) members of the type.
            yield return Expression.Default(typeof(JSValue));
        }
    }

    private static MethodInfo? GetCastFromJSValueMethod(Type toType)
    {
        if (toType == typeof(JSValue)) return null;
        return typeof(JSValue).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where((m) => m.Name == "op_Explicit" && m.ReturnType == toType &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == typeof(JSValue))
            .SingleOrDefault();
    }

    private static MethodInfo? GetCastToJSValueMethod(Type fromType)
    {
        if (fromType == typeof(JSValue)) return null;
        return typeof(JSValue).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where((m) => m.Name == "op_Implicit" && m.ReturnType == typeof(JSValue) &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == fromType)
            .SingleOrDefault() ?? fromType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where((m) => m.Name == "op_Implicit" && m.ReturnType == typeof(JSValue))
            .SingleOrDefault();
    }

    /// <summary>
    /// Checks whether an element type is one of the JS TypedArray element types.
    /// </summary>
    private static bool IsTypedArrayType(Type elementType)
    {
        return elementType == typeof(sbyte)
            || elementType == typeof(byte)
            || elementType == typeof(short)
            || elementType == typeof(ushort)
            || elementType == typeof(int)
            || elementType == typeof(uint)
            || elementType == typeof(long)
            || elementType == typeof(ulong)
            || elementType == typeof(float)
            || elementType == typeof(double);
    }

    private static string FullMethodName(MethodInfo method)
    {
        string prefix = string.Empty;
        string name = method.Name;
        if (name.StartsWith("get_") || name.StartsWith("set_"))
        {
            prefix = name.Substring(0, 4);
            name = name.Substring(4);
        }

        return $"{prefix}{FullTypeName(method.DeclaringType!)}_{name}";
    }

    internal static string FullTypeName(Type type)
    {
        string? ns = type.Namespace;
        string name = type.Name;

        if (type.IsGenericType)
        {
            int nameEnd = name.IndexOf('`');
            if (nameEnd >= 0)
            {
                string typeArgs = string.Join("_", type.GenericTypeArguments.Select(FullTypeName));
#if NETFRAMEWORK
                name = name.Substring(0, nameEnd) + "_of_" + typeArgs;
#else
                name = string.Concat(name.AsSpan(0, nameEnd), "_of_", typeArgs);
#endif
            }
        }

        if (type.IsNested)
        {
            return $"{FullTypeName(type.DeclaringType!)}_{name}";
        }

        return string.IsNullOrEmpty(ns) ? name : $"{ns.Replace('.', '_')}_{name}";
    }

    /// <summary>
    /// Creates a parameter expression for a method parameter.
    /// </summary>
    private static ParameterExpression Parameter(ParameterInfo parameter)
    {
        Type parameterType = parameter.ParameterType;
        string parameterName = parameter.Name!;

        if (parameter.GetCustomAttribute<OutAttribute>() is not null)
        {
            if (!parameterType.IsByRef)
            {
                parameterType = parameterType.MakeByRefType();
            }

            if (parameter.GetCustomAttribute<InAttribute>() is null)
            {
                // ParameterExpression doesn't distinguish between ref and out parameters,
                // but source generators need to use the correct keyword.
                parameterName = OutParameterPrefix + parameterName;
            }
        }

        if (parameterType.IsGenericTypeDefinition || parameterType.IsGenericParameter)
        {
            parameterType = typeof(object);
        }

        return Expression.Parameter(parameterType, parameterName);
    }

    /// <summary>
    /// Creates a variable expression for a method parameter (for converting arguments before
    /// calling the method).
    /// </summary>
    private static ParameterExpression Variable(ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsByRef)
        {
            return Expression.Parameter(parameter.ParameterType.GetElementType()!, parameter.Name);
        }
        else
        {
            return Expression.Parameter(parameter.ParameterType, parameter.Name);
        }
    }

    /// <summary>
    /// If the lambda expression consists of a single statement, the statement expression is returned
    /// directly, with the "value" parameter replaced with the target expression. Otherwise,
    /// an invocation expression for the lambda is returned.
    /// </summary>
    private static Expression InlineOrInvoke(
        LambdaExpression lambda,
        Expression targetExpression,
        string? caller)
    {
        if (lambda.Body is BlockExpression block && block.Expressions.Count == 1)
        {
            return new VariableReplacer("value", targetExpression)
                .VisitAndConvert(block.Expressions.Single(), caller);
        }
        else
        {
            return Expression.Invoke(lambda, targetExpression);
        }
    }

    /// <summary>
    /// Modifies an expression tree by replacing a variable with another expression. Useful for
    /// inlining a lambda expression within another expression tree.
    /// </summary>
    private class VariableReplacer : ExpressionVisitor
    {
        private readonly string _replaceVariableName;
        private readonly Expression _replacementExpression;

        public VariableReplacer(string replaceVariableName, Expression replacementExpression)
        {
            _replaceVariableName = replaceVariableName;
            _replacementExpression = replacementExpression;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node.Name == _replaceVariableName ? _replacementExpression : node;

        // Do not recursively visit nested lambdas.
        protected override Expression VisitLambda<T>(Expression<T> node) => node;
    }

    /// <summary>
    /// Converts a lambda expression that takes a <see cref="JSValue"/> as the first parameter
    /// to a lambda expression that references the the member value of a <see cref="JSInterface"/>
    /// and automatically switches to the JS thread.
    /// </summary>
    public LambdaExpression MakeInterfaceExpression(LambdaExpression methodExpression)
    {
        ParameterExpression thisVariable = Expression.Variable(typeof(JSInterface), "this");
        ParameterExpression[] parameters = methodExpression.Parameters.Skip(1).ToArray();
        Type returnType = methodExpression.Body.Type;

        if (parameters.Any((p) => p.IsByRef))
        {
            PropertyInfo valueProperty = typeof(JSInterface).GetProperty(
                "Value", BindingFlags.Instance | BindingFlags.NonPublic)!;

            // Ref/out parameters cannot be used within a lambda expression. So interface
            // methods with ref/out parameters will not automatically switch to the JS thread.
            // (The caller must be already on the JS thread.)
            // TODO: Use temporary variables to avoid this limitation.
            return Expression.Lambda(
                _delegates.Value.GetToJSDelegateType(returnType, parameters),
                Expression.Block(
                    returnType,
                    new Expression[]
                    {
                        Expression.Assign(
                            methodExpression.Parameters.First(),
                            Expression.Property(thisVariable, valueProperty)),
                    }.Concat(((BlockExpression)methodExpression.Body).Expressions)),
                methodExpression.Name,
                parameters);
        }

        PropertyInfo valueReferenceProperty = typeof(JSInterface).GetProperty(
            "ValueReference", BindingFlags.Instance | BindingFlags.NonPublic)!;

        // Use the JSReference.Run() method to switch to the JS thread when operating on the value.
        Type runDelegateType;
        MethodInfo runMethod;
        if (returnType == typeof(void))
        {
            runDelegateType = typeof(Action<JSValue>);
            runMethod = typeof(JSReference).GetInstanceMethod(
                nameof(JSReference.Run), new[] { typeof(Action<JSValue>) });
        }
        else
        {
            runDelegateType = typeof(Func<,>).MakeGenericType(typeof(JSValue), returnType);
            runMethod = typeof(JSReference)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Single((m) => m.Name == nameof(JSReference.Run) && m.IsGenericMethodDefinition)
                .MakeGenericMethod(returnType);
        }

        // Build a lambda expression that moves the __this parameter from the original lambda
        // to the inner lambda where the parameter is supplied by the Run() callback.
        return Expression.Lambda(
            _delegates.Value.GetToJSDelegateType(returnType, parameters),
            Expression.Block(returnType, Expression.Call(
                Expression.Property(thisVariable, valueReferenceProperty),
                runMethod,
                Expression.Lambda(
                    runDelegateType,
                    methodExpression.Body,
                    methodExpression.Parameters.Take(1)))),
            methodExpression.Name,
            parameters);
    }

    internal static AssemblyBuilder CreateAssemblyBuilder(Type forType)
    {
        string assemblyName = forType.FullName + "_" + Environment.CurrentManagedThreadId;

#if NETFRAMEWORK
        bool collectible = false;
#else
        // Make the dynamic assembly collectible if in a collectible load context.
        // The delegate types generated by lambda expressions are not collectible by default;
        // the custom marshalling delegates resolve that problem.
        bool collectible = System.Runtime.Loader.AssemblyLoadContext.Default.IsCollectible;
#endif

        return AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            collectible ? AssemblyBuilderAccess.RunAndCollect : AssemblyBuilderAccess.Run);
    }
}
