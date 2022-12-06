using System;

namespace NodeApi;

public class JSPropertyDescriptor
{
    public JSValue Name { get; }
    public JSCallback? Method { get; } = null;
    public JSCallback? Getter { get; } = null;
    public JSCallback? Setter { get; } = null;
    public JSValue? Value { get; } = null;
    public JSPropertyAttributes Attributes { get; } = JSPropertyAttributes.Default;

    public JSPropertyDescriptor(JSValue name, JSValue value, JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
    {
        Name = name;
        Value = value;
        Attributes = attributes;
    }

    public JSPropertyDescriptor(string name, JSValue value, JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
      : this(JSNativeApi.CreateStringUtf16(name), value, attributes)
    {
    }

    public JSPropertyDescriptor(JSValue name, JSCallback method, JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
    {
        Name = name;
        Method = method;
        Attributes = attributes;
    }

    public JSPropertyDescriptor(string name, JSCallback method, JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
      : this(JSNativeApi.CreateStringUtf16(name), method, attributes)
    {
    }

    public JSPropertyDescriptor(JSValue name, JSCallback? getter, JSCallback? setter, JSPropertyAttributes attributes = JSPropertyAttributes.Configurable)
    {
        if (getter == null && setter == null)
        {
            throw new ArgumentException($"Either `{nameof(getter)}` or `{nameof(setter)}` or both must be not null");
        }
        Name = name;
        Getter = getter;
        Setter = setter;
        Attributes = attributes;
    }

    public JSPropertyDescriptor(string name, JSCallback? getter, JSCallback? setter, JSPropertyAttributes attributes = JSPropertyAttributes.Configurable)
      : this(JSNativeApi.CreateStringUtf16(name), getter, setter, attributes)
    {
    }
}
