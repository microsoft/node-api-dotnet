// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Supports dynamic implementation of .NET interfaces by JavaScript.
/// </summary>
internal class JSInterfaceMarshaller
{
    private readonly ConcurrentDictionary<Type, Type> _interfaceTypes = new();
    private readonly AssemblyBuilder _assemblyBuilder;
    private readonly ModuleBuilder _moduleBuilder;

    public JSInterfaceMarshaller()
    {
        string assemblyName = typeof(JSInterface).FullName +
            "_" + Environment.CurrentManagedThreadId;
        _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        _moduleBuilder =
            _assemblyBuilder.DefineDynamicModule(typeof(JSInterface).Name);
    }

    /// <summary>
    /// Defines a class type that extends <see cref="JSInterface" /> and implements the requested
    /// interface type by forwarding all member access to the JS value.
    /// </summary>
    public Type Implement(Type interfaceType, JSMarshaller marshaller)
    {
        if (_interfaceTypes.TryGetValue(interfaceType, out Type? implementationType))
        {
            return implementationType;
        }

        return BuildInterfaceImplementation(interfaceType, marshaller);
    }

    private Type BuildInterfaceImplementation(Type interfaceType, JSMarshaller marshaller)
    {
        TypeBuilder typeBuilder = _moduleBuilder.DefineType(
            "proxy_" + JSMarshaller.FullTypeName(interfaceType),
            TypeAttributes.Class | TypeAttributes.Sealed,
            typeof(JSInterface),
            new[] { interfaceType });

        // Add the type builder to the dictionary, in case interface members include a
        // (possibly indirect) reference the same interface.
        Type implementationType = _interfaceTypes.GetOrAdd(interfaceType, typeBuilder);
        if (implementationType != typeBuilder)
        {
            return implementationType;
        }

        BuildConstructorImplementation(typeBuilder);

        // A field index ensures each of the delegate fields has a unique name, in case of
        // method overloads. (The overload parameter types could be used instead, but the
        // index suffix is simpler.)
        int fieldIndex = 0;

        // Each method or property getter/setter requires a static fields holding the
        // implementation delegate. The fields can't be initialized until the type is fully built.
        FieldBuilder CreateDelegateField(MethodInfo method)
        {
            string fieldName = $"_{method.Name}_{fieldIndex++}";
            FieldBuilder fieldBuilder = typeBuilder.DefineField(
                fieldName,
                typeof(Delegate),
                requiredCustomModifiers: null,
                optionalCustomModifiers: null,
                FieldAttributes.Private | FieldAttributes.Static);
            return fieldBuilder;
        }

        IEnumerable<Type> allInterfaces =
            new[] { interfaceType }.Concat(GetInterfaces(interfaceType)).Distinct();
        IEnumerable<PropertyInfo> interfaceProperties =
            allInterfaces.SelectMany((i) => i.GetProperties())
            .Where((p) => p.GetMethod?.IsStatic != true && p.SetMethod?.IsStatic != true);
        IEnumerable<MethodInfo> interfaceMethods =
            allInterfaces.SelectMany((i) => i.GetMethods())
            .Where((m) => !m.IsStatic && !m.IsSpecialName);

        foreach (PropertyInfo? property in interfaceProperties)
        {
            FieldBuilder? getFieldBuilder = null;
            if (property.GetMethod?.IsPublic == true)
            {
                getFieldBuilder = CreateDelegateField(property.GetMethod!);
            }

            FieldBuilder? setFieldBuilder = null;
            if (property.SetMethod?.IsPublic == true)
            {
                setFieldBuilder = CreateDelegateField(property.SetMethod!);
            }

            BuildPropertyImplementation(
                typeBuilder,
                property,
                getFieldBuilder,
                setFieldBuilder,
                marshaller);
        }

        foreach (MethodInfo? method in interfaceMethods)
        {
            FieldBuilder fieldBuilder = CreateDelegateField(method);

            BuildMethodImplementation(typeBuilder, method, fieldBuilder, marshaller);
        }

        // TODO: Events

        implementationType = typeBuilder.CreateType()!;
        _interfaceTypes.TryUpdate(interfaceType, implementationType, typeBuilder);

        // Build and assign the implementation delegates after building the type.
        fieldIndex = 0;

        foreach (PropertyInfo? property in interfaceProperties)
        {
            if (property.GetMethod?.IsPublic == true)
            {
                string fieldName = $"_{property.GetMethod.Name}_{fieldIndex++}";
                FieldInfo delegateField = implementationType.GetField(
                    fieldName, BindingFlags.NonPublic | BindingFlags.Static)!;
                delegateField.SetValue(
                    null, marshaller.BuildToJSPropertyGetExpression(property).Compile());
            }

            if (property.SetMethod?.IsPublic == true)
            {
                string fieldName = $"_{property.SetMethod.Name}_{fieldIndex++}";
                FieldInfo delegateField = implementationType.GetField(
                    fieldName, BindingFlags.NonPublic | BindingFlags.Static)!;
                delegateField.SetValue(
                    null, marshaller.BuildToJSPropertySetExpression(property).Compile());
            }
        }

        foreach (MethodInfo? method in interfaceMethods)
        {
            string fieldName = $"_{method.Name}_{fieldIndex++}";

            if (method.IsGenericMethodDefinition || method.ReturnType.IsGenericTypeDefinition)
            {
                // Generic methods are not yet supported.
                continue;
            }

            FieldInfo delegateField = implementationType.GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Static)!;
            delegateField.SetValue(
                    null, marshaller.BuildToJSMethodExpression(method).Compile());
        }

