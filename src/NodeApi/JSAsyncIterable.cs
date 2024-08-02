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

    public static implicit operator JSValue(JSAsyncIterable value) => value.AsJSValue();
    public static explicit operator JSAsyncIterable?(JSValue value) => value.As<JSAsyncIterable>();
    public static explicit operator JSAsyncIterable(JSValue value)
        => value.CastTo<JSAsyncIterable>();

    public static explicit operator JSAsyncIterable(JSObject obj) => (JSAsyncIterable)(JSValue)obj;
    public static implicit operator JSObject(JSAsyncIterable iterable) => (JSObject)iterable._value;

    private JSAsyncIterable(JSValue value)
    {
        _value = value;
    }

    #region IJSValue<JSAsyncIterable> implementation

    public static bool CanCreateFrom(JSValue value)
        => value.IsObject() && value.HasProperty(JSValue.Global["Symbol"]["asyncIterator"]);

#if NET7_0_OR_GREATER
    static JSAsyncIterable IJSValue<JSAsyncIterable>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSAsyncIterable CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

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
