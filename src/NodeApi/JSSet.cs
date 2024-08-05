// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSSet : IJSValue<JSSet>, ISet<JSValue>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSSet" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSSet" /> to convert.</param>
    public static implicit operator JSValue(JSSet value) => value.AsJSValue();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSSet" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSSet" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSSet?(JSValue value) => value.As<JSSet>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSSet" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSSet" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSSet(JSValue value) => value.CastTo<JSSet>();

    public static explicit operator JSSet(JSObject obj) => (JSSet)(JSValue)obj;
    public static implicit operator JSObject(JSSet set) => (JSObject)set._value;

    public static explicit operator JSSet(JSIterable obj) => (JSSet)(JSValue)obj;
    public static implicit operator JSIterable(JSSet set) => (JSIterable)set._value;


    private JSSet(JSValue value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a new empty JS Set.
    /// </summary>
    public JSSet()
    {
        _value = JSRuntimeContext.Current.Import(null, "Set").CallAsConstructor();
    }

    /// <summary>
    /// Creates a new JS Set with values from an iterable (such as another set).
    /// </summary>
    public JSSet(JSIterable iterable)
    {
        _value = JSRuntimeContext.Current.Import(null, "Set").CallAsConstructor(iterable);
    }

    #region IJSValue<JSSet> implementation

    /// <summary>
    /// Determines whether a <see cref="JSSet" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSSet" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
    public static bool CanCreateFrom(JSValue value)
        => value.IsObject() && value.InstanceOf(JSValue.Global["Set"]);

    /// <summary>
    /// Creates a new instance of <see cref="JSSet" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSSet" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSSet" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSSet IJSValue<JSSet>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSSet CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    /// <summary>
    /// Converts the <see cref="JSSet" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <returns>
    /// The <see cref="JSValue" /> representation of the <see cref="JSSet" />.
    /// </returns>
    public JSValue AsJSValue() => _value;

    #endregion

    public int Count => (int)_value["size"];

    bool ICollection<JSValue>.IsReadOnly => false;

    public JSIterable.Enumerator GetEnumerator() => new(_value);

    IEnumerator<JSValue> IEnumerable<JSValue>.GetEnumerator() => GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Add(JSValue item)
    {
        int countBeforeAdd = Count;
        _value.CallMethod("add", item);
        return Count > countBeforeAdd;
    }

    void ICollection<JSValue>.Add(JSValue item)
    {
        _value.CallMethod("add", item);
    }

    public bool Remove(JSValue item) => (bool)_value.CallMethod("delete", item);

    public void Clear() => _value.CallMethod("clear");

    public bool Contains(JSValue item) => (bool)_value.CallMethod("has", item);

    public void CopyTo(JSValue[] array, int arrayIndex)
    {
        int i = arrayIndex;
        foreach (JSValue item in this)
        {
            array[i++] = item;
        }
    }

    public void ExceptWith(IEnumerable<JSValue> other)
    {
        foreach (JSValue item in other)
        {
            Remove(item);
        }
    }

    public void IntersectWith(IEnumerable<JSValue> other)
    {
        List<JSValue> itemsToRemove = new();
        foreach (JSValue item in this)
        {
            if (!other.Contains(item))
            {
                itemsToRemove.Add(item);
            }
        }

        foreach (JSValue item in itemsToRemove)
        {
            Remove(item);
        }
    }

    public bool IsProperSubsetOf(IEnumerable<JSValue> other)
    {
        JSSet thisSet = this; // Required for anonymous lambda.
        return IsSubsetOf(other) && other.Any((item) => !thisSet.Contains(item));
    }

    public bool IsProperSupersetOf(IEnumerable<JSValue> other)
    {
        if (!IsSupersetOf(other))
        {
            return false;
        }

        // Not using this.Any() to avoid boxing.
        foreach (JSValue item in this)
        {
            if (!other.Contains(item))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSubsetOf(IEnumerable<JSValue> other)
    {
        // Not using this.All() to avoid boxing.
        foreach (JSValue item in this)
        {
            if (!other.Contains(item))
            {
                return false;
            }
        }
        return true;
    }

    public bool IsSupersetOf(IEnumerable<JSValue> other)
    {
        JSSet thisSet = this; // Required for anonymous lambda.
        return other.All((item) => thisSet.Contains(item));
    }

    public bool Overlaps(IEnumerable<JSValue> other)
    {
        JSSet thisSet = this; // Required for anonymous lambda.
        return other.Any((item) => thisSet.Contains(item));
    }

    public bool SetEquals(IEnumerable<JSValue> other)
        => IsSubsetOf(other) && IsSupersetOf(other);

    public void SymmetricExceptWith(IEnumerable<JSValue> other)
    {
        foreach (JSValue item in other)
        {
            if (!Remove(item))
            {
                Add(item);
            }
        }
    }

    public void UnionWith(IEnumerable<JSValue> other)
    {
        foreach (JSValue item in other)
        {
            _value.CallMethod("add", item);
        }
    }

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSSet a, JSSet b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSSet a, JSSet b) => !a._value.StrictEquals(b);

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
