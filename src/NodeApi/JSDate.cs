// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public readonly struct JSDate : IJSValue<JSDate>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSDate" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSDate" /> to convert.</param>
    public static implicit operator JSValue(JSDate value) => value.AsJSValue();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSDate" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSDate" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSDate?(JSValue value) => value.As<JSDate>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSDate" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSDate" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSDate(JSValue value) => value.CastTo<JSDate>();

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

    #region IJSValue<JSDate> implementation

    /// <summary>
    /// Determines whether a <see cref="JSDate" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSDate" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
    public static bool CanCreateFrom(JSValue value) => value.IsDate();

    /// <summary>
    /// Creates a new instance of <see cref="JSDate" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSDate" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSDate" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSDate IJSValue<JSDate>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSDate CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    /// <summary>
    /// Converts the <see cref="JSDate" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <returns>
    /// The <see cref="JSValue" /> representation of the <see cref="JSDate" />.
    /// </returns>
    public JSValue AsJSValue() => _value;

    #endregion


    public static JSDate FromDateTime(DateTime value)
    {
        DateTimeKind kind = value.Kind;

        // JS Date values are always represented with a underlying UTC epoch value,
        // so local times must be converted to UTC. Unspecified kind is treated as local.
        if (kind != DateTimeKind.Utc)
        {
            value = value.ToUniversalTime();
        }

        long dateValue = new DateTimeOffset(value).ToUnixTimeMilliseconds();
        JSDate jsDate = new(dateValue);

        // Add an extra property that allows round-tripping the DateTimeKind.
        jsDate._value.SetProperty("kind", kind.ToString().ToLowerInvariant());

        return jsDate;
    }

    public DateTime ToDateTime()
    {
        // JS Date values are always represented with a underlying UTC epoch value.
        // FromUnixTimeMilliseconds expects a value in UTC and produces a result with 0 offset.
        DateTimeOffset utcValue = DateTimeOffset.FromUnixTimeMilliseconds(DateValue);
        DateTime value = utcValue.UtcDateTime;

        // Check for the kind hint. If absent, default to UTC, not Unspecified.
        JSValue kindHint = _value.GetProperty("kind");
        if (kindHint.IsString() && Enum.TryParse((string)kindHint, true, out DateTimeKind kind) &&
            kind != DateTimeKind.Utc)
        {
            value = DateTime.SpecifyKind(value.ToLocalTime(), kind);
        }

        return value;
    }

    public static JSDate FromDateTimeOffset(DateTimeOffset value)
    {
        long dateValue = value.ToUnixTimeMilliseconds();
        JSDate jsDate = new(dateValue);

        jsDate._value.SetProperty("offset", value.Offset.TotalMinutes);
        jsDate._value.SetProperty("toString", new JSFunction(JSDateWithOffsetToString));

        return jsDate;
    }

    private static JSValue JSDateWithOffsetToString(JSCallbackArgs args)
    {
        JSValue thisDate = args.ThisArg;
        JSValue value = thisDate.CallMethod("valueOf");
        JSValue offset = thisDate.GetProperty("offset");

        if (!offset.IsNumber() || !value.IsNumber() || double.IsNaN((double)value))
        {
            JSValue dateClass = JSRuntimeContext.Current.Import(null, "Date");
            return dateClass.GetProperty("prototype").GetProperty("toString").Call(thisDate);
        }

        // Call toISOString on another Date instance with the offset applied.
        int offsetValue = (int)offset;
        JSDate offsetDate = new((long)thisDate.CallMethod("valueOf") + offsetValue * 60 * 1000);
        JSValue isoString = offsetDate._value.CallMethod("toISOString");

        string offsetSign = offsetValue < 0 ? "-" : "+";
        offsetValue = Math.Abs(offsetValue);
        int offsetHours = offsetValue / 60;
        int offsetMinutes = offsetValue % 60;

        // Convert the ISO string to a string with the offset.
        return ((string)isoString).Replace("T", " ").Replace("Z", "") + " " + offsetSign +
            offsetHours.ToString("D2") + ":" + offsetMinutes.ToString("D2");
    }

    public DateTimeOffset ToDateTimeOffset()
    {
        JSValue offset = _value.GetProperty("offset");
        if (offset.IsNumber())
        {
            // FromUnixTimeMilliseconds expects a value in UTC and produces a result with 0 offset.
            // The offset must be added to UTC when constructing the DateTimeOffset.
            DateTimeOffset utcValue = DateTimeOffset.FromUnixTimeMilliseconds(DateValue);
            TimeSpan offsetTime = TimeSpan.FromMinutes((double)offset);
            return new DateTimeOffset(
                new DateTime(utcValue.DateTime.Add(offsetTime).Ticks),
                offsetTime);
        }
        else
        {
            return new DateTimeOffset(ToDateTime());
        }
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
