// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSMap : IDictionary<JSValue, JSValue>, IEquatable<JSValue>
{
    private readonly JSValue _value;

    public static explicit operator JSMap(JSValue value) => new(value);
    public static implicit operator JSValue(JSMap map) => map._value;

    public static explicit operator JSMap(JSObject obj) => (JSMap)(JSValue)obj;
    public static implicit operator JSObject(JSMap map) => (JSObject)map._value;

    private JSMap(JSValue value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a new empty JS Map.
    /// </summary>
    public JSMap()
    {
        _value = JSRuntimeContext.Current.Import(null, "Map").CallAsConstructor();
    }

    /// <summary>
    /// Creates a new JS Map with entries from an iterable (such as another map) whose elements
    /// are key-value pairs.
    /// </summary>
    public JSMap(JSIterable iterable)
    {
        _value = JSRuntimeContext.Current.Import(null, "Map").CallAsConstructor(iterable);
    }

    public int Count => (int)_value["size"];

    public Enumerator GetEnumerator() => new(_value);

    IEnumerator<KeyValuePair<JSValue, JSValue>> IEnumerable<KeyValuePair<JSValue, JSValue>>.GetEnumerator() => GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public JSValue this[JSValue key]
    {
        get
        {
            JSValue value = _value.CallMethod("get", key);
            if (value.IsUndefined())
            {
                throw new KeyNotFoundException();
            }
            return value;
        }
        set
        {
            _value.CallMethod("set", key, value);
        }
    }

    public bool TryGetValue(JSValue key, [MaybeNullWhen(false)] out JSValue value)
    {
        value = _value.CallMethod("get", key);
        return !value.IsUndefined();
    }

    public JSMap.Collection Keys => new((JSIterable)_value["keys"], GetCount);

    public JSMap.Collection Values => new((JSIterable)_value["values"], GetCount);

    private int GetCount() => Count;

    public bool ContainsKey(JSValue key) => (bool)_value.CallMethod("has", key);

    public void Add(JSValue key, JSValue value) => this[key] = value;

    public bool Remove(JSValue key) => (bool)_value.CallMethod("delete", key);

    public void Clear() => _value.CallMethod("clear");

    ICollection<JSValue> IDictionary<JSValue, JSValue>.Keys => Keys;

    ICollection<JSValue> IDictionary<JSValue, JSValue>.Values => Values;

    bool ICollection<KeyValuePair<JSValue, JSValue>>.IsReadOnly => false;

    void ICollection<KeyValuePair<JSValue, JSValue>>.Add(KeyValuePair<JSValue, JSValue> item)
        => Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<JSValue, JSValue>>.Contains(KeyValuePair<JSValue, JSValue> item)
        => TryGetValue(item.Key, out JSValue value) && item.Value.Equals(value);

    void ICollection<KeyValuePair<JSValue, JSValue>>.CopyTo(
        KeyValuePair<JSValue, JSValue>[] array, int arrayIndex)
    {
        int i = arrayIndex;
        foreach (KeyValuePair<JSValue, JSValue> pair in this)
        {
            array[i++] = pair;
        }
    }

    bool ICollection<KeyValuePair<JSValue, JSValue>>.Remove(KeyValuePair<JSValue, JSValue> item)
        => TryGetValue(item.Key, out JSValue value) && item.Value.Equals(value) && Remove(item.Key);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSMap a, JSMap b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSMap a, JSMap b) => !a._value.StrictEquals(b);

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

    public readonly struct Collection : ICollection<JSValue>, IReadOnlyCollection<JSValue>
    {
        private readonly JSIterable _iterable;
        private readonly Func<int> _getCount;

        internal Collection(JSIterable iterable, Func<int> getCount)
        {
            _iterable = iterable;
            _getCount = getCount;
        }

        public int Count => _getCount();

        public bool IsReadOnly => true;

        public IEnumerator<JSValue> GetEnumerator() => _iterable.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();

        public bool Contains(JSValue item) => throw new NotImplementedException();

        public void CopyTo(JSValue[] array, int arrayIndex)
        {
            int i = arrayIndex;
            foreach (JSValue item in this)
            {
                array[i++] = item;
            }
        }

        public void Add(JSValue item) => throw new NotSupportedException();
        public bool Remove(JSValue item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }
}
