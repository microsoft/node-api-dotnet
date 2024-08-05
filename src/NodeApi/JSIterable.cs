// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSIterable : IJSValue<JSIterable>, IEnumerable<JSValue>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSIterable" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSIterable" /> to convert.</param>
    public static implicit operator JSValue(JSIterable value) => value.AsJSValue();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSIterable" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSIterable" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSIterable?(JSValue value) => value.As<JSIterable>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSIterable" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSIterable" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSIterable(JSValue value) => value.CastTo<JSIterable>();

    public static explicit operator JSIterable(JSObject obj) => (JSIterable)(JSValue)obj;
    public static implicit operator JSObject(JSIterable iterable) => (JSObject)iterable._value;

    private JSIterable(JSValue value)
    {
        _value = value;
    }

    #region IJSValue<JSIterable> implementation

    /// <summary>
    /// Determines whether a <see cref="JSIterable" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSIterable" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
    public static bool CanCreateFrom(JSValue value)
        => value.IsObject() && value.HasProperty(JSSymbol.Iterator);

    /// <summary>
    /// Creates a new instance of <see cref="JSIterable" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSIterable" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSIterable" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSIterable IJSValue<JSIterable>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSIterable CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    /// <summary>
    /// Converts the <see cref="JSIterable" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <returns>
    /// The <see cref="JSValue" /> representation of the <see cref="JSIterable" />.
    /// </returns>
    public JSValue AsJSValue() => _value;

    #endregion

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
