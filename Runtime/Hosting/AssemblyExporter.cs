using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace NodeApi.Hosting;

using static ManagedHost;

[RequiresUnreferencedCode("Managed host is not used in trimmed assembly.")]
[RequiresDynamicCode("Managed host is not used in trimmed assembly.")]
internal class AssemblyExporter
{
    private readonly JSReference _assemblyObject;
    private readonly Dictionary<Type, JSReference> _typeObjects = new();

    public AssemblyExporter(Assembly assembly)
    {
        Assembly = assembly;
        _assemblyObject = new JSReference(ExportAssembly());
    }

    public Assembly Assembly { get; }

    public JSValue AssemblyObject => _assemblyObject.GetValue()!.Value;

    private JSValue ExportAssembly()
    {
        Trace($"> AssemblyExporter.ExportAssembly({Assembly.FullName})");

        // Reflect over public types in the loaded assembly.
        // Define a property for each type, with a getter that lazily defines the class.

        List<JSPropertyDescriptor> assemblyProperties = new();
        void AddAssemblyTypeProperty(Type type, Func<Type, JSValue> getter)
        {
            assemblyProperties.Add(JSPropertyDescriptor.Accessor(
                type.FullName!,
                (_) => getter(type),
                setter: null,
                JSPropertyAttributes.Enumerable));
        }

        foreach (var type in Assembly.GetTypes().Where((t) => t.IsPublic))
        {
            if (type.IsClass)
            {
                if (type.IsAbstract && type.IsSealed) // static class
                {
                    AddAssemblyTypeProperty(type, ExportStaticClass);
                }
                else
                {
                    AddAssemblyTypeProperty(type, ExportClass);
                }
            }
            else if (type.IsValueType)
            {
                if (type.IsEnum)
                {
                    AddAssemblyTypeProperty(type, ExportEnum);
                }
                else
                {
                    AddAssemblyTypeProperty(type, ExportStruct);
                }
            }
        }

        JSObject assemblyObject = new JSObject();
        assemblyObject.DefineProperties(assemblyProperties);

        Trace($"< AssemblyExporter.ExportAssembly() => [{assemblyProperties.Count}]");
        return assemblyObject;
    }

    private JSValue ExportStaticClass(Type classType)
    {
        Trace($"> AssemblyExporter.ExportStaticClass({classType.FullName})");

        if (_typeObjects.TryGetValue(classType, out JSReference? typeObjectReference))
        {
            Trace($"< AssemblyExporter.ExportStaticClass() => already exported");
            return typeObjectReference!.GetValue()!.Value;
        }

        List<JSPropertyDescriptor> classProperties = new();

        var attributes = JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;
        foreach (var member in classType.GetMembers(BindingFlags.Public | BindingFlags.Static))
        {
            if (member is MethodInfo method && !method.IsSpecialName)
            {
                LambdaExpression lambda = BuildStaticMethodLambda(method);
                JSCallback methodDelegate = (JSCallback)lambda.Compile();
                classProperties.Add(JSPropertyDescriptor.Function(
                    member.Name,
                    methodDelegate,
                    attributes));
            }
            else if (member is PropertyInfo property)
            {
                JSCallback? getterDelegate = null;
                if (property.GetMethod != null)
                {
                    LambdaExpression lambda = BuildStaticPropertyGetLambda(property);
                    getterDelegate = (JSCallback)lambda.Compile();
                }

                JSCallback? setterDelegate = null;
                if (property.SetMethod != null)
                {
                    LambdaExpression lambda = BuildStaticPropertySetLambda(property);
                    setterDelegate = (JSCallback)lambda.Compile();
                }

                classProperties.Add(JSPropertyDescriptor.Accessor(
                    member.Name,
                    getterDelegate,
                    setterDelegate,
                    attributes));
            }
        }

        JSObject staticClassObject = new();
        staticClassObject.DefineProperties(classProperties);

        Trace($"< AssemblyExporter.ExportStaticClass() => [{classProperties.Count}]");
        return staticClassObject;
    }

    private JSValue ExportClass(Type classType)
    {
        if (_typeObjects.TryGetValue(classType, out JSReference? typeObjectReference))
        {
            return typeObjectReference!.GetValue()!.Value;
        }

        // TODO: Build static and instance property and method adapter methods,
        // then define the class.

        return new JSObject();
    }

    private JSValue ExportStruct(Type structType)
    {
        if (_typeObjects.TryGetValue(structType, out JSReference? typeObjectReference))
        {
            return typeObjectReference!.GetValue()!.Value;
        }

        // TODO: Build struct proeprties and methods.

        return new JSObject();
    }

    private JSValue ExportEnum(Type enumType)
    {
        if (_typeObjects.TryGetValue(enumType, out JSReference? typeObjectReference))
        {
            return typeObjectReference!.GetValue()!.Value;
        }

        // TODO: Export enum values as properties on an object.

        return new JSObject();
    }

