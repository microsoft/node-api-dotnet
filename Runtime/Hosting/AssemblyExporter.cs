using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using static NodeApi.Hosting.ManagedHost;

namespace NodeApi.Hosting;
/// <summary>
/// Dynamically exports APIs from a .NET assembly to JS.
/// </summary>
[RequiresUnreferencedCode("Dynamic binding is not available in trimmed assembly.")]
[RequiresDynamicCode("Dynamic binding is not available in trimmed assembly.")]
internal class AssemblyExporter
{
    private readonly JSMarshaler _marshaler;
    private readonly JSReference _assemblyObject;
    private readonly Dictionary<Type, JSReference> _typeObjects = new();

    /// <summary>
    /// Creates a new instance of the <see cref="AssemblyExporter" /> class.
    /// </summary>
    /// <param name="assembly">The assembly to be exported.</param>
    /// <param name="marshaler">Marshaler that supports dynamic binding to .NET APIs.</param>
    /// <param name="target">Proxy target object; any properties/methods on this object
    /// will be exposed on the exported assembly object in addition to assembly types.</param>
    public AssemblyExporter(
        Assembly assembly,
        JSMarshaler marshaler,
        JSObject target)
    {
        Assembly = assembly;
        _marshaler = marshaler;

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

        if (type.IsClass)
        {
            return ExportClass(type);
        }
        else if (type.IsEnum)
        {
            return ExportEnum(type);
        }
        else if (type.IsValueType)
        {
            return ExportStruct(type);
        }
        else
        {
            return JSValue.Undefined;
        }
    }

    private JSValue ExportClass(Type classType)
    {
        Trace($"> AssemblyExporter.ExportStaticClass({classType.FullName})");

        if (_typeObjects.TryGetValue(classType, out JSReference? typeObjectReference))
        {
            Trace($"< AssemblyExporter.ExportStaticClass() => already exported");
            return typeObjectReference!.GetValue()!.Value;
        }

        List<JSPropertyDescriptor> classProperties = new();

        // TODO: Non-static class members

        JSPropertyAttributes attributes =
            JSPropertyAttributes.Enumerable | JSPropertyAttributes.Configurable;
        foreach (MemberInfo member in classType.GetMembers(
            BindingFlags.Public | BindingFlags.Static))
        {
            if (member is MethodInfo method && !method.IsSpecialName)
            {
                // TODO: Implement overload resolution for dynamically invoked methods.
                // For an initial demo, overload resolution is hardcoded for the WriteLine method.
                if (method.Name == "WriteLine" && (method.GetParameters().Length != 1 ||
                    method.GetParameters()[0].ParameterType != typeof(string)))
                {
                    continue;
                }

                LambdaExpression lambda = _marshaler.BuildFromJSMethodExpression(method);
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
                    LambdaExpression lambda = _marshaler.BuildFromJSPropertyGetExpression(property);
                    getterDelegate = (JSCallback)lambda.Compile();
                }

                JSCallback? setterDelegate = null;
                if (property.SetMethod != null)
                {
                    LambdaExpression lambda = _marshaler.BuildFromJSPropertySetExpression(property);
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
}
