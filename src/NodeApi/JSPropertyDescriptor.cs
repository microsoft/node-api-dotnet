// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Describes a property of a JavaScript object, including its name, value, and attributes.
/// Can be converted to a JavaScript property descriptor object using the <see cref="ToObject"/>
/// method.
/// </summary>
[DebuggerDisplay("{Name,nq}")]
public readonly struct JSPropertyDescriptor
{
    /// <summary>
    /// Saves the module context under which the callback was defined, so that multiple .NET
    /// modules in the same process can register callbacks for module-level functions.
    /// </summary>
    internal JSModuleContext? ModuleContext { get; init; }

    // Either Name or NameValue should be non-null.
    // NameValue supports non-string property names like symbols.
    public string? Name { get; }
    public JSValue? NameValue { get; }

    public JSCallback? Method { get; }
    public JSCallback? Getter { get; }
    public JSCallback? Setter { get; }
    public JSValue? Value { get; }
    public JSPropertyAttributes Attributes { get; }

    /// <summary>
    /// Gets the optional context data object to be passed to getter/setter or method callbacks.
    /// </summary>
    public object? Data { get; }

    /// <summary>
    /// Creates a property descriptor with a string name.
    /// </summary>
    public JSPropertyDescriptor(
        string name,
        JSCallback? method = null,
        JSCallback? getter = null,
        JSCallback? setter = null,
        JSValue? value = null,
        JSPropertyAttributes attributes = JSPropertyAttributes.Default,
        object? data = null)
    {
        ModuleContext = JSValueScope.Current.ModuleContext;

        Name = name;
        Method = method;
        Getter = getter;
        Setter = setter;
        Value = value;
        Attributes = attributes;
        Data = data;
    }

    /// <summary>
    /// Creates a property descriptor with a name that can be either a string or symbol value.
    /// </summary>
    public JSPropertyDescriptor(
        JSValue name,
        JSCallback? method = null,
        JSCallback? getter = null,
        JSCallback? setter = null,
        JSValue? value = null,
        JSPropertyAttributes attributes = JSPropertyAttributes.Default,
        object? data = null)
    {
        ModuleContext = JSValueScope.Current.ModuleContext;

        NameValue = name;
        Method = method;
        Getter = getter;
        Setter = setter;
        Value = value;
        Attributes = attributes;
        Data = data;
    }

    /// <summary>
    /// Creates a property descriptor with a value.
    /// </summary>
    public static JSPropertyDescriptor DataProperty(
        string name,
        JSValue value,
        JSPropertyAttributes attributes = JSPropertyAttributes.Default,
        object? data = null)
    {
        return new JSPropertyDescriptor(name, null, null, null, value, attributes, data);
    }

    /// <summary>
    /// Creates a property descriptor with getter and/or setter callbacks.
    /// </summary>
    /// <exception cref="ArgumentException">Both getter and setter are null.</exception>
    public static JSPropertyDescriptor AccessorProperty(
        string name,
        JSCallback? getter = null,
        JSCallback? setter = null,
        JSPropertyAttributes attributes = JSPropertyAttributes.Default,
        object? data = null)
    {
        if (getter == null && setter == null)
        {
            throw new ArgumentException(
                $"Either `{nameof(getter)}` or `{nameof(setter)}` or both must be not null");
        }

        return new JSPropertyDescriptor(name, null, getter, setter, null, attributes, data);
    }

    /// <summary>
    /// Creates a property descriptor with a method callback.
    /// </summary>
    public static JSPropertyDescriptor Function(
        string name,
        JSCallback method,
        JSPropertyAttributes attributes = JSPropertyAttributes.Default,
        object? data = null)
    {
        return new JSPropertyDescriptor(name, method, null, null, null, attributes, data);
    }

    /// <summary>
    /// Converts the structure to a JavaScript property descriptor object.
    /// </summary>
    public JSObject ToObject()
    {
        JSObject descriptor = new();

        if (Value != null)
        {
            descriptor["value"] = Value.Value;
        }
        else if (Method != null)
        {
            descriptor["value"] =
                JSValue.CreateFunction(Name, Method);
        }
        else
        {
            if (Getter != null)
            {
                descriptor["get"] =
                    JSValue.CreateFunction(Name, Getter);
            }
            if (Setter != null)
            {
                descriptor["set"] =
                    JSValue.CreateFunction(Name, Setter);
            }
        }

        if (Attributes.HasFlag(JSPropertyAttributes.Writable))
        {
            descriptor["writable"] = true;
        }
        if (Attributes.HasFlag(JSPropertyAttributes.Enumerable))
        {
            descriptor["enumerable"] = true;
        }
        if (Attributes.HasFlag(JSPropertyAttributes.Configurable))
        {
            descriptor["configurable"] = true;
        }

        return descriptor;
    }

    /// <summary>
    /// Converts the structure to a JavaScript property descriptor object.
    /// </summary>
    public static explicit operator JSObject(JSPropertyDescriptor descriptor)
        => descriptor.ToObject();
}
