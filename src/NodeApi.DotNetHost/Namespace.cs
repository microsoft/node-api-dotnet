// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Represents a projection of a .NET namespace into JavaScript. Supports merging
/// namespaces from multiple assemblies into the combined namespace hierarchy.
/// Also indexes types in the namespace and manages deferred export of the types.
/// </summary>
internal class Namespace
{
    private readonly Func<Type, JSReference?> _export;
    private readonly JSReference _valueReference;
    private readonly JSReference _tostringReference;

    /// <summary>
    /// Creates a new namespace object.
    /// </summary>
    /// <param name="name">Full name of the namespace, including any parent namespaces.</param>
    /// <param name="export">A callback that can export a type to JS on-demand. It returns
    /// a strong reference to the JS object that represents the type (and constructor, for classes),
    /// or null if the type cannot be exported.</param>
    public Namespace(string name, Func<Type, JSReference?> export)
    {
        if (string.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));

        Name = name;
        _export = export;
        JSProxy proxy = new(new JSObject(), CreateProxyHandler());
        _valueReference = new JSReference(proxy);
        _tostringReference = new JSReference(new JSFunction(() => ToString()));
    }

    /// <summary>
    /// Gets the full name of the namespace.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the JS object value that is a projection of the namespace to JS.
    /// </summary>
    public JSValue Value => _valueReference.GetValue()!.Value;

    /// <summary>
    /// Gets all the known child namespaces of this namespace, indexed by child name
    /// (not full name).
    /// </summary>
    public IDictionary<string, Namespace> Namespaces { get; }
        = new Dictionary<string, Namespace>();

    /// <summary>
    /// Gets all the known types in this namespace, indexed by type name (not full name).
    /// </summary>
    public IDictionary<string, Type> Types { get; }
        = new Dictionary<string, Type>();

    /// <summary>
    /// Gets all the exported types in this namespace, indexed by type name (not full name).
    /// Each value is a strong reference to a JS object that is a projection of the type,
    /// or null if the type could not be exported.
    /// </summary>
    public IDictionary<string, JSReference?> JSTypes { get; }
        = new Dictionary<string, JSReference?>();

    /// <summary>
    /// Gets the full name of the namespace.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => Name;

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
                // Calling `toString()` on a namespace returns the full namespace name.
                return _tostringReference.GetValue()!.Value;
            }
            else if (Namespaces.TryGetValue(propertyName, out Namespace? ns))
            {
                return ns.Value;
            }

            if (!JSTypes.TryGetValue(propertyName, out JSReference? jsTypeRef))
            {
                if (!Types.TryGetValue(propertyName, out Type? type))
                {
                    return default;
                }

                // The type is known but not yet exported. Invoke the callback to export it.
                jsTypeRef = _export(type);
                JSTypes.Add(propertyName, jsTypeRef);
            }

            // The reference here may be null if the type could not be exported for some reason.
            return jsTypeRef?.GetValue()!.Value ?? default;
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

            if (Namespaces.TryGetValue(propertyName, out Namespace? ns))
            {
                return new JSObject
                {
                    ["enumerable"] = true,
                    ["configurable"] = true,
                    ["value"] = ns.Value,
                };
            }

            if (!JSTypes.TryGetValue(propertyName, out JSReference? jsTypeRef))
            {
                if (!Types.TryGetValue(propertyName, out Type? type))
                {
                    return default;
                }

                jsTypeRef = _export(type);
                JSTypes.Add(propertyName, jsTypeRef);
            }

            return new JSObject
            {
                ["enumerable"] = true,
                ["configurable"] = true,
                ["value"] = jsTypeRef?.GetValue()!.Value ?? default,
            };
        },
    };
}
