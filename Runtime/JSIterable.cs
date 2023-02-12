using System.Collections;
using System.Collections.Generic;

namespace NodeApi;

public readonly partial struct JSIterable : IEnumerable<JSValue>
{
    private readonly JSValue _value;

    public static explicit operator JSIterable(JSValue value) => new(value);
    public static implicit operator JSValue(JSIterable iterable) => iterable._value;

    public static explicit operator JSArray(JSIterable iterable) => (JSArray)iterable._value;
    public static implicit operator JSIterable(JSArray array) => (JSIterable)(JSValue)array;

    public static explicit operator JSIterable(JSObject obj) => (JSIterable)(JSValue)obj;
    public static implicit operator JSObject(JSIterable iterable) => (JSObject)iterable._value;

    private JSIterable(JSValue value)
    {
        _value = value;
    }

    public Enumerator GetEnumerator() => new(_value);

    IEnumerator<JSValue> IEnumerable<JSValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
