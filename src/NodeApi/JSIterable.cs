// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSIterable : IEnumerable<JSValue>, IEquatable<JSValue>
#if NET7_0_OR_GREATER
    , IJSValue<JSIterable>
#endif
{
    private readonly JSValue _value;

    public static implicit operator JSValue(JSIterable value) => value.AsJSValue();
    public static explicit operator JSIterable?(JSValue value) => value.As<JSIterable>();
    public static explicit operator JSIterable(JSValue value)
        => value.As<JSIterable>() ?? throw new InvalidCastException("JSValue is not an Iterable.");

    public static explicit operator JSIterable(JSObject obj) => (JSIterable)(JSValue)obj;
    public static implicit operator JSObject(JSIterable iterable) => (JSObject)iterable._value;

    private JSIterable(JSValue value)
    {
        _value = value;
    }

    #region IJSValue<JSIterable> implementation

    //TODO: (vmoroz) implement proper check using Symbol.iterator
    public static bool CanBeConvertedFrom(JSValue value) => value.IsObject();

    public static JSIterable CreateUnchecked(JSValue value) => new(value);

    #endregion

    public JSValue AsJSValue() => _value;

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
