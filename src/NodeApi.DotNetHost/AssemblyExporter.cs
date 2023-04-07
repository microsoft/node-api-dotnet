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
/// Dynamically exports APIs from a .NET assembly to JS.
/// </summary>
internal class AssemblyExporter
{
    private readonly JSMarshaller _marshaller;
    private readonly JSReference _assemblyObject;
    private readonly Dictionary<Type, JSReference> _typeObjects = new();

    /// <summary>
    /// Creates a new instance of the <see cref="AssemblyExporter" /> class.
    /// </summary>
    /// <param name="assembly">The assembly to be exported.</param>
    /// <param name="marshaller">Marshaller that supports dynamic binding to .NET APIs.</param>
    /// <param name="target">Proxy target object; any properties/methods on this object
    /// will be exposed on the exported assembly object in addition to assembly types.</param>
    public AssemblyExporter(
        Assembly assembly,
        JSMarshaller marshaller,
        JSObject target)
    {
        Assembly = assembly;
        _marshaller = marshaller;

        JSProxy proxy = new(target, CreateProxyHandler());
        _assemblyObject = new JSReference(proxy);
    }

    /// <summary>
    /// Gets the assembly being exported.
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// Gets the JS Value (Proxy object) that represents the exported assembly.
    /// </summary>
    public JSValue AssemblyObject => _assemblyObject.GetValue()!.Value;

    /// <summary>
    /// Creates a proxy handler that enables deferred enumeration and loading of types in the
    /// assembly.
    /// </summary>
    private JSProxy.Handler CreateProxyHandler() => new()
    {
        Get = (JSObject target, JSValue property, JSObject receiver) =>
        {
            if (target.ContainsKey(property))
            {
                // The host may define some properties on the target object.
                return target[property];
            }

            string? propertyName = property.IsString() ? (string?)property : null;
            if (propertyName == null)
            {
                return JSValue.Undefined;
            }

            return TryExportType(propertyName);
        },

        OwnKeys = (JSObject target) =>
        {
            JSArray keys = new();

            foreach (JSValue key in target.Keys.Select(v => (string)v))
            {
                keys.Add(key);
            }

            // TODO: Enumerate types in the assembly?

            return keys;
        },

        GetOwnPropertyDescriptor = (JSObject target, JSValue property) =>
        {
            if (target.TryGetValue(property, out JSValue value))
            {
                JSObject descriptor = new()
                {
                    ["enumerable"] = false, // Target properties are not enumerable.
                    ["configurable"] = false,
                    ["value"] = value,
                };
                return descriptor;
            }

            string? propertyName = property.IsString() ? (string?)property : null;
            if (propertyName == null)
            {
                return (JSObject)JSValue.Undefined;
            }

            JSValue typeValue = TryExportType(propertyName);
            if (!typeValue.IsUndefined())
            {
                JSObject descriptor = new()
                {
                    ["enumerable"] = true, // Type properties are enumerable.
                    ["configurable"] = false,
                    ["value"] = typeValue,
                };
                return descriptor;
            }

            return (JSObject)JSValue.Undefined;
        },
    };

    /// <summary>
    /// Attempts to load and export a type, either by simple name or full type name.
    /// </summary>
    /// <param name="name">Either a simple type name or a namespace-qualified type name.</param>
    /// <returns>The exported type, or <see cref="JSValue.Undefined"/> if the type was
    /// not found.</returns>
    public JSValue TryExportType(string name)
    {
        // TODO: Handle generic types.

        Type? type = Assembly.GetType(name);
        if (type == null)
        {
            type = Assembly.GetTypes().SingleOrDefault((t) => t.Name == name);
            if (type == null)
            {
                return JSValue.Undefined;
            }
        }

        try
        {
            if (type.IsEnum)
            {
                return ExportEnum(type);
            }
            if (type.IsClass || type.IsInterface || type.IsValueType)
            {
                return ExportClass(type);
            }
            else
            {
                return JSValue.Undefined;
            }
        }
        catch (Exception ex)
        {
            Trace($"Failed to export type {type}: {ex}");
            throw;
        }
    }

    private JSValue ExportClass(Type type)
    {
        if (_typeObjects.TryGetValue(type, out JSReference? typeObjectReference))
        {
            return typeObjectReference!.GetValue()!.Value;
        }

        if (type == typeof(object) || type == typeof(string) ||
            type == typeof(void) || type.IsPrimitive)
        {
            return default;
        }

        Trace($"> AssemblyExporter.ExportClass({type.FullName})");

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

        string defineMethodName = type.IsInterface ? "DefineInterface" :
            isStatic ? "DefineStaticClass" : type.IsValueType ? "DefineStruct" : "DefineClass";
        MethodInfo defineClassMethod = classBuilderType.GetInstanceMethod(defineMethodName);
        JSValue classObject = (JSValue)defineClassMethod.Invoke(
            classBuilder,
            defineClassMethod.GetParameters().Select((_) => (object?)null).ToArray())!;

        _typeObjects.Add(type, new JSReference(classObject));

        // Also export any types returned by properties or methods of this type, because
        // they might otherwise not be referenced by JS before they are used.
        ExportClassDependencies(type);

        Trace($"< AssemblyExporter.ExportClass()");
        return classObject;
    }

    private void ExportClassDependencies(Type type)
    {
        foreach (MemberInfo member in type.GetMembers
            (BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        {
            if (member is PropertyInfo property &&
                property.PropertyType.Assembly == type.Assembly &&
                IsSupportedType(property.PropertyType) &&
                !JSMarshaller.IsConvertedType(property.PropertyType))
            {
                ExportClass(property.PropertyType);
            }
            else if (member is MethodInfo method &&
                IsSupportedMethod(method) &&
                method.ReturnType.Assembly == type.Assembly &&
                IsSupportedType(method.ReturnType) &&
                !JSMarshaller.IsConvertedType(method.ReturnType))
            {
                ExportClass(method.ReturnType);
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

    private JSValue ExportEnum(Type type)
    {
        Trace($"> AssemblyExporter.ExportEnum({type.FullName})");

        if (_typeObjects.TryGetValue(type, out JSReference? typeObjectReference))
        {
            return typeObjectReference!.GetValue()!.Value;
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
        _typeObjects.Add(type, new JSReference(enumObject));

        Trace($"< AssemblyExporter.ExportEnum()");
        return enumObject;
    }

    private static bool IsSupportedType(Type type)
    {
        if (type.IsPointer ||
            type == typeof(Type) ||
            type.Namespace == "System.Reflection" ||
            (type.Namespace?.StartsWith("System.Collections.") == true && !type.IsGenericType) ||
            (type.Namespace?.StartsWith("System.Threading.") == true && type != typeof(Task) &&
            !(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))))
        {
            return false;
        }

#if NETFRAMEWORK
        if (type.IsByRef)
#else
        if (type.IsByRef || type.IsByRefLike)
#endif
        {
            // ref parameters aren't yet supported.
            // ref structs like Span<T> aren't yet supported.
            return false;
        }

        if (typeof(Stream).IsAssignableFrom(type))
        {
            // Streams should be projected as Duplex.
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
        if (parameter.IsOut)
        {
            // out parameters aren't yet supported.
            return false;
        }

        Type parameterType = parameter.ParameterType;
        return IsSupportedType(parameterType);
    }
}
