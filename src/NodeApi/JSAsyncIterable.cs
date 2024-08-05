// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.JavaScript.NodeApi;

public readonly partial struct JSAsyncIterable :
    IJSValue<JSAsyncIterable>, IAsyncEnumerable<JSValue>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSAsyncIterable" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSAsyncIterable" /> to convert.</param>
    public static implicit operator JSValue(JSAsyncIterable value) => value.AsJSValue();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSAsyncIterable" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSAsyncIterable" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSAsyncIterable?(JSValue value) => value.As<JSAsyncIterable>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSAsyncIterable" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSAsyncIterable" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSAsyncIterable(JSValue value)
        => value.CastTo<JSAsyncIterable>();

    public static explicit operator JSAsyncIterable(JSObject obj) => (JSAsyncIterable)(JSValue)obj;
    public static implicit operator JSObject(JSAsyncIterable iterable) => (JSObject)iterable._value;

    private JSAsyncIterable(JSValue value)
    {
        _value = value;
    }

    #region IJSValue<JSAsyncIterable> implementation

    /// <summary>
    /// Determines whether a <see cref="JSAsyncIterable" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSAsyncIterable" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
    public static bool CanCreateFrom(JSValue value)
        => value.IsObject() && value.HasProperty(JSSymbol.AsyncIterator);

    /// <summary>
    /// Creates a new instance of <see cref="JSAsyncIterable" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSAsyncIterable" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSAsyncIterable" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSAsyncIterable IJSValue<JSAsyncIterable>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSAsyncIterable CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    /// <summary>
    /// Converts the <see cref="JSAsyncIterable" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <returns>
    /// The <see cref="JSValue" /> representation of the <see cref="JSAsyncIterable" />.
    /// </returns>
    public JSValue AsJSValue() => _value;

    #endregion

#pragma warning disable IDE0060 // Unused parameter
    public Enumerator GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new(_value);
#pragma warning restore IDE0060

    IAsyncEnumerator<JSValue> IAsyncEnumerable<JSValue>.GetAsyncEnumerator(
        CancellationToken cancellationToken)
        => GetAsyncEnumerator(cancellationToken);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSAsyncIterable a, JSAsyncIterable b)
        => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSAsyncIterable a, JSAsyncIterable b)
        => !a._value.StrictEquals(b);

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
