using System.Collections;
using System.Collections.Generic;

namespace NodeApi;

public struct JSArray : IEnumerable<JSValue>, IEnumerable
{
    private JSValue _value;

    public static explicit operator JSArray(JSValue value) => new() { _value = value };
    public static implicit operator JSValue(JSArray arr) => arr._value;

    public static explicit operator JSArray(JSObject obj) => (JSArray)(JSValue)obj;
    public static implicit operator JSObject(JSArray arr) => (JSObject)arr._value;

    public JSArray()
    {
        _value = JSValue.CreateArray();
    }

    public JSArray(int length)
    {
        _value = JSValue.CreateArray(length);
    }

    public int Length => _value.GetArrayLength();

    public JSValue this[int index]
    {
        get => _value.GetElement(index);
        set => _value.SetElement(index, value);
    }

    public void Add(JSValue item) => _value["push"].Call(_value, item);

    public JSArrayItemEnumerator GetEnumerator() => new(_value);

    IEnumerator<JSValue> IEnumerable<JSValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
