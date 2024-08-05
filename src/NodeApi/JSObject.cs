// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSObject : IJSValue<JSObject>, IDictionary<JSValue, JSValue>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSObject" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSObject" /> to convert.</param>
    public static implicit operator JSValue(JSObject value) => value.AsJSValue();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSObject" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSObject" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSObject?(JSValue value) => value.As<JSObject>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSObject" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSObject" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSObject(JSValue value) => value.CastTo<JSObject>();

    private JSObject(JSValue value)
    {
        _value = value;
    }

    public JSObject() : this(JSValue.CreateObject())
    {
    }

    #region IJSValue<JSObject> implementation

    /// <summary>
    /// Determines whether a <see cref="JSObject" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSObject" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
    public static bool CanCreateFrom(JSValue value) => value.IsObject();

    /// <summary>
    /// Creates a new instance of <see cref="JSObject" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSObject" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSObject" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSObject IJSValue<JSObject>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSObject CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    /// <summary>
    /// Converts the <see cref="JSObject" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <returns>
    /// The <see cref="JSValue" /> representation of the <see cref="JSObject" />.
    /// </returns>
    public JSValue AsJSValue() => _value;

    #endregion

    public JSObject(IEnumerable<KeyValuePair<JSValue, JSValue>> properties) : this()
    {
        foreach (KeyValuePair<JSValue, JSValue> property in properties)
        {
            _value.SetProperty(property.Key, property.Value);
        }
    }

    public JSObject(params KeyValuePair<JSValue, JSValue>[] properties)
        : this((IEnumerable<KeyValuePair<JSValue, JSValue>>)properties)
    {
    }

    int ICollection<KeyValuePair<JSValue, JSValue>>.Count
        => _value.GetPropertyNames().GetArrayLength();

    bool ICollection<KeyValuePair<JSValue, JSValue>>.IsReadOnly => false;

    public void DefineProperties(params JSPropertyDescriptor[] descriptors)
    {
        _value.DefineProperties(descriptors);
    }

    public void DefineProperties(IReadOnlyCollection<JSPropertyDescriptor> descriptors)
    {
        _value.DefineProperties(descriptors);
    }

    public JSObject Wrap(object target)
    {
        _value.Wrap(target);
        return this;
    }

    public bool TryUnwrap<T>(out T? target) where T : class
    {
        if (_value.TryUnwrap() is object unwrapped)
        {
            target = unwrapped as T;
            return true;
        }

        target = null;
        return false;
    }

    public T Unwrap<T>() where T : class
    {
        return (T)_value.Unwrap(typeof(T).Name);
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

    public JSValue CallMethod(JSValue methodName)
        => _value.GetProperty(methodName).Call(_value);

    public JSValue CallMethod(JSValue methodName, JSValue arg0)
        => _value.GetProperty(methodName).Call(_value, arg0);

    public JSValue CallMethod(JSValue methodName, JSValue arg0, JSValue arg1)
        => _value.GetProperty(methodName).Call(_value, arg0, arg1);

    public JSValue CallMethod(JSValue methodName, JSValue arg0, JSValue arg1, JSValue arg2)
        => _value.GetProperty(methodName).Call(_value, arg0, arg1, arg2);

    public JSValue CallMethod(JSValue methodName, params JSValue[] args)
        => _value.GetProperty(methodName).Call(_value, args);

    public JSValue CallMethod(JSValue methodName, ReadOnlySpan<JSValue> args)
        => _value.GetProperty(methodName).Call(_value, args);

    public void Add(JSValue key, JSValue value) => _value.SetProperty(key, value);

    public bool ContainsKey(JSValue key) => _value.HasProperty(key);

    public bool Remove(JSValue key) => _value.DeleteProperty(key);

    public bool TryGetValue(JSValue key, [MaybeNullWhen(false)] out JSValue value)
    {
        value = _value.GetProperty(key);
        return !value.IsUndefined();
    }

    public void Add(KeyValuePair<JSValue, JSValue> item) => _value.SetProperty(item.Key, item.Value);

    public bool Contains(KeyValuePair<JSValue, JSValue> item) => _value.HasProperty(item.Key);

    public void CopyTo(KeyValuePair<JSValue, JSValue>[] array, int arrayIndex)
    {
        int i = arrayIndex;
        foreach (KeyValuePair<JSValue, JSValue> item in this)
        {
            array[i++] = item;
        }
    }

    public bool Remove(KeyValuePair<JSValue, JSValue> item) => _value.DeleteProperty(item.Key);

    public Enumerator GetEnumerator() => new(_value);

    public ICollection<JSValue> Keys => (JSArray)_value.GetPropertyNames();

    ICollection<JSValue> IDictionary<JSValue, JSValue>.Values => throw new NotSupportedException();

    void ICollection<KeyValuePair<JSValue, JSValue>>.Clear() => throw new NotSupportedException();

    IEnumerator<KeyValuePair<JSValue, JSValue>> IEnumerable<KeyValuePair<JSValue, JSValue>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSObject a, JSObject b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSObject a, JSObject b) => !a._value.StrictEquals(b);

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
