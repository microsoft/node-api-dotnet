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
public readonly struct JSAbortSignal : IEquatable<JSValue>
#if NET7_0_OR_GREATER
    , IJSValue<JSAbortSignal>
#endif
{
    private readonly JSValue _value;

    public static implicit operator JSValue(JSAbortSignal value) => value.AsJSValue();
    public static explicit operator JSAbortSignal?(JSValue value) => value.As<JSAbortSignal>();
    public static explicit operator JSAbortSignal(JSValue value)
        => value.As<JSAbortSignal>()
        ?? throw new InvalidCastException("JSValue is not an AbortSignal.");

    public static explicit operator JSAbortSignal(JSObject obj) => (JSAbortSignal)(JSValue)obj;
    public static implicit operator JSObject(JSAbortSignal promise) => (JSObject)promise._value;

    private JSAbortSignal(JSValue value)
    {
        _value = value;
    }

    public static explicit operator CancellationToken(JSAbortSignal signal)
        => signal.ToCancellationToken();

    public static explicit operator JSAbortSignal(CancellationToken cancellation)
        => FromCancellationToken(cancellation);
    public static explicit operator JSAbortSignal(CancellationToken? cancellation)
        => cancellation.HasValue ? FromCancellationToken(cancellation.Value) : default;

    #region IJSValue<JSAbortSignal> implementation

    // TODO: (vmoroz) Implement
    public static bool CanBeConvertedFrom(JSValue value) => value.IsObject();

    public static JSAbortSignal CreateUnchecked(JSValue value) => new(value);

    #endregion

    public JSValue AsJSValue() => _value;

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
                    controllerReference.GetValue()!.Value.CallMethod("abort");
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
