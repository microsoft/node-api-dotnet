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
    /// <param name="deferMembers">True to delay exporting of all type members until each one is
    /// accessed. If false, all type members are immediately exported, which may cascade to
    /// exporting many additional types referenced by the members, including members that are
    /// never actually used.</param>
    /// <returns>A strong reference to a JS object that represents the exported type, or null
    /// if the type could not be exported.</returns>
    public JSReference? TryExportType(Type type, bool deferMembers)
    {
        try
        {
            return ExportType(type, deferMembers);
        }
        catch (NotSupportedException ex)
        {
            Trace($"Cannot export type {type}: {ex}");
            return null;
        }
        catch (Exception ex)
        {
            Trace($"Failed to export type {type}: {ex}");
            return null;
        }
    }

    private JSReference ExportType(Type type, bool deferMembers)
    {
        if (!IsSupportedType(type))
        {
            throw new NotSupportedException("The type is not supported for JS export.");
        }
        else if (type.IsEnum)
        {
            return ExportEnum(type);
        }
        else if (type.IsGenericTypeDefinition)
        {
            return ExportGenericTypeDefinition(type, deferMembers);
        }
        else if (type.IsClass || type.IsInterface || type.IsValueType)
        {
            if (type.IsClass && type.BaseType?.FullName == typeof(MulticastDelegate).FullName)
            {
                // Delegate types are not exported as type objects, but the JS marshaller can
                // still dynamically convert delegate instances to/from JS functions.
                throw new NotSupportedException("Delegate types are not exported.");
            }
            else
            {
                return ExportClass(type, deferMembers);
            }
        }
        else
        {
            throw new NotSupportedException("Unknown type kind.");
        }
    }

    private JSReference ExportClass(Type type, bool deferMembers)
    {
        string typeName = type.Name;

        if (_exportedTypes.TryGetValue(type, out JSReference? classObjectReference))
        {
            return classObjectReference;
        }

        Trace($"> {nameof(TypeExporter)}.ExportClass({type.FormatName()})");

        // Add a temporary null entry to the dictionary while exporting this type, in case the
        // type is encountered while exporting members. It will be non-null by the time this method returns
        // (or removed if an exception is thrown).
        _exportedTypes.Add(type, null!);
        try
        {
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
                JSCallbackDescriptor constructorDescriptor =
                    CreateConstructorDescriptor(type, deferMembers);
                classBuilder = classBuilderType.CreateInstance(
                    new[] { typeof(string), typeof(JSCallbackDescriptor) },
                    new object[] { type.Name, constructorDescriptor });
            }

            ExportProperties(type, classBuilder, deferMembers);
            ExportMethods(type, classBuilder, deferMembers);
            ExportNestedTypes(type, classBuilder, deferMembers);

            string defineMethodName = type.IsInterface ? "DefineInterface" :
                isStatic ? "DefineStaticClass" : type.IsValueType ? "DefineStruct" : "DefineClass";
            MethodInfo defineClassMethod = classBuilderType.GetInstanceMethod(defineMethodName);
            JSValue classObject = (JSValue)defineClassMethod.Invoke(
                classBuilder,
                defineClassMethod.GetParameters().Select((_) => (object?)null).ToArray())!;

            classObjectReference = new JSReference(classObject);
            _exportedTypes[type] = classObjectReference;
        }
        catch
        {
            // Clean up the temporary null entry.
            _exportedTypes.Remove(type);
            throw;
        }

        if (!deferMembers)
        {
            // Also export any types returned by properties or methods of this type, because
            // they might otherwise not be referenced by JS before they are used.
            ExportClassDependencies(type);
        }

        Trace($"< {nameof(TypeExporter)}.ExportClass()");
        return classObjectReference;
    }

    private JSCallbackDescriptor CreateConstructorDescriptor(Type type, bool defer)
    {
        ConstructorInfo[] constructors =
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Where(IsSupportedConstructor)
            .ToArray();

        JSCallbackDescriptor constructorDescriptor;
        if (defer)
        {
            // Create a descriptor that does deferred loading and resolution of overloads.
            // (It also handles the case when there is no overloading, only one constructor.)
            constructorDescriptor = JSCallbackOverload.CreateDescriptor(type.Name, () =>
            {
                return _marshaller.GetConstructorOverloads(constructors);
            });
        }
        else
        {
            if (constructors.Length == 1 &&
                !constructors[0].GetParameters().Any((p) => p.IsOptional))
            {
                // No deferral and no overload resolution - use the single callback descriptor.
                constructorDescriptor = new JSCallbackDescriptor(
                    type.Name,
                    _marshaller.BuildFromJSConstructorExpression(constructors[0]).Compile());
            }
            else
            {
                // Multiple constructors or optional parameters require overload resolution.
                constructorDescriptor = JSCallbackOverload.CreateDescriptor(
                    type.Name, _marshaller.GetConstructorOverloads(constructors));
            }
        }

        return constructorDescriptor;
    }

    private void ExportClassDependencies(Type type)
    {

        foreach (MemberInfo member in type.GetMembers
            (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            if (member is PropertyInfo property &&
                !JSMarshaller.IsConvertedType(property.PropertyType))
            {
                ExportTypeIfSupported(property.PropertyType, deferMembers: false);
            }

            if (member is MethodInfo method &&
                IsSupportedMethod(method) &&
                !JSMarshaller.IsConvertedType(method.ReturnType))
            {
                ExportTypeIfSupported(method.ReturnType, deferMembers: false);
            }

            if (member is MethodInfo interfaceMethod && type.IsInterface)
            {
                // Interface method parameter types must be exported in case the interface
                // will be implemented by JS.
                foreach (ParameterInfo interfaceMethodParameter in interfaceMethod.GetParameters())
                {
                    ExportTypeIfSupported(
                        interfaceMethodParameter.ParameterType, deferMembers: false);
                }

                ExportTypeIfSupported(interfaceMethod.ReturnType, deferMembers: false);
            }
        }
    }

    private void ExportTypeIfSupported(Type dependencyType, bool deferMembers)
    {
        if (dependencyType.IsArray || dependencyType.IsByRef)
        {
            ExportTypeIfSupported(dependencyType.GetElementType()!, deferMembers);
            return;
        }
        else if (dependencyType.IsGenericType)
        {
            Type genericTypeDefinition = dependencyType.GetGenericTypeDefinition();
            if (genericTypeDefinition == typeof(Nullable<>) ||
                genericTypeDefinition == typeof(Task<>) ||
                genericTypeDefinition.Namespace == typeof(IList<>).Namespace)
            {
                foreach (Type typeArg in dependencyType.GetGenericArguments())
                {
                    ExportTypeIfSupported(typeArg, deferMembers);
                }
                return;
            }
        }

        if (
#if !NETFRAMEWORK // TODO: Find an alternative for .NET Framework.
            !dependencyType.IsGenericTypeParameter &&
            !dependencyType.IsGenericMethodParameter &&
#endif
            IsSupportedType(dependencyType))
        {
            TryExportType(dependencyType, deferMembers);
        }
    }

    private void ExportProperties(Type type, object classBuilder, bool defer)
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

                JSCallback? getterCallback = null;
                if (property.GetMethod != null)
                {
                    if (defer)
                    {
                        // Set up a callback that defers generation of marshalling callbacks
                        // for the property until the first time it is accessed.
                        getterCallback = (args) =>
                        {
                            JSCallback getter =
                                _marshaller.BuildFromJSPropertyGetExpression(property).Compile();
                            JSCallback? setter = property.SetMethod == null ? null :
                                _marshaller.BuildFromJSPropertySetExpression(property).Compile();
                            args.ThisArg.DefineProperties(JSPropertyDescriptor.Accessor(
                                property.Name, getter, setter, propertyAttributes));

                            ExportTypeIfSupported(property.PropertyType, deferMembers: true);

                            return getter(args);
                        };
                    }
                    else
                    {
                        getterCallback = _marshaller.BuildFromJSPropertyGetExpression(property)
                            .Compile();
                    }
                }

                JSCallback? setterCallback = null;
                if (property.SetMethod != null)
                {
                    if (defer)
                    {
                        setterCallback = (args) =>
                        {
                            JSCallback? getter = property.GetMethod == null ? null :
                                _marshaller.BuildFromJSPropertyGetExpression(property).Compile();
                            JSCallback setter =
                                _marshaller.BuildFromJSPropertySetExpression(property).Compile();
                            args.ThisArg.DefineProperties(JSPropertyDescriptor.Accessor(
                                property.Name, getter, setter, propertyAttributes));

                            ExportTypeIfSupported(property.PropertyType, deferMembers: true);

                            return setter(args);
                        };
                    }
                    else
                    {
                        setterCallback = _marshaller.BuildFromJSPropertySetExpression(property)
                            .Compile();
                    }
                }

                addPropertyMethod.Invoke(
                    classBuilder,
                    new object?[]
                    {
                        property.Name,
                        getterCallback,
                        setterCallback,
                        propertyAttributes,
                        null,
                    });
            }
        }
    }

    private void ExportMethods(Type type, object classBuilder, bool defer)
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

            if (methods.Any((m) => m.IsGenericMethodDefinition))
            {
                Trace($"    {(methodIsStatic ? "static " : string.Empty)}{methodName}<>()");

                // Exporting generic methods is always essentially deferred because the methods
                // cannot be fully exported until the type parameter(s) are known.
                MethodInfo[] genericMethods = methods.Where(
                    (m) => m.IsGenericMethodDefinition).ToArray();
                ExportGenericMethodDefinition(classBuilder, genericMethods);

                methods = methods.Where((m) => !m.IsGenericMethodDefinition).ToArray();
                if (methods.Length == 0)
                {
                    continue;
                }
            }

            JSCallbackDescriptor methodDescriptor = CreateMethodDescriptor(methods, defer);

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

    private JSCallbackDescriptor CreateMethodDescriptor(MethodInfo[] methods, bool defer)
    {
        JSCallbackDescriptor methodDescriptor;
        string methodName = methods[0].Name;
        bool methodIsStatic = methods[0].IsStatic;
        Trace($"    {(methodIsStatic ? "static " : string.Empty)}{methodName}()" +
            (methods.Length > 1 ? " [" + methods.Length + "]" : string.Empty));

        if (defer)
        {
            // Create a descriptor that does deferred loading and resolution of overloads.
            // (It also handles the case when there is no overloading, only one method.)
            methodDescriptor = JSCallbackOverload.CreateDescriptor(methodName, () =>
            {
                JSCallbackOverload[] overloads = _marshaller.GetMethodOverloads(methods);

                ExportTypeIfSupported(methods[0].ReturnType, deferMembers: true);

                return overloads;
            });
        }
        else
        {
            if (methods.Length == 1 &&
                !methods[0].GetParameters().Any((p) => p.IsOptional))
            {
                // No deferral and no overload resolution - use the single callback descriptor.
                methodDescriptor = _marshaller.BuildFromJSMethodExpression(methods[0]).Compile();
            }
            else
            {
                // Multiple overloads or optional parameters require overload resolution.
                methodDescriptor = JSCallbackOverload.CreateDescriptor(
                    methodName, _marshaller.GetMethodOverloads(methods));
            }
        }
        return methodDescriptor;

    }

    private void ExportNestedTypes(Type type, object classBuilder, bool deferMembers)
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

            JSReference? nestedTypeReference = TryExportType(nestedType, deferMembers);
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
        Trace($"> {nameof(TypeExporter)}.ExportEnum({type.FormatName()})");

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
        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        if (type.IsPointer ||
            type == typeof(void) ||
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
        return method.CallingConvention != CallingConventions.VarArgs &&
            method.Name != nameof(System.Collections.IEnumerable.GetEnumerator) &&
            method.GetParameters().All(IsSupportedParameter) &&
            (method.ReturnType == typeof(void) || IsSupportedParameter(method.ReturnParameter));
    }

    private static bool IsSupportedParameter(ParameterInfo parameter)
    {
        Type parameterType = parameter.ParameterType;

        if (parameter.Position < 0 && parameterType.IsByRef)
        {
            // Ref return values are not supported.
            return false;
        }

        return IsSupportedType(parameterType);
    }

    private JSReference ExportGenericTypeDefinition(Type type, bool deferMembers)
    {
        // TODO: Support multiple generic types with same name and differing type arg counts.

        if (_exportedTypes.TryGetValue(type, out JSReference? genericTypeFunctionReference))
        {
            return genericTypeFunctionReference;
        }

        // A generic type definition is exported as a function that constructs the
        // specialized generic type.
        JSFunction function = new(
            (args) => MakeGenericType(args, deferMembers), callbackData: type);

        // Override the type's toString() to return the formatted generic type name.
        ((JSValue)function).SetProperty("toString", new JSFunction(() => type.FormatName()));

        genericTypeFunctionReference = new JSReference(function);
        _exportedTypes.Add(type, genericTypeFunctionReference);
        return genericTypeFunctionReference;
    }

    /// <summary>
    /// Makes a specialized generic type from a generic type definition and type arguments.
    /// </summary>
    /// <param name="args">Type arguments passed as JS values.</param>
    /// <returns>A strong reference to a JS value that represents the specialized generic
    /// type.</returns>
    private JSValue MakeGenericType(JSCallbackArgs args, bool deferMembers)
    {
        Type genericTypeDefinition = args.Data as Type ??
            throw new ArgumentException("Missing generic type definition.");

        Type[] typeArgs = new Type[args.Length];
        for (int i = 0; i < typeArgs.Length; i++)
        {
            typeArgs[i] = args[i].TryUnwrap() as Type ??
                throw new ArgumentException($"Invalid generic type argument at position {i}.");
        }

        Type genericType;
        try
        {
            genericType = genericTypeDefinition.MakeGenericType(typeArgs);
        }
        catch (Exception ex)
        {
            throw new JSException(
                $"Failed to make generic type {genericTypeDefinition.FormatName()} with supplied " +
                $"type arguments: [{string.Join(", ", typeArgs.Select((t) => t.FormatName()))}]. " +
                ex.Message,
                ex);
        }

        JSReference exportedTypeReference = ExportType(genericType, deferMembers);
        return exportedTypeReference.GetValue()!.Value;
    }

    private void ExportGenericMethodDefinition(object classBuilder, MethodInfo[] methods)
    {
        // Add method that is a function that makes the generic method.
        MethodInfo addMethodMethod = classBuilder.GetType().GetInstanceMethod(
            "AddMethod",
            new[]
            {
                typeof(string),
                typeof(JSCallback),
                typeof(JSPropertyAttributes),
                typeof(object),
            });
        addMethodMethod.Invoke(
            classBuilder,
            new object[]
            {
                methods[0].Name + '$',
                (JSCallback)MakeGenericMethod,
                JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable |
                    (methods[0].IsStatic ? JSPropertyAttributes.Static : default),
                methods,
            });
    }

    private JSValue MakeGenericMethod(JSCallbackArgs args)
    {
        MethodInfo[] genericMethodDefinitions = args.Data as MethodInfo[] ??
            throw new ArgumentException("Missing generic type definition.");

        Type[] typeArgs = new Type[args.Length];
        for (int i = 0; i < typeArgs.Length; i++)
        {
            typeArgs[i] = args[i].TryUnwrap() as Type ??
                throw new ArgumentException($"Invalid generic type argument at position {i}.");
        }

        MethodInfo[] matchingMethodDefinitions = genericMethodDefinitions
            .Where((m) => m.GetGenericArguments().Length == typeArgs.Length)
            .ToArray();

        if (matchingMethodDefinitions.Length == 0)
        {
            throw new JSException(
                "Incorrect number of type arguments for method: +" +
                genericMethodDefinitions[0].Name);
        }

        MethodInfo[] matchingMethods;
        try
        {
            matchingMethods = genericMethodDefinitions.Select((m) => m.MakeGenericMethod(typeArgs))
                .ToArray();
        }
        catch (Exception ex)
        {
            throw new JSException(
                $"Failed to make generic method {genericMethodDefinitions[0].Name} with supplied " +
                $"type arguments: [{string.Join(", ", typeArgs.Select((t) => t.FormatName()))}]. " +
                ex.Message,
                ex);
        }

        JSCallbackDescriptor descriptor = CreateMethodDescriptor(matchingMethods, defer: false);
        JSFunction function = new(descriptor.Callback, descriptor.Data);

        if (!args.ThisArg.IsUndefined())
        {
            function = function.Bind(args.ThisArg);
        }

        return function;
    }
}
