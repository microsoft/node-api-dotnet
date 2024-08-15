// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Represents a JavaScript AbortSignal object and supports conversion to and from
/// <see cref="CancellationToken" />.
/// </summary>
/// <remarks>
/// https://nodejs.org/api/globals.html#class-abortsignal
/// https://developer.mozilla.org/en-US/docs/Web/API/AbortSignal
/// </remarks>
public readonly struct JSAbortSignal : IJSValue<JSAbortSignal>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSAbortSignal" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSAbortSignal" /> to convert.</param>
    public static implicit operator JSValue(JSAbortSignal signal) => signal._value;

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSAbortSignal" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSAbortSignal" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSAbortSignal?(JSValue value) => value.As<JSAbortSignal>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSAbortSignal" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSAbortSignal" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSAbortSignal(JSValue value) => value.CastTo<JSAbortSignal>();

    public static explicit operator JSAbortSignal(JSObject obj) => (JSAbortSignal)(JSValue)obj;
    public static implicit operator JSObject(JSAbortSignal signal) => (JSObject)signal._value;

    private JSAbortSignal(JSValue value)
    {
        _value = value;
    }

    public static explicit operator CancellationToken(JSAbortSignal signal)
        => signal.ToCancellationToken();

    public static explicit operator CancellationToken(JSAbortSignal? signal)
        => signal?.ToCancellationToken() ?? default;

    public static explicit operator JSAbortSignal(CancellationToken cancellation)
        => FromCancellationToken(cancellation);

    public static explicit operator JSAbortSignal(CancellationToken? cancellation)
        => cancellation.HasValue ? FromCancellationToken(cancellation.Value) : default;

    #region IJSValue<JSAbortSignal> implementation

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
    /// Determines whether a <see cref="JSAbortSignal" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSAbortSignal" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
#if NET7_0_OR_GREATER
    static bool IJSValue<JSAbortSignal>.CanCreateFrom(JSValue value)
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static bool CanCreateFrom(JSValue value)
#pragma warning restore IDE0051
#endif
        => value.IsObject() && value.InstanceOf(JSValue.Global["AbortSignal"]);

    /// <summary>
    /// Creates a new instance of <see cref="JSAbortSignal" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSAbortSignal" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSAbortSignal" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSAbortSignal IJSValue<JSAbortSignal>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSAbortSignal CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    #endregion

    private CancellationToken ToCancellationToken()
    {
        if (!_value.IsObject())
        {
            return default;
        }
        else if ((bool)_value.GetProperty("aborted"))
        {
            return new CancellationToken(canceled: true);
        }
        else
        {
            CancellationTokenSource cancellationSource = new();
            _value.CallMethod("addEventListener", "abort", JSValue.CreateFunction("abort", (args) =>
            {
                cancellationSource.Cancel();
                return default;
            }));
            return cancellationSource.Token;
        }
    }

    private static JSAbortSignal FromCancellationToken(CancellationToken cancellation)
    {
        if (cancellation == default)
        {
            return default;
        }
        else if (cancellation.IsCancellationRequested)
        {
            JSValue abortSignalClass = JSValue.Global["AbortSignal"];
            if (abortSignalClass.IsFunction())
            {
                JSValue value = abortSignalClass.CallMethod("abort");
                return new JSAbortSignal(value);
            }
            else
            {
                // AbortSignal is not supported in this environment.
                return default;
            }
        }
        else
        {
            JSValue abortControllerClass = JSValue.Global["AbortController"];
            if (abortControllerClass.IsFunction())
            {
                JSValue controller = abortControllerClass.CallAsConstructor();
                JSReference controllerReference = new(controller);
                JSSynchronizationContext syncContext = JSSynchronizationContext.Current!;
                cancellation.Register(() => syncContext.Post(() =>
                {
                    controllerReference.GetValue().CallMethod("abort");
                    controllerReference.Dispose();
                }));
                return new JSAbortSignal(controller["signal"]);
            }
            else
            {
                // AbortController is not supported in this environment.
                return default;
            }
        }
    }

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSAbortSignal a, JSAbortSignal b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSAbortSignal a, JSAbortSignal b) => !a._value.StrictEquals(b);

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
