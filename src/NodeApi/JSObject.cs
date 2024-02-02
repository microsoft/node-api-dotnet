// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSObject : IDictionary<JSValue, JSValue>, IEquatable<JSValue>
#if NET7_0_OR_GREATER
    , IJSValue<JSObject>
#endif
{
    private readonly JSValue _value;

    public static implicit operator JSValue(JSObject value) => value.AsJSValue();
    public static explicit operator JSObject?(JSValue value) => value.As<JSMap>();
    public static explicit operator JSObject(JSValue value)
        => value.As<JSObject>() ?? throw new InvalidCastException("JSValue is not an Object.");

    private JSObject(JSValue value)
    {
        _value = value;
    }

    public JSObject() : this(JSValue.CreateObject())
    {
    }

    #region IJSValue<JSObject> implementation

    public static bool CanBeConvertedFrom(JSValue value) => value.IsObject();

    public static JSObject CreateUnchecked(JSValue value) => new(value);

    #endregion

    public JSValue AsJSValue() => _value;

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