        return implementationType;
    }

    private static IEnumerable<Type> GetInterfaces(Type type)
    {
        IEnumerable<Type> result = Enumerable.Empty<Type>();
        foreach (Type interfaceType in type.GetInterfaces())
        {
            result = result.Concat(new[] { interfaceType });
            result = result.Concat(GetInterfaces(interfaceType));
        }
        return result;
    }

    private static void BuildConstructorImplementation(TypeBuilder typeBuilder)
    {
        ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.HasThis,
            new[] { typeof(JSValue) });
        constructorBuilder.DefineParameter(1, ParameterAttributes.None, "value");

        ConstructorInfo baseConstructor =
            typeof(JSInterface).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                new[] { typeof(JSValue) },
                modifiers: null)!;

        ILGenerator il = constructorBuilder.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0); // this
        il.Emit(OpCodes.Ldarg_1); // JSValue value
        il.Emit(OpCodes.Call, baseConstructor);
        il.Emit(OpCodes.Ret);
    }

    private static void BuildPropertyImplementation(
        TypeBuilder typeBuilder,
        PropertyInfo property,
        FieldInfo? getDelegateField,
        FieldInfo? setDelegateField,
        JSMarshaller marshaller)
    {
        PropertyBuilder propertyBuilder = typeBuilder.DefineProperty(
            property.Name,
            PropertyAttributes.None,
            property.PropertyType,
            property.GetIndexParameters().Select((p) => p.ParameterType).ToArray());

        MethodAttributes attributes =
            MethodAttributes.Public | MethodAttributes.SpecialName |
            MethodAttributes.Virtual | MethodAttributes.Final |
            MethodAttributes.NewSlot | MethodAttributes.HideBySig;

        if (property.GetMethod != null)
        {
            ParameterInfo[] parameters = property.GetMethod.GetParameters();
            MethodBuilder getMethodBuilder = typeBuilder.DefineMethod(
                property.DeclaringType!.Name + "." + property.GetMethod.Name,
                attributes,
                CallingConventions.HasThis,
                property.GetMethod.ReturnType,
                parameters.Select((p) => p.ParameterType).ToArray());
            BuildMethodParameters(getMethodBuilder, parameters);

            ILGenerator il = getMethodBuilder.GetILGenerator();

            /*
             * return this._get_property.DynamicInvoke(new object[] { Value });
             */

            // Load the static field for the delegate that implements the method by marshalling to JS.
            il.Emit(OpCodes.Ldsfld, getDelegateField!);

            // Create an array to hold the arguments passed to the delegate invocation.
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Newarr, typeof(object));

            // Store the value from the Value property in the first array slot.
            il.Emit(OpCodes.Dup); // Duplicate the array reference on the stack.
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldarg_0); // this
            PropertyInfo valueProperty = typeof(JSInterface).GetProperty(
                "Value", BindingFlags.NonPublic | BindingFlags.Instance)!;
            il.Emit(OpCodes.Call, valueProperty.GetMethod!);
            il.Emit(OpCodes.Box, typeof(JSValue));
            il.Emit(OpCodes.Stelem_Ref);

            // Invoke the delegate.
            il.Emit(OpCodes.Callvirt, typeof(Delegate).GetMethod(nameof(Delegate.DynamicInvoke))!);

            // Return the result, casting to the return type.
            if (property.PropertyType.IsValueType)
            {
                il.Emit(OpCodes.Unbox_Any, property.PropertyType);
            }
            else
            {
                il.Emit(OpCodes.Castclass, property.PropertyType);
            }
            il.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(getMethodBuilder, property.GetMethod);
            propertyBuilder.SetGetMethod(getMethodBuilder);
        }

        if (property.SetMethod != null)
        {
            ParameterInfo[] parameters = property.SetMethod.GetParameters();
            MethodBuilder setMethodBuilder = typeBuilder.DefineMethod(
                property.DeclaringType!.Name + "." + property.SetMethod.Name,
                attributes,
                CallingConventions.HasThis,
                property.SetMethod.ReturnType,
                parameters.Select((p) => p.ParameterType).ToArray());
            BuildMethodParameters(setMethodBuilder, parameters);

            ILGenerator il = setMethodBuilder.GetILGenerator();

            /*
             * return this._set_property.DynamicInvoke(new object[] { Value, value });
             */

            // Load the static field for the delegate that implements the method by marshalling to JS.
            il.Emit(OpCodes.Ldsfld, setDelegateField!);

            // Create an array to hold the arguments passed to the delegate invocation.
            il.Emit(OpCodes.Ldc_I4_2);
            il.Emit(OpCodes.Newarr, typeof(object));

            // Store the value from the Value property in the first array slot.
            il.Emit(OpCodes.Dup); // Duplicate the array reference on the stack.
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldarg_0); // this
            PropertyInfo valueProperty = typeof(JSInterface).GetProperty(
                "Value", BindingFlags.NonPublic | BindingFlags.Instance)!;
            il.Emit(OpCodes.Call, valueProperty.GetMethod!);
            il.Emit(OpCodes.Box, typeof(JSValue));
            il.Emit(OpCodes.Stelem_Ref);

            // Store the set argument "value" in the second array slot.
            il.Emit(OpCodes.Dup); // Duplicate the array reference on the stack.
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ldarg_1); // value
            if (property.PropertyType.IsValueType) il.Emit(OpCodes.Box, property.PropertyType);
            il.Emit(OpCodes.Stelem_Ref);

            // Invoke the delegate.
            il.Emit(OpCodes.Callvirt, typeof(Delegate).GetMethod(nameof(Delegate.DynamicInvoke))!);

            // Remove unused return value from the stack.
            il.Emit(OpCodes.Pop);

            il.Emit(OpCodes.Ret);

            typeBuilder.DefineMethodOverride(setMethodBuilder, property.SetMethod);
            propertyBuilder.SetSetMethod(setMethodBuilder);
        }
    }

    private void BuildMethodImplementation(
        TypeBuilder typeBuilder,
        MethodInfo method,
        FieldInfo delegateField,
        JSMarshaller marshaller)
    {
        MethodAttributes attributes =
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final |
            MethodAttributes.NewSlot | MethodAttributes.HideBySig;
        ParameterInfo[] parameters = method.GetParameters();
        MethodBuilder methodBuilder = typeBuilder.DefineMethod(
            method.DeclaringType!.Name + "." + method.Name, // Explicit interface implementation
            attributes,
            CallingConventions.HasThis,
            method.ReturnType,
            parameters.Select((p) => p.ParameterType).ToArray());

        if (method.IsGenericMethodDefinition)
        {
            methodBuilder.DefineGenericParameters(
                method.GetGenericArguments().Select((t) => t.Name).ToArray());
        }

        BuildMethodParameters(methodBuilder, parameters);

        ILGenerator il = methodBuilder.GetILGenerator();

        // TODO: Consider defining delegate types as needed for method signatures so the
        // delegate invocations are not "dynamic". It would avoid boxing value types.

        /*
         * return this._method.DynamicInvoke(new object[] { Value, args... });
         */

        // Load the static field for the delegate that implements the method by marshalling to JS.
        il.Emit(OpCodes.Ldsfld, delegateField);

        // Create an array to hold the arguments passed to the delegate invocation.
        il.Emit(OpCodes.Ldc_I4, 1 + parameters.Length);
        il.Emit(OpCodes.Newarr, typeof(object));

        // Store the value from the Value property in the first array slot.
        il.Emit(OpCodes.Dup); // Duplicate the array reference on the stack.
        il.Emit(OpCodes.Ldc_I4_0);
        il.Emit(OpCodes.Ldarg_0); // this
        PropertyInfo valueProperty = typeof(JSInterface).GetProperty(
            "Value", BindingFlags.NonPublic | BindingFlags.Instance)!;
        il.Emit(OpCodes.Call, valueProperty.GetMethod!);
        il.Emit(OpCodes.Box, typeof(JSValue));
        il.Emit(OpCodes.Stelem_Ref);

        // Store the arguments in the remaining array slots.
        for (int i = 0; i < parameters.Length; i++)
        {
            il.Emit(OpCodes.Dup);  // Duplicate the array reference on the stack.
            il.Emit(OpCodes.Ldc_I4, 1 + i);
            il.Emit(OpCodes.Ldarg, 1 + i);

            if (parameters[i].ParameterType.IsValueType)
            {
                il.Emit(OpCodes.Box, parameters[i].ParameterType);
            }

            il.Emit(OpCodes.Stelem_Ref);
        }

        // Invoke the delegate.
        il.Emit(OpCodes.Callvirt, typeof(Delegate).GetMethod(nameof(Delegate.DynamicInvoke))!);

        // Return the result, casting to the return type if necessary.
        if (method.ReturnType == typeof(void))
        {
            // Remove unused return value from the stack.
            il.Emit(OpCodes.Pop);
        }
        else if (method.ReturnType.IsValueType)
        {
            il.Emit(OpCodes.Unbox_Any, method.ReturnType);
        }
        else
        {
            il.Emit(OpCodes.Castclass, method.ReturnType);
        }
        il.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(methodBuilder, method);
    }

    private static void BuildMethodParameters(
        MethodBuilder methodBuilder, ParameterInfo[] parameters)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            // Position here is offset by one because the return parameter is at 0.
            methodBuilder.DefineParameter(i + 1, parameters[i].Attributes, parameters[i].Name);
        }
    }
}
