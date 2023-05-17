// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;

using static Microsoft.JavaScript.NodeApi.DotNetHost.ManagedHost;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Dynamically exports .NET types to JS.
/// </summary>
internal class TypeExporter
{
    private readonly IDictionary<Type, JSReference> _exportedTypes;
    private readonly JSMarshaller _marshaller;

    /// <summary>
    /// Creates a new instance of the <see cref="TypeExporter" /> class.
    /// </summary>
    /// <param name="exportedTypes">Mapping from .NET types to exported JS types. Used to
    /// ensure related types are not exported multiple times.</param>
    public TypeExporter(
        IDictionary<Type, JSReference> exportedTypes)
    {
        _marshaller = JSMarshaller.Current;
        _exportedTypes = exportedTypes;
    }

    /// <summary>
    /// Attempts to project a .NET type as a JS object.
    /// </summary>
    /// <param name="type">A type to export.</param>
    /// <returns>A strong reference to a JS object that represents the exported type, or null
    /// if the type could not be exported.</returns>
    public JSReference? TryExportType(Type type)
    {
        // TODO: Handle generic types.

        JSReference? valueReference;
        try
        {
            if (!IsSupportedType(type))
            {
                Trace($"      Unsupported type: {type}");
                return null;
            }
            else if (type.IsEnum)
            {
                valueReference = ExportEnum(type);
            }
            else if (type.IsClass || type.IsInterface || type.IsValueType)
            {
                if (type.IsClass && type.BaseType?.FullName == typeof(MulticastDelegate).FullName)
                {
                    // Delegate types are not exported as type objects, but the JS marshaller can
                    // still dynamically convert delegate instances to/from JS functions.
                    Trace($"      Delegate types are not exported.");
                    return null;
                }
                else
                {
                    valueReference = ExportClass(type);
                }
            }
            else
            {
                Trace($"      Unknown type kind: {type}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Trace($"Failed to export type {type}: {ex}");
            return null;
        }

        return valueReference;
    }

    private JSReference? ExportClass(Type type)
    {
        if (_exportedTypes.TryGetValue(type, out JSReference? classObjectReference))
        {
            return classObjectReference;
        }

        if (type == typeof(object) || type == typeof(string) ||
            type == typeof(void) || type.IsPrimitive)
        {
            return default;
        }

        Trace($"> {nameof(TypeExporter)}.ExportClass({type.FullName})");

        bool isStatic = type.IsAbstract && type.IsSealed;
        Type classBuilderType =
            (type.IsValueType ? typeof(JSStructBuilder<>) : typeof(JSClassBuilder<>))
            .MakeGenericType(isStatic ? typeof(object) : type);

        object classBuilder;
        if (type.IsInterface || isStatic || type.IsValueType)
        {
            classBuilder = classBuilderType.CreateInstance(
                new[] { typeof(string) }, new[] { type.Name });
        }
        else
        {
            ConstructorInfo[] constructors =
                type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .Where(IsSupportedConstructor)
                .ToArray();
            JSCallbackDescriptor constructorDescriptor;
            if (constructors.Length == 1 &&
                !constructors[0].GetParameters().Any((p) => p.IsOptional))
            {
                constructorDescriptor =
                    _marshaller.BuildFromJSConstructorExpression(constructors[0]).Compile();
            }
            else
            {
                // Multiple constructors or optional parameters require overload resolution.
                constructorDescriptor =
                    _marshaller.BuildConstructorOverloadDescriptor(constructors);
            }

            classBuilder = classBuilderType.CreateInstance(
                new[] { typeof(string), typeof(JSCallbackDescriptor) },
                new object[] { type.Name, constructorDescriptor });
        }

        ExportProperties(type, classBuilder);
        ExportMethods(type, classBuilder);
        ExportNestedTypes(type, classBuilder);

        string defineMethodName = type.IsInterface ? "DefineInterface" :
            isStatic ? "DefineStaticClass" : type.IsValueType ? "DefineStruct" : "DefineClass";
        MethodInfo defineClassMethod = classBuilderType.GetInstanceMethod(defineMethodName);
        JSValue classObject = (JSValue)defineClassMethod.Invoke(
            classBuilder,
            defineClassMethod.GetParameters().Select((_) => (object?)null).ToArray())!;

        classObjectReference = new JSReference(classObject);
        _exportedTypes.Add(type, classObjectReference);

        // Also export any types returned by properties or methods of this type, because
        // they might otherwise not be referenced by JS before they are used.
        ExportClassDependencies(type);

        Trace($"< {nameof(TypeExporter)}.ExportClass()");
        return classObjectReference;
    }

    private void ExportClassDependencies(Type type)
    {
        void ExportTypeIfSupported(Type dependencyType)
        {
            if (dependencyType.IsArray || dependencyType.IsByRef)
            {
                ExportTypeIfSupported(dependencyType.GetElementType()!);
                return;
            }
            else if (dependencyType.IsGenericType)
            {
                Type genericTypeDefinition = dependencyType.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Nullable<>) ||
                    genericTypeDefinition.Namespace == typeof(IList<>).Namespace)
                {
                    foreach (Type typeArg in dependencyType.GetGenericArguments())
                    {
                        ExportTypeIfSupported(typeArg);
                    }
                    return;
                }
            }

            if (IsSupportedType(dependencyType))
            {
                TryExportType(dependencyType);
            }
        }

        foreach (MemberInfo member in type.GetMembers
            (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            if (member is PropertyInfo property &&
                !JSMarshaller.IsConvertedType(property.PropertyType))
            {
                ExportTypeIfSupported(property.PropertyType);
            }
            else if (member is MethodInfo method &&
                IsSupportedMethod(method) &&
                !JSMarshaller.IsConvertedType(method.ReturnType))
            {
                ExportTypeIfSupported(method.ReturnType);
            }
            else if (member is MethodInfo interfaceMethod && type.IsInterface)
            {
                // Interface method parameter types must be exported in case the interface
                // will be implemented by JS.
                foreach (ParameterInfo interfaceMethodParameter in interfaceMethod.GetParameters())
                {
                    Type parameterType = interfaceMethodParameter.ParameterType;
#if !NETFRAMEWORK // TODO: Find an alternative for .NET Framework.
                    if (!parameterType.IsGenericMethodParameter)
#endif
                    {
                        ExportTypeIfSupported(parameterType);
                    }
                }
            }
        }
    }

    private void ExportProperties(Type type, object classBuilder)
    {
        Type classBuilderType = classBuilder.GetType();
        MethodInfo? addValuePropertyMethod = classBuilderType.GetInstanceMethod(
            "AddProperty", new[] { typeof(string), typeof(JSPropertyAttributes) });
        MethodInfo addPropertyMethod = classBuilderType.GetInstanceMethod(
            "AddProperty",
            new[]
            {
                typeof(string),
                typeof(JSCallback),
                typeof(JSCallback),
                typeof(JSPropertyAttributes),
                typeof(object),
            });

        JSPropertyAttributes attributes =
            JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;
        bool isStatic = type.IsAbstract && type.IsSealed;

        foreach (PropertyInfo property in type.GetProperties(
            BindingFlags.Public | BindingFlags.Static |
            (isStatic ? default : BindingFlags.Instance)))
        {
            if (!IsSupportedType(property.PropertyType))
            {
                continue;
            }

            JSPropertyAttributes propertyAttributes = attributes;
            bool isStaticProperty = property.GetMethod?.IsStatic == true ||
                property.SetMethod?.IsStatic == true;

            Trace($"    {(isStaticProperty ? "static " : string.Empty)}{property.Name}");

            if (type.IsValueType && !isStaticProperty)
            {
                // Struct instance properties are not backed by getter/setter methods. The
                // entire struct is always passed by value. Properties are converted to/from
                // `JSValue` by the struct adapter method.
                propertyAttributes |= JSPropertyAttributes.Writable;
                addValuePropertyMethod.Invoke(
                    classBuilder,
                    new object[] { property.Name, propertyAttributes });
            }
            else
            {
                if (isStaticProperty)
                {
                    propertyAttributes |= JSPropertyAttributes.Static;
                }

                if (property.SetMethod != null)
                {
                    propertyAttributes |= JSPropertyAttributes.Writable;
                }

                JSCallback? getterDelegate = null;
                if (property.GetMethod != null)
                {
                    LambdaExpression lambda =
                        _marshaller.BuildFromJSPropertyGetExpression(property);
                    getterDelegate = (JSCallback)lambda.Compile();
                }

                JSCallback? setterDelegate = null;
                if (property.SetMethod != null)
                {
                    LambdaExpression lambda =
                        _marshaller.BuildFromJSPropertySetExpression(property);
                    setterDelegate = (JSCallback)lambda.Compile();
                }

                addPropertyMethod.Invoke(
                    classBuilder,
                    new object?[]
                    {
                        property.Name,
                        getterDelegate,
                        setterDelegate,
                        propertyAttributes,
                        null,
                    });
            }
        }
    }

    private void ExportMethods(Type type, object classBuilder)
    {
        Type classBuilderType = classBuilder.GetType();
        MethodInfo addMethodMethod = classBuilderType.GetInstanceMethod(
            "AddMethod",
            new[]
            {
                typeof(string),
                typeof(JSCallback),
                typeof(JSPropertyAttributes),
                typeof(object),
            });

        JSPropertyAttributes attributes =
            JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;
        bool isStatic = type.IsAbstract && type.IsSealed;

        foreach (IGrouping<(bool IsStatic, string Name), MethodInfo> methodGroup in type.GetMethods(
            BindingFlags.Public | BindingFlags.Static |
            (isStatic ? default : BindingFlags.Instance))
            .Where((m) => !m.IsSpecialName)
            .GroupBy((m) => (m.IsStatic, m.Name)))
        {
            bool methodIsStatic = methodGroup.Key.IsStatic;
            string methodName = methodGroup.Key.Name;
            MethodInfo[] methods = methodGroup.Where(IsSupportedMethod).ToArray();
            if (methods.Length == 0)
            {
                continue;
            }

            JSCallbackDescriptor methodDescriptor;
            if (methods.Length == 1 &&
                !methods[0].GetParameters().Any((p) => p.IsOptional))
            {
                MethodInfo method = methods[0];
                Trace($"    {(methodIsStatic ? "static " : string.Empty)}{methodName}(" +
                    string.Join(", ", method.GetParameters().Select((p) => p.ParameterType)) + ")");

                methodDescriptor = _marshaller.BuildFromJSMethodExpression(method).Compile();
            }
            else
            {
                // Set up overload resolution for multiple methods or optional parmaeters.
                Trace($"    {(methodIsStatic ? "static " : string.Empty)}{methodName}[" +
                    methods.Length + "]");
                foreach (MethodInfo method in methods)
                {
                    Trace($"        {methodName}(" + string.Join(
                        ", ", method.GetParameters().Select((p) => p.ParameterType)) + ")");

                }

                methodDescriptor = _marshaller.BuildMethodOverloadDescriptor(methods);
            }

            addMethodMethod.Invoke(
                classBuilder,
                new object?[]
                {
                    methodName,
                    methodDescriptor.Callback,
                    attributes | (methodIsStatic ? JSPropertyAttributes.Static : default),
                    methodDescriptor.Data,
                });
            if (!methodIsStatic && methodName == nameof(Object.ToString))
            {
                // Also export non-uppercased toString(), which is a special method in JavaScript.
                addMethodMethod.Invoke(
                    classBuilder,
                    new object?[]
                    {
                        "toString",
                        methodDescriptor.Callback,
                        attributes,
                        methodDescriptor.Data,
                    });
            }
        }
    }

    private void ExportNestedTypes(Type type, object classBuilder)
    {
        Type classBuilderType = classBuilder.GetType();
        MethodInfo? addValuePropertyMethod = classBuilderType.GetInstanceMethod(
            "AddProperty", new[] { typeof(string), typeof(JSValue), typeof(JSPropertyAttributes) });

        JSPropertyAttributes propertyAttributes = JSPropertyAttributes.Static |
            JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;

        foreach (Type nestedType in type.GetNestedTypes())
        {
            if (!nestedType.IsNestedPublic || !IsSupportedType(nestedType))
            {
                continue;
            }

            JSReference? nestedTypeReference = TryExportType(nestedType);
            if (nestedTypeReference != null)
            {
                addValuePropertyMethod.Invoke(
                    classBuilder,
                    new object[]
                    {
                        nestedType.Name,
                        nestedTypeReference.GetValue()!.Value,
                        propertyAttributes,
                    });
            }
        }
    }

    private JSReference ExportEnum(Type type)
    {
        Trace($"> {nameof(TypeExporter)}.ExportEnum({type.FullName})");

        if (_exportedTypes.TryGetValue(type, out JSReference? enumObjectReference))
        {
            return enumObjectReference;
        }

        JSClassBuilder<object> enumBuilder = new(type.Name);

        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            enumBuilder.AddProperty(
                field.Name,
                (JSValue)Convert.ToInt64(field.GetRawConstantValue()),
                JSPropertyAttributes.Static | JSPropertyAttributes.Enumerable);
        }

        JSValue enumObject = enumBuilder.DefineEnum();
        enumObjectReference = new JSReference(enumObject);
        _exportedTypes.Add(type, enumObjectReference);

        Trace($"< {nameof(TypeExporter)}.ExportEnum()");
        return enumObjectReference;
    }

