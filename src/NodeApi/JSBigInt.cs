// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSBigInt : IJSValue<JSBigInt>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSBigInt" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSBigInt" /> to convert.</param>
    public static implicit operator JSValue(JSBigInt value) => value._value;

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSBigInt" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSBigInt" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSBigInt?(JSValue value) => value.As<JSBigInt>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSBigInt" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSBigInt" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSBigInt(JSValue value) => value.CastTo<JSBigInt>();

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

    #region IJSValue<JSBigInt> implementation

    /// <summary>
    /// Checks if the T struct can be created from this instance`.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// `true` if the T struct can be created from this instance. Otherwise it returns `false`.
    /// </returns>
    public bool Is<T>() where T : struct, IJSValue<T> => _value.Is<T>();

    /// <summary>
    /// Tries to create a T struct from this instance.
    /// It returns `null` if the T struct cannot be created.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// Nullable value that contains T struct if it was successfully created
    /// or `null` if it was failed.
    /// </returns>
    public T? As<T>() where T : struct, IJSValue<T> => _value.As<T>();

    /// <summary>
    /// Creates a T struct from this instance without checking the enclosed handle type.
    /// It must be used only when the handle type is known to be correct.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    public T AsUnchecked<T>() where T : struct, IJSValue<T> => _value.AsUnchecked<T>();

    /// <summary>
    /// Creates a T struct from this instance.
    /// It throws `InvalidCastException` in case of failure.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be crated based on this instance.
    /// </exception>
    public T CastTo<T>() where T : struct, IJSValue<T> => _value.CastTo<T>();

    /// <summary>
    /// Determines whether a <see cref="JSBigInt" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSBigInt" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
#if NET7_0_OR_GREATER
    static bool IJSValue<JSBigInt>.CanCreateFrom(JSValue value)
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static bool CanCreateFrom(JSValue value)
#pragma warning restore IDE0051
#endif
        => value.IsBigInt();

    /// <summary>
    /// Creates a new instance of <see cref="JSBigInt" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSBigInt" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSBigInt" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSBigInt IJSValue<JSBigInt>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSBigInt CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    #endregion

    public int GetWordCount() => _value.GetBigIntWordCount();

    public void CopyTo(Span<ulong> destination, out int sign, out int wordCount)
        => _value.GetBigIntWords(destination, out sign, out wordCount);

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
