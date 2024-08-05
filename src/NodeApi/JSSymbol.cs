// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSSymbol : IJSValue<JSSymbol>
{
    private readonly JSValue _value;

    //TODO: [vmoroz] This is a bug. we must never use static variables for JSReference or JSValue
    private static readonly Lazy<JSReference> s_iteratorSymbol =
        new(() => new JSReference(JSValue.Global["Symbol"]["iterator"]));
    private static readonly Lazy<JSReference> s_asyncIteratorSymbol =
        new(() => new JSReference(JSValue.Global["Symbol"]["asyncIterator"]));

    /// <summary>
    /// Implicitly converts a <see cref="JSSymbol" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSSymbol" /> to convert.</param>
    public static implicit operator JSValue(JSSymbol value) => value.AsJSValue();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSSymbol" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSSymbol" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSSymbol?(JSValue value) => value.As<JSSymbol>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSSymbol" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSSymbol" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSSymbol(JSValue value) => value.CastTo<JSSymbol>();

    private JSSymbol(JSValue value)
    {
        _value = value;
    }

    public JSSymbol(string? description = null)
    {
        _value = JSValue.CreateSymbol(description ?? JSValue.Undefined);
    }

    #region IJSValue<JSSymbol> implementation

    /// <summary>
    /// Converts the <see cref="JSSymbol" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <returns>
    /// The <see cref="JSValue" /> representation of the <see cref="JSSymbol" />.
    /// </returns>
    public JSValue AsJSValue() => _value;

    /// <summary>
    /// Determines whether a <see cref="JSSymbol" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSSymbol" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
#if NET7_0_OR_GREATER
    static bool IJSValue<JSSymbol>.CanCreateFrom(JSValue value)
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static bool CanCreateFrom(JSValue value)
#pragma warning restore IDE0051
#endif
        => value.IsSymbol();

    /// <summary>
    /// Creates a new instance of <see cref="JSSymbol" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSSymbol" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSSymbol" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSSymbol IJSValue<JSSymbol>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSSymbol CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    #endregion

    /// <summary>
    /// Gets the symbol's description, or null if it does not have one.
    /// </summary>
    public string? Description
    {
        get
        {
            JSValue descriptionValue = _value["description"];
            return descriptionValue.IsString() ? (string)descriptionValue : null;
        }
    }

    public static JSSymbol For(string name)
    {
        return new JSSymbol(JSValue.SymbolFor(name));
    }

    public static JSSymbol Iterator => (JSSymbol)s_iteratorSymbol.Value.GetValue()!;

    public static JSSymbol AsyncIterator => (JSSymbol)s_asyncIteratorSymbol.Value.GetValue()!;

    // TODO: Add static properties for other well-known symbols.

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSSymbol a, JSSymbol b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSSymbol a, JSSymbol b) => !a._value.StrictEquals(b);

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
