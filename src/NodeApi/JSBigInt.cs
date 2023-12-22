// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSBigInt : IEquatable<JSValue>
{
    private readonly JSValue _value;

    public static implicit operator JSValue(JSBigInt value) => value.AsJSValue();
    public static explicit operator JSBigInt?(JSValue value) => value.AsJSBigInt();
    public static explicit operator JSBigInt(JSValue value)
        => value.AsJSBigInt() is JSBigInt result
            ? result
            : throw new InvalidCastException("JSValue is not BigInt");

    public static implicit operator JSBigInt(BigInteger value) => new(value);
    public static explicit operator BigInteger(JSBigInt value) => value.ToBigInteger();

    private JSBigInt(JSValue value)
    {
        _value = value;
    }

    public JSBigInt(long value) : this(JSValue.CreateBigInt(value))
    {
    }

    public JSBigInt(ulong value) : this(JSValue.CreateBigInt(value))
    {
    }

    public JSBigInt(int sign, ReadOnlySpan<ulong> words)
        : this(JSValue.CreateBigInt(sign, words))
    {
    }

    public JSBigInt(BigInteger value) : this(JSValue.CreateBigInt(value))
    {
    }

    public static JSBigInt CreateUnchecked(JSValue value) => new(value);

    public int GetWordCount() => _value.GetBigIntWordCount();

    public void CopyTo(Span<ulong> destination, out int sign, out int wordCount)
        => _value.GetBigIntWords(destination, out sign, out wordCount);

    public JSValue AsJSValue() => _value;

    public BigInteger ToBigInteger() => _value.ToBigInteger();

    public long ToInt64(out bool isLossless) => _value.ToInt64BigInt(out isLossless);

    public ulong ToUInt64(out bool isLossless) => _value.ToUInt64BigInt(out isLossless);

    public unsafe ulong[] ToUInt64Array(out int sign) => _value.GetBigIntWords(out sign);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSBigInt left, JSBigInt right)
        => left._value.StrictEquals(right._value);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSBigInt left, JSBigInt right)
        => !left._value.StrictEquals(right._value);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public bool Equals(JSValue other) => _value.StrictEquals(other);

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? obj)
        => obj is JSBigInt other && Equals(other)
        || obj is JSValue otherValue && _value.StrictEquals(otherValue);

    /// <inheritdoc/>
    public override int GetHashCode()
        => throw new NotSupportedException(
            "Hashing JS values is not supported. Use JSSet or JSMap instead.");
}
