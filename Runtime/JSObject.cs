using System.Collections;
using System.Collections.Generic;

namespace NodeApi;

public struct JSObject : IEnumerable<(JSValue name, JSValue value)>, IEnumerable
{
    private JSValue _value;

    public static explicit operator JSObject(JSValue value) => new() { _value = value };
    public static implicit operator JSValue(JSObject obj) => obj._value;

    public JSObject()
    {
        _value = JSValue.CreateObject();
    }

    public JSValue this[JSValue name]
    {
        get => _value.GetProperty(name);
        set => _value.SetProperty(name, value);
    }

    public JSValue this[string name]
    {
        get => _value.GetProperty(name);
        set => _value.SetProperty(name, value);
    }

    public void Add(JSValue name, JSValue value)
        => _value.SetProperty(name, value);

    public void Add((JSValue Key, JSValue Value) entry)
        => _value.SetProperty(entry.Key, entry.Value);

    public JSObjectPropertyEnumerator GetEnumerator() => new(_value);

    IEnumerator<(JSValue name, JSValue value)> IEnumerable<(JSValue name, JSValue value)>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
