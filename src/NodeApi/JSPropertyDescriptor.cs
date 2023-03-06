using System;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSPropertyDescriptor
{
    public JSValue Name { get; }
    public JSCallback? Method { get; } = null;
    public JSCallback? Getter { get; } = null;
    public JSCallback? Setter { get; } = null;
    public JSValue? Value { get; } = null;
    public JSPropertyAttributes Attributes { get; } = JSPropertyAttributes.Default;
    public object? Data { get; } = null;

    public JSPropertyDescriptor(
        JSValue name,
        JSCallback? method = null,
        JSCallback? getter = null,
        JSCallback? setter = null,
        JSValue? value = null,
        JSPropertyAttributes attributes = JSPropertyAttributes.Default,
        object? data = null)
    {
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
