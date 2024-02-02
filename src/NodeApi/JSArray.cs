// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSArray : IList<JSValue>, IEquatable<JSValue>
#if NET7_0_OR_GREATER
    , IJSValue<JSArray>
#endif
{
    private readonly JSValue _value;

    public static implicit operator JSValue(JSArray arr) => arr.AsJSValue();
    public static explicit operator JSArray?(JSValue value) => value.As<JSArray>();
    public static explicit operator JSArray(JSValue value)
        => value.As<JSArray>() ?? throw new InvalidCastException("JSValue is not an Array");

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

    public JSArray(JSValue[] array)
    {
        // Do not specify the length because it will create a sparse array
        // that prevents some V8 optimizations.
        _value = JSValue.CreateArray();
        for (int i = 0; i < array.Length; i++)
        {
            _value.SetElement(i, array[i]);
        }
    }

    #region IJSValue<JSArray> implementation

    public static bool CanBeConvertedFrom(JSValue value) => value.IsArray();

    public static JSArray CreateUnchecked(JSValue value) => new(value);

    #endregion

    public JSValue AsJSValue() => _value;

    /// <inheritdoc/>
    public int Length => _value.GetArrayLength();

    int ICollection<JSValue>.Count => _value.GetArrayLength();

    bool ICollection<JSValue>.IsReadOnly => false;

    public JSValue this[int index]
    {
        get => _value.GetElement(index);
        set => _value.SetElement(index, value);
    }

    /// <inheritdoc/>
    public void Add(JSValue item) => _value["push"].Call(_value, item);

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public Enumerator GetEnumerator() => new(_value);

    IEnumerator<JSValue> IEnumerable<JSValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc/>
    public int IndexOf(JSValue item) => (int)_value.CallMethod("indexOf", item);

    /// <inheritdoc/>
    public void Insert(int index, JSValue item) => _value.CallMethod("splice", index, 0, item);

    /// <inheritdoc/>
    public void RemoveAt(int index) => _value.CallMethod("splice", index, 1);

    /// <inheritdoc/>
    public void Clear() => _value.CallMethod("splice", 0, Length);

    /// <inheritdoc/>
    public bool Contains(JSValue item) => IndexOf(item) >= 0;

    /// <inheritdoc/>
    public bool Remove(JSValue item)
    {
        int index = IndexOf(item);
        if (index < 0) return false;
        RemoveAt(index);
        return true;
    }

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

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is JSValue other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        throw new NotSupportedException(
            "Hashing JS values is not supported. Use JSSet or JSMap instead.");
    }
}
