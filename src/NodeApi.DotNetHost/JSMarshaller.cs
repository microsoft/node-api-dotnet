// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
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
    private readonly Lazy<JSInterfaceMarshaller> _interfaceMarshaller = new();

    private readonly ConcurrentDictionary<Type, Delegate> _fromJSDelegates = new();
    private readonly ConcurrentDictionary<Type, Delegate> _toJSDelegates = new();
    private readonly ConcurrentDictionary<Type, LambdaExpression> _fromJSExpressions = new();
    private readonly ConcurrentDictionary<Type, LambdaExpression> _toJSExpressions = new();

    private static readonly ParameterExpression s_argsParameter =
        Expression.Parameter(typeof(JSCallbackArgs), "__args");
    private static readonly IEnumerable<ParameterExpression> s_argsArray =
        new[] { s_argsParameter };

    // Cache some reflected members that are frequently referenced in expressions.

    private static readonly PropertyInfo s_context =
        typeof(JSRuntimeContext).GetStaticProperty(nameof(JSRuntimeContext.Current))!;

    private static readonly PropertyInfo s_moduleContext =
        typeof(JSModuleContext).GetStaticProperty(nameof(JSModuleContext.Current))!;

    private static readonly PropertyInfo s_undefinedValue =
        typeof(JSValue).GetStaticProperty(nameof(JSValue.Undefined))!;

    private static readonly PropertyInfo s_nullValue =
        typeof(JSValue).GetStaticProperty(nameof(JSValue.Null))!;

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
    /// Checks whether a type is converted to a JavaScript built-in type.
    /// </summary>
    public static bool IsConvertedType(Type type)
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
            (type == typeof(Task<>) ||
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
                BuildArgumentExpression(i, parameters[i].ParameterType)));
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

        MethodInfo? getMethod = property.GetMethod;
        if (getMethod is null) throw new ArgumentException("Property does not have a get method.");

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

        MethodInfo? setMethod = property.SetMethod;
        if (setMethod is null) throw new ArgumentException("Property does not have a set method.");

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

        try
        {
            string name = method.Name;
            if (method.DeclaringType!.IsInterface)
            {
                name = method.DeclaringType.Namespace + '.' +
                    method.DeclaringType.Name + '.' + name;
            }

            ParameterExpression resultVariable = Expression.Parameter(typeof(JSValue), "__result");

            ParameterInfo[] methodParameters = method.GetParameters();
            ParameterExpression[] parameters = new ParameterExpression[methodParameters.Length + 1];
            ParameterExpression thisParameter = Expression.Parameter(typeof(JSValue), "__this");
            parameters[0] = thisParameter;
            for (int i = 0; i < methodParameters.Length; i++)
            {
                parameters[i + 1] = Expression.Parameter(
                    methodParameters[i].ParameterType, methodParameters[i].Name);
            }

            /*
             * ReturnType MethodName(JSValue __this, Arg0Type arg0, ...)
             * {
             *     JSValue __result = __this.CallMethod("methodName", (JSValue)arg0, ...);
             *     return (ReturnType)__result;
             * }
             */

            Expression methodName = Expression.Convert(
                Expression.Constant(ToCamelCase(method.Name)),
                typeof(JSValue),
                typeof(JSValue).GetImplicitConversion(typeof(string), typeof(JSValue)));

            Expression ParameterToJSValue(int index) => InlineOrInvoke(
                GetToJSValueExpression(methodParameters[index].ParameterType),
                parameters[index + 1],
                nameof(BuildToJSMethodExpression));

            // Switch on parameter count to avoid allocating an array if < 4 parameters.
            // (Expression trees don't support stackallock.)
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

            return Expression.Lambda(
                Expression.Block(
                    typeof(void),
                    new[] { jsValueVariable },
                    new[] { convertStatement, setStatement }),
                name,
                new[] { thisParameter, valueParameter });
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
        IEnumerable<ParameterExpression> variables;
        ParameterInfo[] parameters = method.GetParameters();
        List<Expression> statements = new(parameters.Length + 2);

        for (int i = 0; i < parameters.Length; i++)
        {
            argVariables.Add(Expression.Variable(parameters[i].ParameterType, parameters[i].Name));
            statements.Add(Expression.Assign(argVariables[i],
                BuildArgumentExpression(i, parameters[i].ParameterType)));
        }

        if (method.ReturnType == typeof(void))
        {
            variables = argVariables;
            statements.Add(Expression.Call(method, argVariables));
            statements.Add(Expression.Property(null, s_undefinedValue));
        }
        else
        {
            ParameterExpression resultVariable = Expression.Variable(method.ReturnType, "__result");
            variables = argVariables.Append(resultVariable);
            statements.Add(Expression.Assign(resultVariable,
                Expression.Call(method, argVariables)));
            statements.Add(BuildResultExpression(resultVariable, method.ReturnType));
        }

        return (Expression<JSCallback>)Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables, statements),
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
            argVariables.Add(Expression.Variable(parameters[i].ParameterType, parameters[i].Name));
            statements.Add(Expression.Assign(argVariables[i],
                BuildArgumentExpression(i, parameters[i].ParameterType)));
        }

        if (method.ReturnType == typeof(void))
        {
            statements.Add(Expression.Call(thisVariable, method, argVariables));
            statements.Add(Expression.Label(returnTarget,
                Expression.Property(null, s_undefinedValue)));
        }
        else
        {
            ParameterExpression resultVariable = Expression.Variable(method.ReturnType, "__result");
            variables.Add(resultVariable);
            statements.Add(Expression.Assign(resultVariable,
                Expression.Call(thisVariable, method, argVariables)));
            statements.Add(Expression.Label(returnTarget,
                BuildResultExpression(resultVariable, method.ReturnType)));
        }

        return (Expression<JSCallback>)Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables.Concat(argVariables), statements),
            name: FullMethodName(method),
            parameters: s_argsArray);
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
            Type indexType = property.GetMethod.GetParameters()[0].ParameterType;
            propertyExpression = Expression.Property(
                thisVariable, property, BuildArgumentExpression(0, indexType));
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
            Expression.Property(null, s_undefinedValue),
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
            Type indexType = property.SetMethod.GetParameters()[0].ParameterType;
            setExpression = Expression.Call(
                thisVariable,
                property.SetMethod,
                BuildArgumentExpression(0, indexType),
                valueVariable);
        }

        statements.AddRange(BuildThisArgumentExpressions(
            property.DeclaringType!, thisVariable, returnTarget));
        statements.Add(Expression.Assign(valueVariable,
                    BuildArgumentExpression(0, property.PropertyType)));
        statements.Add(setExpression);
        statements.Add(Expression.Label(returnTarget, Expression.Property(null, s_undefinedValue)));

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
                Expression.Return(returnTarget, Expression.Property(null, s_undefinedValue)));
        }
        else if (type.IsClass || type.IsInterface)
        {
            // For normal instance methods, the .NET object is wrapped by the JS object.

            /*
             * ObjecType? __this = __args.ThisArg.Unwrap() as ObjectType;
             * if (__this == null) return JSValue.Undefined;
             */

            PropertyInfo thisArgProperty = typeof(JSCallbackArgs).GetProperty(
                nameof(JSCallbackArgs.ThisArg))!;
            MethodInfo unwrapMethod = typeof(JSNativeApi).GetMethod(nameof(JSNativeApi.Unwrap))!;
            yield return Expression.Assign(
                thisVariable,
                Expression.TypeAs(
                    Expression.Call(
                        unwrapMethod,
                        Expression.Property(s_argsParameter, thisArgProperty)),
                    type));
            yield return Expression.IfThen(
                Expression.Equal(thisVariable, Expression.Constant(null)),
                Expression.Return(returnTarget, Expression.Property(null, s_undefinedValue)));
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

    private Expression BuildArgumentExpression(
        int index,
        Type parameterType)
    {
        Expression argExpression = Expression.Property(
            s_argsParameter, s_callbackArg, Expression.Constant(index));

        Type? nullableType = null;
        if (parameterType.IsGenericType &&
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
                Expression.Convert(Expression.Constant(null), nullableType),
                Expression.Convert(convertExpression, nullableType));
        }

        return convertExpression;
    }

    private Expression BuildResultExpression(
        ParameterExpression resultVariable,
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
                Expression.Property(null, s_nullValue));
        }

        return resultExpression;
    }

    private LambdaExpression BuildConvertFromJSValueExpression(Type toType)
    {
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
            if (toType.IsGenericType &&
                (toType.GetGenericTypeDefinition() == typeof(Memory<>) ||
                toType.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>)))
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
            else
            {
                statements = new[]
                {
                    Expression.Convert(Expression.Call(s_unwrap, valueParameter), toType),
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
        else if (toType == typeof(JSValue))
        {
            statements = new[] { valueParameter };
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
            if (fromType.IsGenericType && fromType.GetGenericTypeDefinition() == typeof(Memory<>))
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
            else
            {
                statements = BuildToJSFromStructExpressions(fromType, variables, valueExpression);
            }
        }
        else if (fromType.IsClass)
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
        else if (fromType == typeof(JSValue))
        {
            statements = new[] { valueParameter };
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
                    Expression.Property(null, s_undefinedValue)),
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
        ICollection<ParameterExpression> variables,
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
        ICollection<ParameterExpression> variables,
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
        ICollection<ParameterExpression> variables,
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
            if (property.SetMethod == null)
            {
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
        ICollection<ParameterExpression> variables,
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

        MethodInfo createStructMethod = typeof(JSRuntimeContext).GetMethod(nameof(JSRuntimeContext.CreateStruct))
            !.MakeGenericMethod(fromType);
        yield return Expression.Assign(
            jsValueVariable,
            Expression.Call(Expression.Property(null, s_context), createStructMethod));

        foreach (PropertyInfo property in fromType.GetProperties(
            BindingFlags.Public | BindingFlags.Instance))
        {
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
            throw new NotImplementedException();
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
            yield return Expression.Call(
                Expression.Property(null, s_context),
                wrapMethod,
                valueExpression,
                GetToJSValueExpression(keyType),
                GetToJSValueExpression(valueType),
                GetFromJSValueExpression(keyType));
        }
        else
        {
            throw new NotImplementedException();
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
        string name = (type.Namespace ?? string.Empty).Replace('.', '_');

        if (type.IsGenericType)
        {
            name = name + '_' + type.Name.Substring(0, type.Name.IndexOf('`')) +
                "_of_" + string.Join("_", type.GenericTypeArguments.Select(FullTypeName));
        }
        else
        {
            name = name + '_' + type.Name;
        }

        return name;
    }

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
}
