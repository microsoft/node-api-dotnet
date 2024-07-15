// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Represents a projection of a .NET namespace as JavaScript proxy object. Supports merging
/// namespaces from multiple assemblies into the combined namespace hierarchy.
/// Also indexes types in the namespace and manages deferred export of the types.
/// </summary>
internal class NamespaceProxy
{
    private JSReference? _valueReference;
    private JSReference? _tostringReference;

    /// <summary>
    /// Creates a new namespace object.
    /// </summary>
    /// <param name="name">Full name of the namespace, including any parent namespaces.</param>
    /// <param name="containingNamespace">Containing (parent) namespace of this one, or null
    /// if this is a top-level namespace.</param>
    /// <param name="typeExporter">A callback that can export a type to JS on-demand. It returns
    /// a strong reference to the JS object that represents the type (and constructor, for classes),
    /// or null if the type cannot be exported.</param>
    public NamespaceProxy(
        string name,
        NamespaceProxy? containingNamespace,
        TypeExporter typeExporter)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

        Name = name;
        ContainingNamespace = containingNamespace;
        TypeExporter = typeExporter;

        if (!typeExporter.IsDelayLoadEnabled)
        {
            GetNamespaceObject();
            GetToStringFunction();
        }
    }

    /// <summary>
    /// Gets the full name of the namespace.
    /// </summary>
    public string Name { get; }

    internal TypeExporter TypeExporter { get; }

    /// <summary>
    /// Gets the JS object value that is a projection of the namespace to JS.
    /// If <see cref="TypeExporter.IsDelayLoadEnabled"/> is true), the JS object is created the
    /// first time the value is requested.
    /// </summary>
    public JSProxy Value => GetNamespaceObject();

    /// <summary>
    /// Gets the containing (parent) namespace of this namespace,
    /// or null if this is a top-level namespace. (The "global" namespace is not supported.)
    /// </summary>
    public NamespaceProxy? ContainingNamespace { get; }

    /// <summary>
    /// Gets all the known child namespaces of this namespace, indexed by child name
    /// (not full name).
    /// </summary>
    public IDictionary<string, NamespaceProxy> Namespaces { get; }
        = new Dictionary<string, NamespaceProxy>();

    /// <summary>
    /// Gets all the known types in this namespace, indexed by type name (not full name).
    /// </summary>
    /// <remarks>
    /// Types become "known" after their assembly is loaded, so as more assemblies
    /// are loaded the collection may grow.
    /// </remarks>
    public IDictionary<string, TypeProxy> Types { get; }
        = new Dictionary<string, TypeProxy>();

    /// <summary>
    /// Gets the full name of the namespace.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => Name;

    private JSProxy GetNamespaceObject()
    {
        if (_valueReference != null)
        {
            return (JSProxy)_valueReference.GetValue();
        }

        JSProxy proxy = new(new JSObject(), CreateProxyHandler());
        _valueReference = new JSReference(proxy);
        return proxy;
    }

    private JSFunction GetToStringFunction()
    {
        if (_tostringReference != null)
        {
            return (JSFunction)_tostringReference.GetValue();
        }

        // Calling `toString()` on a namespace returns the full namespace name.
        JSFunction tostringFunction = new("toString", () => ToString());
        _tostringReference = new JSReference(tostringFunction);
        return tostringFunction;
    }

    /// <summary>
    /// Creates a handler for a <see cref="JSProxy"/> that supports deferred export of types.
    /// </summary>
    private JSProxy.Handler CreateProxyHandler() => new()
    {
        Get = (JSObject target, JSValue property, JSObject receiver) =>
        {
            string propertyName = property.IsString() ? (string)property : string.Empty;

            if (propertyName == "toString")
            {
                return GetToStringFunction();
            }
            else if (Namespaces.TryGetValue(propertyName, out NamespaceProxy? ns))
            {
                // Child namespace.
                return ns.Value;
            }
            else if (Types.TryGetValue(propertyName, out TypeProxy? typeProxy))
            {
                // Type in the namespace.
                return typeProxy.Value ?? default;
            }

            // Unknown type.
            return default;
        },

        OwnKeys = (JSObject target) =>
        {
            JSArray keys = new();

            foreach (string ns in Namespaces.Keys)
            {
                keys.Add(ns);
            }

            foreach (string t in Types.Keys)
            {
                keys.Add(t);
            }

            return keys;
        },

        GetOwnPropertyDescriptor = (JSObject target, JSValue property) =>
        {
            string propertyName = property.IsString() ? (string)property : string.Empty;

            if (Namespaces.TryGetValue(propertyName, out NamespaceProxy? ns))
            {
                // Child namespace.
                return new JSObject
                {
                    ["enumerable"] = true,
                    ["configurable"] = true,
                    ["value"] = ns.Value,
                };
            }
            else if (Types.TryGetValue(propertyName, out TypeProxy? type))
            {
                // Type in the namespace.
                return new JSObject
                {
                    ["enumerable"] = true,
                    ["configurable"] = true,
                    ["value"] = type.Value ?? default,
                };
            }

            // Unknown type.
            return default;
        },
    };
}
