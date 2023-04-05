// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSPropertyDescriptor
{
    /// <summary>
    /// Saves the module context under which the callback was defined, so that multiple .NET
    /// modules in the same process can register callbacks for module-level functions.
    /// </summary>
    internal JSModuleContext? ModuleContext { get; init; }

    public JSValue Name { get; }
    public JSCallback? Method { get; }
    public JSCallback? Getter { get; }
    public JSCallback? Setter { get; }
    public JSValue? Value { get; }
    public JSPropertyAttributes Attributes { get; }
    public object? Data { get; }

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

        Name = name;
        Method = method;
        Getter = getter;
        Setter = setter;
        Value = value;
        Attributes = attributes;
        Data = data;
    }

    public static JSPropertyDescriptor Accessor(
        JSValue name,
        JSCallback? getter = null,
        JSCallback? setter = null,
        JSPropertyAttributes attributes = JSPropertyAttributes.Default,
        object? data = null)
    {
        if (getter == null && setter == null)
        {
            throw new ArgumentException($"Either `{nameof(getter)}` or `{nameof(setter)}` or both must be not null");
        }

        return new JSPropertyDescriptor(name, null, getter, setter, null, attributes, data);
    }

    public static JSPropertyDescriptor ForValue(
        JSValue name,
        JSValue value,
        JSPropertyAttributes attributes = JSPropertyAttributes.Default,
        object? data = null)
    {
        return new JSPropertyDescriptor(name, null, null, null, value, attributes, data);
    }

    public static JSPropertyDescriptor Function(
        JSValue name,
        JSCallback method,
        JSPropertyAttributes attributes = JSPropertyAttributes.Default,
        object? data = null)
    {
        return new JSPropertyDescriptor(name, method, null, null, null, attributes, data);
    }
}
