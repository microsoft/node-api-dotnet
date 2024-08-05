// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSArray : IJSValue<JSArray>, IList<JSValue>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSArray" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSArray" /> to convert.</param>
    public static implicit operator JSValue(JSArray arr) => arr.AsJSValue();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSArray" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSArray" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSArray?(JSValue value) => value.As<JSArray>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSArray" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSArray" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSArray(JSValue value) => value.CastTo<JSArray>();

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

    /// <summary>
    /// Determines whether a <see cref="JSArray" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSArray" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
    public static bool CanCreateFrom(JSValue value) => value.IsArray();

    /// <summary>
    /// Creates a new instance of <see cref="JSArray" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSArray" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSArray" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSArray IJSValue<JSArray>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSArray CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    /// <summary>
    /// Converts the <see cref="JSArray" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <returns>
    /// The <see cref="JSValue" /> representation of the <see cref="JSArray" />.
    /// </returns>
    public JSValue AsJSValue() => _value;

    #endregion

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

    public void AddRange(IEnumerable<JSValue> items)
    {
        foreach (JSValue item in items)
        {
            Add(item);
        }
    }

    public void AddRange<T>(IEnumerable<T> items, JSValue.From<T> toJS)
    {
        foreach (T item in items)
        {
            Add(toJS(item));
        }
    }

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
