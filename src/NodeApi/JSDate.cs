// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSDate : IEquatable<JSValue>
{
    private readonly JSValue _value;

    public static explicit operator JSDate(JSValue value) => new(value);
    public static implicit operator JSValue(JSDate date) => date._value;

    private JSDate(JSValue value)
    {
        _value = value;
    }

    public JSDate()
    {
        _value = JSRuntimeContext.Current.Import(null, "Date").CallAsConstructor();
    }

    public JSDate(long dateValue)
    {
        _value = JSRuntimeContext.Current.Import(null, "Date").CallAsConstructor(dateValue);
    }

    public JSDate(string dateString)
    {
        _value = JSRuntimeContext.Current.Import(null, "Date").CallAsConstructor(dateString);
    }

    public long DateValue => (long)_value.CallMethod("valueOf");

    public static JSDate FromDateTime(DateTime value)
    {
        long dateValue = new DateTimeOffset(value.ToUniversalTime())
            .ToUnixTimeMilliseconds();
        return new JSDate(dateValue);
    }

    public DateTime ToDateTime()
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(DateValue).UtcDateTime;
    }

    public static JSDate FromDateTimeOffset(DateTimeOffset value)
    {
        long dateValue = value.ToUnixTimeMilliseconds();
        return new JSDate(dateValue);
    }

    public DateTimeOffset ToDateTimeOffset()
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(DateValue);
    }

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSDate a, JSDate b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSDate a, JSDate b) => !a._value.StrictEquals(b);

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
