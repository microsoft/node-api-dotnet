using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSArray : IList<JSValue>, IEquatable<JSValue>
{
    private readonly JSValue _value;

    public static explicit operator JSArray(JSValue value) => new(value);
    public static implicit operator JSValue(JSArray arr) => arr._value;

    public static explicit operator JSArray(JSObject obj) => (JSArray)(JSValue)obj;
    public static implicit operator JSObject(JSArray arr) => (JSObject)arr._value;

    public static explicit operator JSArray(JSIterable obj) => (JSArray)(JSValue)obj;
    public static implicit operator JSIterable(JSArray arr) => (JSIterable)arr._value;

    private JSArray(JSValue value)
    {
        _value = value;
    }

    public JSArray() : this(JSValue.CreateArray())
    {
    }

    public JSArray(int length) : this(JSValue.CreateArray(length))
    {
    }

    public int Length => _value.GetArrayLength();

    int ICollection<JSValue>.Count => _value.GetArrayLength();

    bool ICollection<JSValue>.IsReadOnly => false;

    public JSValue this[int index]
    {
        get => _value.GetElement(index);
        set => _value.SetElement(index, value);
    }

    public void Add(JSValue item) => _value["push"].Call(_value, item);

    public void CopyTo(JSValue[] array, int arrayIndex)
    {
        int i = arrayIndex;
        foreach (JSValue item in this)
        {
            array[i++] = item;
        }
    }

    /// <summary>
    /// Copies array elements to a destination array, converting each element from JS
    /// values using a conversion delegate.
    /// </summary>
    /// <param name="array">Destination array.</param>
    /// <param name="arrayIndex">Starting index in the destination array.</param>
    /// <param name="fromJS">Delegate that converts from JS value to array element type.</param>
    public void CopyTo<T>(T[] array, int arrayIndex, JSValue.To<T> fromJS)
    {
        int i = arrayIndex;
        foreach (JSValue item in this)
        {
            array[i++] = fromJS(item);
        }
    }

    /// <summary>
    /// Copies array elements from a source array, converting each element to JS values
    /// using a conversion delegate.
    /// </summary>
    /// <param name="array">Source array.</param>
    /// <param name="arrayIndex">Starting index in the destination array.</param>
    /// <param name="toJS">Delegate that converts from array element type to JS value.</param>
    public void CopyFrom<T>(T[] array, int arrayIndex, JSValue.From<T> toJS)
    {
        int i = arrayIndex;
        foreach (T item in array)
        {
            this[i++] = toJS(item);
        }
    }

    public Enumerator GetEnumerator() => new(_value);

    IEnumerator<JSValue> IEnumerable<JSValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    int IList<JSValue>.IndexOf(JSValue item) => throw new System.NotImplementedException();

    void IList<JSValue>.Insert(int index, JSValue item) => throw new System.NotImplementedException();

    void IList<JSValue>.RemoveAt(int index) => throw new System.NotImplementedException();

    void ICollection<JSValue>.Clear() => throw new System.NotImplementedException();

    bool ICollection<JSValue>.Contains(JSValue item) => throw new System.NotImplementedException();

    bool ICollection<JSValue>.Remove(JSValue item) => throw new System.NotImplementedException();

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSArray a, JSArray b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSArray a, JSArray b) => !a._value.StrictEquals(b);

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