    private LambdaExpression BuildStaticMethodLambda(MethodInfo method)
    {
        /*
         * private static JSValue MethodClass_MethodName(JSCallbackArgs __args)
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
            argVariables.Add(Expression.Parameter(parameters[i].ParameterType, parameters[i].Name));
            statements.Add(Expression.Assign(argVariables[i],
                BuildArgumentExpression(s_argsParameter, i, parameters[i].ParameterType)));
        }

        if (method.ReturnType == typeof(void))
        {
            variables = argVariables;
            statements.Add(Expression.Call(instance: null, method, argVariables));
            statements.Add(Expression.Property(null, s_getUndefined));
        }
        else
        {
            ParameterExpression resultVariable =
                Expression.Parameter(method.ReturnType, "__result");
            variables = argVariables.Append(resultVariable);
            statements.Add(Expression.Assign(resultVariable,
                Expression.Call(instance: null, method, argVariables)));
            statements.Add(BuildResultExpression(resultVariable, method.ReturnType));
        }

        return Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables, statements),
            name: method.Name,
            parameters: s_argsArray);
    }

    private LambdaExpression BuildStaticPropertyGetLambda(PropertyInfo property)
    {
        /*
         * private static JSValue get_PropertyClass_PropertyName(JSCallbackArgs __args)
         * {
         *     var __result = PropertyClass.PropertyName;
         *     return (JSValue)__result;
         * }
         */

        List<Expression> statements = new(2);
        ParameterExpression resultVariable =
            Expression.Parameter(property.PropertyType, "__result");
        IEnumerable<ParameterExpression> variables = new[] { resultVariable };
        statements.Add(Expression.Assign(resultVariable,
            Expression.Property(null, property)));
        statements.Add(BuildResultExpression(resultVariable, property.PropertyType));

        return Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables, statements),
            name: property.GetMethod!.Name,
            parameters: s_argsArray);
    }

    private LambdaExpression BuildStaticPropertySetLambda(PropertyInfo property)
    {
        /*
         * private static JSValue get_PropertyClass_PropertyName(JSCallbackArgs __args)
         * {
         *     var __value = (PropertyType)__args[0];
         *     __obj.PropertyName = __value;
         *     return JSValue.Undefined;
         * }
         */

        List<Expression> statements = new(3);
        ParameterExpression valueVariable = Expression.Parameter(property.PropertyType, "__value");
        IEnumerable<ParameterExpression> variables = new[] { valueVariable };
        statements.Add(Expression.Assign(valueVariable,
                BuildArgumentExpression(s_argsParameter, 0, property.PropertyType)));
        statements.Add(Expression.Call(null, property.SetMethod!, valueVariable));
        statements.Add(Expression.Call(null, s_getUndefined));

        return Expression.Lambda(
            delegateType: typeof(JSCallback),
            body: Expression.Block(typeof(JSValue), variables, statements),
            name: property.SetMethod!.Name,
            parameters: s_argsArray);
    }

    private static Expression BuildArgumentExpression(
        ParameterExpression args,
        int index,
        Type parameterType)
    {
        // TODO: Build expressions for more conversions beyond simple casts.

        return Expression.Call(instance: null, GetCastFromJSValueMethod(parameterType),
                Expression.Call(args, s_getCallbackArg, Expression.Constant(index)));
    }

    private static Expression BuildResultExpression(
        ParameterExpression resultVariable,
        Type resultType)
    {
        // TODO: Build expressions for more conversions beyond simple casts.

        return Expression.Call(instance: null, GetCastToJSValueMethod(resultType), resultVariable);
    }

    private static MethodInfo GetCastFromJSValueMethod(Type toType)
    {
        MethodInfo? castMethod =
            typeof(JSValue).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where((m) => m.Name == "op_Explicit" && m.ReturnType == toType &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == typeof(JSValue))
            .SingleOrDefault();
        if (castMethod == null)
        {
            throw new NotSupportedException($"Cannot cast to {toType.Name} from JSValue.");
        }
        return castMethod;
    }

    private static MethodInfo GetCastToJSValueMethod(Type fromType)
    {
        MethodInfo? castMethod =
            typeof(JSValue).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where((m) => m.Name == "op_Implicit" && m.ReturnType == typeof(JSValue) &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == fromType)
            .SingleOrDefault();
        if (castMethod == null)
        {
            throw new NotSupportedException($"Cannot cast from {fromType.Name} to JSValue.");
        }
        return castMethod;
    }

    private static readonly ParameterExpression s_argsParameter =
        Expression.Parameter(typeof(JSCallbackArgs), "__args");
    private static readonly IEnumerable<ParameterExpression> s_argsArray =
        new[] { s_argsParameter };

    private static readonly MethodInfo s_getUndefined =
        typeof(JSValue).GetMethod("get_Undefined")!;

    private static readonly MethodInfo s_getCallbackArg =
        typeof(JSCallbackArgs).GetMethod("get_Item")!;
}
