// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Supports dynamic implementation of .NET interfaces by JavaScript.
/// </summary>
internal class JSInterfaceMarshaller
{
    private static readonly MethodInfo s_jsInterfaceDynamicInvoke = typeof(JSInterface).GetMethod(
        nameof(Delegate.DynamicInvoke),
        BindingFlags.NonPublic | BindingFlags.Instance,
        binder: null,
        new[] { typeof(Delegate), typeof(object?[]) },
        modifiers: null)!;

    private readonly ConcurrentDictionary<Type, Type> _interfaceTypes = new();
    private readonly AssemblyBuilder _assemblyBuilder;
    private readonly ModuleBuilder _moduleBuilder;

    public JSInterfaceMarshaller()
    {
        _assemblyBuilder = JSMarshaller.CreateAssemblyBuilder(typeof(JSInterfaceMarshaller));
        _moduleBuilder = _assemblyBuilder.DefineDynamicModule(typeof(JSInterfaceMarshaller).Name);
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
        IEnumerable<EventInfo> interfaceEvents =
            allInterfaces.SelectMany((i) => i.GetEvents())
            .Where((e) => e.AddMethod?.IsStatic != true && e.RemoveMethod?.IsStatic != true);

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
                setFieldBuilder);
        }

        foreach (MethodInfo method in interfaceMethods)
        {
            FieldBuilder fieldBuilder = CreateDelegateField(method);

            BuildMethodImplementation(typeBuilder, method, fieldBuilder);
        }

        foreach (EventInfo eventInfo in interfaceEvents)
        {
            BuildEventImplementation(typeBuilder, eventInfo);
        }

        implementationType = typeBuilder.CreateTypeInfo()!;
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

            if (method.IsGenericMethodDefinition)
            {
                // The delegate field is not used for generic methods.
                continue;
            }

            FieldInfo delegateField = implementationType.GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Static)!;
            delegateField.SetValue(null, marshaller.GetToJSMethodDelegate(method));
        }

        return implementationType;
    }

    private static IEnumerable<Type> GetInterfaces(Type type)
    {
        IEnumerable<Type> result = [];
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
        FieldInfo? setDelegateField)
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
             * return this.DynamicInvoke(_get_property, new object[] { Value });
             */

            il.Emit(OpCodes.Ldarg_0); // this

            // Load the static field for the delegate that implements the method by marshalling to JS.
            il.Emit(OpCodes.Ldsfld, getDelegateField!);

            // Create an array to hold the arguments passed to the delegate invocation.
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Newarr, typeof(object));

            // Invoke the delegate.
            il.Emit(OpCodes.Callvirt, s_jsInterfaceDynamicInvoke);

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
             * return this.DynamicInvoke(_set_property, new object[] { Value, value });
             */

            il.Emit(OpCodes.Ldarg_0); // this

            // Load the static field for the delegate that implements the method by marshalling to JS.
            il.Emit(OpCodes.Ldsfld, setDelegateField!);

            // Create an array to hold the arguments passed to the delegate invocation.
            il.Emit(OpCodes.Ldc_I4_2);
            il.Emit(OpCodes.Newarr, typeof(object));

            // Store the set argument "value" in the second array slot.
            il.Emit(OpCodes.Dup); // Duplicate the array reference on the stack.
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ldarg_1); // value
            if (property.PropertyType.IsValueType) il.Emit(OpCodes.Box, property.PropertyType);
            il.Emit(OpCodes.Stelem_Ref);

            // Invoke the delegate.
            il.Emit(OpCodes.Callvirt, s_jsInterfaceDynamicInvoke);

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
        FieldInfo delegateField)
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

        void EmitArgs()
        {
            // Create an array to hold the arguments passed to the delegate invocation.
            il.Emit(OpCodes.Ldc_I4, 1 + parameters.Length);
            il.Emit(OpCodes.Newarr, typeof(object));

            // Store the arguments in the array, leaving index 0 for the JS `this` value.
            for (int i = 0; i < parameters.Length; i++)
            {
                il.Emit(OpCodes.Dup);  // Duplicate the array reference on the stack.
                il.Emit(OpCodes.Ldc_I4, 1 + i);
                il.Emit(OpCodes.Ldarg, 1 + i);

                if (parameters[i].ParameterType.IsValueType ||
                    parameters[i].ParameterType.IsGenericParameter)
                {
                    il.Emit(OpCodes.Box, parameters[i].ParameterType);
                }

                il.Emit(OpCodes.Stelem_Ref);
            }
        }

        il.Emit(OpCodes.Ldarg_0); // this

        if (method.IsGenericMethodDefinition)
        {
            /*
             * return this.DynamicInvoke(
                   MethodBase.GetCurrentMethod().MakeGenericMethod(new Type[] { typeArgs... }),
                   JSMarshaller.StaticGetToJSMethodDelegate,
                   new object[] { Value, args... });
             */

            il.Emit(
                OpCodes.Call,
                typeof(MethodBase).GetStaticMethod(nameof(MethodBase.GetCurrentMethod)));
            il.Emit(OpCodes.Castclass, typeof(MethodInfo));

            // Create an array to hold the type arguments used to make the generic method.
            Type[] typeArgs = method.GetGenericArguments();
            il.Emit(OpCodes.Ldc_I4, typeArgs.Length);
            il.Emit(OpCodes.Newarr, typeof(Type));

            for (int i = 0; i < typeArgs.Length; i++)
            {
                il.Emit(OpCodes.Dup);  // Duplicate the array reference on the stack.
                il.Emit(OpCodes.Ldc_I4, i);
                il.Emit(OpCodes.Ldtoken, typeArgs[i]);
                il.Emit(OpCodes.Call, typeof(Type).GetStaticMethod(nameof(Type.GetTypeFromHandle)));
                il.Emit(OpCodes.Stelem_Ref);
            }

            // Make a generic method from the generic method definition.
            il.Emit(OpCodes.Callvirt,
                typeof(MethodInfo).GetInstanceMethod(nameof(MethodInfo.MakeGenericMethod)));

            // Load the delegate provider callback. The callback will be invoked on the JS thread,
            // which is necessary because it uses the thread-local JSMarshaller instance.
            il.Emit(OpCodes.Ldnull);
            il.Emit(
                OpCodes.Ldftn,
                typeof(JSMarshaller).GetStaticMethod(
                    nameof(JSMarshaller.StaticGetToJSMethodDelegate)));
            il.Emit(OpCodes.Newobj,
                typeof(Func<MethodInfo, Delegate>)
                    .GetConstructor(new[] { typeof(object), typeof(nint) })!);

            EmitArgs();

            // Invoke the delegate.
            il.Emit(OpCodes.Callvirt, typeof(JSInterface).GetMethod(
                nameof(Delegate.DynamicInvoke),
                BindingFlags.NonPublic | BindingFlags.Instance,
                binder: null,
                new[] { typeof(MethodInfo), typeof(Func<MethodInfo, Delegate>), typeof(object?[]) },
                modifiers: null)!);
        }
        else
        {
            /*
             * return this.DynamicInvoke(_method, new object[] { Value, args... });
             */

            // TODO: Consider defining delegate types as needed for method signatures so the
            // delegate invocations are not "dynamic". It would avoid boxing value types.

            // Load the static field for the delegate that implements the method by marshalling
            // to JS.
            il.Emit(OpCodes.Ldsfld, delegateField);

            EmitArgs();

            // Invoke the delegate.
            il.Emit(OpCodes.Callvirt, s_jsInterfaceDynamicInvoke);
        }

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

    private static void BuildEventImplementation(
        TypeBuilder typeBuilder,
        EventInfo eventInfo)
    {
        EventBuilder eventBuilder = typeBuilder.DefineEvent(
            eventInfo.DeclaringType!.Name + ".add_" + eventInfo.Name, // Explicit interface impl
            EventAttributes.None,
            eventInfo.EventHandlerType!);

        MethodAttributes attributes =
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final |
            MethodAttributes.NewSlot | MethodAttributes.HideBySig | MethodAttributes.SpecialName;

        if (eventInfo.AddMethod != null)
        {
            MethodBuilder addMethodBuilder = typeBuilder.DefineMethod(
                eventInfo.DeclaringType!.Name + ".add_" + eventInfo.Name, // Explicit interface impl
                attributes,
                CallingConventions.HasThis,
                returnType: typeof(void),
                parameterTypes: new[] { eventInfo.EventHandlerType! });
            addMethodBuilder.DefineParameter(1, ParameterAttributes.None, "value");

            ILGenerator il = addMethodBuilder.GetILGenerator();

            // TODO: Implement event add method.
            il.Emit(OpCodes.Ret);

            eventBuilder.SetAddOnMethod(addMethodBuilder);
            typeBuilder.DefineMethodOverride(addMethodBuilder, eventInfo.AddMethod);
        }

        if (eventInfo.RemoveMethod != null)
        {
            MethodBuilder removeMethodBuilder = typeBuilder.DefineMethod(
                eventInfo.DeclaringType!.Name + ".remove_" + eventInfo.Name, // Explicit interface impl
                attributes,
                CallingConventions.HasThis,
                returnType: typeof(void),
                parameterTypes: new[] { eventInfo.EventHandlerType! });
            removeMethodBuilder.DefineParameter(1, ParameterAttributes.None, "value");

            ILGenerator il2 = removeMethodBuilder.GetILGenerator();

            // TODO: Implement event remove method.
            il2.Emit(OpCodes.Ret);

            eventBuilder.SetRemoveOnMethod(removeMethodBuilder);
            typeBuilder.DefineMethodOverride(removeMethodBuilder, eventInfo.RemoveMethod);
        }
    }
}
