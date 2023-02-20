using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace NodeApi.Hosting;

using static ManagedHost;

[RequiresUnreferencedCode("Managed host is not used in trimmed assembly.")]
[RequiresDynamicCode("Managed host is not used in trimmed assembly.")]
internal class AssemblyExporter
{
    private readonly JSReference _assemblyObject;
    private readonly Dictionary<Type, JSReference> _typeObjects = new();
    private readonly AssemblyBuilder _assemblyBuilder;
    private readonly ModuleBuilder _moduleBuilder;

    public AssemblyExporter(Assembly assembly)
    {
        Assembly = assembly;
        _assemblyObject = new JSReference(ExportAssembly());
        _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("JS." + assembly.FullName),
            AssemblyBuilderAccess.Run);
        _moduleBuilder = _assemblyBuilder.DefineDynamicModule("JS");
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

        TypeBuilder typeBuilder = _moduleBuilder.DefineType(classType.FullName!.Replace(".", "_"));
        List<JSPropertyDescriptor> classProperties = new();

        foreach (var member in classType.GetMembers(BindingFlags.Public | BindingFlags.Static))
        {
            if (member is MethodInfo method && !method.IsSpecialName)
            {
                EmitStaticMethod(typeBuilder, method);
            }
            else if (member is PropertyInfo property)
            {
                if (property.GetMethod?.IsPublic == true)
                {
                    EmitStaticPropertyGet(typeBuilder, property);
                }
                if (property.SetMethod?.IsPublic == true)
                {
                    EmitStaticPropertySet(typeBuilder, property);
                }
            }
        }

        Type builtType = typeBuilder.CreateType();

        var attributes = JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;
        foreach (var member in classType.GetMembers(BindingFlags.Public | BindingFlags.Static))
        {
            if (member is MethodInfo method && !method.IsSpecialName)
            {
                MethodInfo builtMethod = builtType.GetMethod(method.Name)!;
                var methodDelegate = (JSCallback)builtMethod.CreateDelegate(typeof(JSCallback));
                classProperties.Add(JSPropertyDescriptor.Function(
                    member.Name,
                    methodDelegate,
                    attributes));
            }
            else if (member is PropertyInfo property)
            {
                /*
                JSCallback? getterDelegate = null;
                if (property.GetMethod != null)
                {
                    MethodInfo builtGetMethod = builtType.GetMethod(property.GetMethod.Name)!;
                    getterDelegate = (JSCallback)builtGetMethod.CreateDelegate(typeof(JSCallback));
                }

                JSCallback? setterDelegate = null;
                if (property.SetMethod != null)
                {
                    MethodInfo builtSetMethod = builtType.GetMethod(property.SetMethod.Name)!;
                    setterDelegate = (JSCallback)builtSetMethod.CreateDelegate(typeof(JSCallback));
                }

                classProperties.Add(JSPropertyDescriptor.Accessor(
                    member.Name,
                    getterDelegate,
                    setterDelegate,
                    attributes));
                */
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

        // TODO: Emit static and instance property and method adapter methods,
        // then define the class.

        return new JSObject();
    }

    private JSValue ExportStruct(Type structType)
    {
        if (_typeObjects.TryGetValue(structType, out JSReference? typeObjectReference))
        {
            return typeObjectReference!.GetValue()!.Value;
        }

        // TODO: Export struct proeprties and methods.

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

    private void EmitStaticMethod(TypeBuilder typeBuilder, MethodInfo method)
    {
        MethodBuilder methodBuilder = typeBuilder.DefineMethod(
            method.Name,
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.Final,
            returnType: typeof(JSValue),
            parameterTypes: new[] { typeof(JSCallbackArgs) });
        ILGenerator il = methodBuilder.GetILGenerator();

        /*
         * private static JSValue MethodClass_MethodName(JSCallbackArgs __args)
         * {
         *     var param0Name = (Param0Type)__args[0];
         *     ...
         *     var __result = MethodClass.MethodName(param0, ...);
         *     return (JSValue)__result;
         * }
         */

        var parameters = method.GetParameters();
        for (int i = 0; i < parameters.Length; i++)
        {
            il.DeclareLocal(parameters[i].ParameterType);
        }

        il.Emit(OpCodes.Nop);
        for (int i = 0; i < parameters.Length; i++)
        {
            EmitLoadAndConvertArg(il, argumentIndex: i, parameters[i].ParameterType, localIndex: i);
        }

        // Load all the local vars onto the stack.
        for (int i = 0; i < parameters.Length; i++)
        {
            il.Emit(OpCodes.Ldloc, i);
        }
        il.Emit(OpCodes.Call, method); // Call the target (static) method!

        EmitConvertAndReturnResult(il, method.ReturnType);
    }

    private void EmitStaticPropertyGet(TypeBuilder typeBuilder, PropertyInfo property)
    {
        // TODO
    }

    private void EmitStaticPropertySet(TypeBuilder typeBuilder, PropertyInfo property)
    {
        // TODO
    }

    private static void EmitLoadAndConvertArg(
        ILGenerator il,
        int argumentIndex,
        Type parameterType,
        int localIndex)
    {
        il.Emit(OpCodes.Ldarg_0);  // Callback args
        il.Emit(OpCodes.Ldc_I4, argumentIndex);

        // This is a 'callvirt' opcode instead of 'call' because it's a class instance method.
        // If JSCallbackArgs changes to a struct, this should be 'call'.
        Debug.Assert(!typeof(JSCallbackArgs).IsValueType);
        il.Emit(OpCodes.Callvirt, s_getCallbackArg); // Get args[i]

        EmitConvertTo(il, parameterType); // Convert from JSValue to arg type
        il.Emit(OpCodes.Stloc, localIndex); // Store result in local var [i]
    }

    private static void EmitConvertAndReturnResult(ILGenerator il, Type returnType)
    {
        if (returnType == typeof(void))
        {
            il.Emit(OpCodes.Call, s_getUndefined);
        }
        else
        {
            EmitConvertFrom(il, returnType);
        }

        il.Emit(OpCodes.Ret);
    }

    private static void EmitConvertTo(ILGenerator il, Type toType)
    {
        EmitCastTo(il, toType);

        // TODO: Emit code for more conversions beyond simple casts.
    }

    private static void EmitCastTo(ILGenerator il, Type toType)
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

        il.Emit(OpCodes.Call, castMethod);
    }

    private static void EmitConvertFrom(ILGenerator il, Type fromType)
    {
        EmitCastFrom(il, fromType);

        // TODO: Emit code for more conversions beyond simple casts.
    }

    private static void EmitCastFrom(ILGenerator il, Type fromType)
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

        il.Emit(OpCodes.Call, castMethod);
    }

    private static readonly MethodInfo s_getUndefined =
        typeof(JSValue).GetMethod("get_Undefined")!;

    private static readonly MethodInfo s_getCallbackArg =
        typeof(JSCallbackArgs).GetMethod("get_Item")!;
}