    private static bool IsSupportedType(Type type)
    {
        if (type.IsPointer ||
            type == typeof(Type) ||
            type.Namespace == "System.Reflection" ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Memory<>)) ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ReadOnlyMemory<>)) ||
            (type.Namespace?.StartsWith("System.Collections.") == true && !type.IsGenericType) ||
            (type.Namespace?.StartsWith("System.Threading.") == true && type != typeof(Task) &&
            !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))))
        {
            return false;
        }

#if !NETFRAMEWORK
        if (type.IsByRefLike)
        {
            // ref structs like Span<T> aren't yet supported.
            return false;
        }
#endif

        if (typeof(Stream).IsAssignableFrom(type))
        {
            // Streams should be projected as Duplex.
            return false;
        }

        if (type.Assembly == typeof(JSValue).Assembly)
        {
            return false;
        }

        return true;
    }

    private static bool IsSupportedConstructor(ConstructorInfo constructor)
    {
        return constructor.GetParameters().All(IsSupportedParameter);
    }

    private static bool IsSupportedMethod(MethodInfo method)
    {
        return !method.IsGenericMethodDefinition &&
            method.CallingConvention != CallingConventions.VarArgs &&
            method.Name != nameof(System.Collections.IEnumerable.GetEnumerator) &&
            method.GetParameters().All(IsSupportedParameter) &&
            IsSupportedParameter(method.ReturnParameter);
    }

    private static bool IsSupportedParameter(ParameterInfo parameter)
    {
        Type parameterType = parameter.ParameterType;
        return IsSupportedType(parameterType);
    }
}
