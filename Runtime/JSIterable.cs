using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NodeApi;

public readonly partial struct JSIterable : IEnumerable<JSValue>, IJSValue
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

    JSValue IJSValue.Value => _value;

    public Enumerator GetEnumerator() => new(_value);

    IEnumerator<JSValue> IEnumerable<JSValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSIterable a, JSIterable b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSIterable a, JSIterable b) => !a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public bool Equals(JSValue other) => _value.StrictEquals(other);

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is JSValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        throw new NotSupportedException(
            "Hashing JS values is not supported. Use JSSet or JSMap instead.");
    }
}
