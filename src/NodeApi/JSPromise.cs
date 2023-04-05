// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Represents a JavaScript Promise object.
/// </summary>
/// <seealso cref="TaskExtensions"/>
public readonly struct JSPromise : IEquatable<JSValue>
{
    private readonly JSValue _value;

    public static explicit operator JSPromise(JSValue value) => new(value);
    public static implicit operator JSValue(JSPromise promise) => promise._value;

    public static explicit operator JSPromise(JSObject obj) => (JSPromise)(JSValue)obj;
    public static implicit operator JSObject(JSPromise promise) => (JSObject)promise._value;

    private JSPromise(JSValue value)
    {
        _value = value;
    }

    public delegate void ResolveCallback(Action<JSValue> resolve);

    public delegate Task AsyncResolveCallback(Action<JSValue> resolve);

    public delegate void ResolveRejectCallback(
        Action<JSValue> resolve,
        Action<JSError> reject);

    public delegate Task AsyncResolveRejectCallback(
        Action<JSValue> resolve,
        Action<JSError> reject);

    /// <summary>
    /// Creates a new JS Promise with a resolve callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/> or throw an exception.</param>
    /// <remarks>
    /// Any exception thrown by the callback will be caught and used as a promise rejection error.
    /// </remarks>
    public JSPromise(ResolveCallback callback)
    {
        _value = JSValue.CreatePromise(out Deferred deferred);
        try
        {
            callback(deferred.Resolve);
        }
        catch (Exception ex)
        {
            deferred.Reject(ex);
        }
    }

    /// <summary>
    /// Creates a new JS Promise with a resolve/reject callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/>, invoke the reject function with
    /// a JS Error, or throw an exception.</param>
    /// <remarks>
    /// Any exception thrown by the callback will be caught and used as a promise rejection error.
    /// </remarks>
    public JSPromise(ResolveRejectCallback callback)
    {
        _value = JSValue.CreatePromise(out Deferred deferred);
        try
        {
            callback(deferred.Resolve, deferred.Reject);
        }
        catch (Exception ex)
        {
            deferred.Reject(ex);
        }
    }

    /// <summary>
    /// Creates a new JS Promise with an async resolve callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/> or throw an exception.</param>
    /// <remarks>
    /// Any (sync or async) exception thrown by the callback will be caught and used as a promise
    /// rejection error.
    /// </remarks>
    public JSPromise(AsyncResolveCallback callback)
    {
        _value = JSValue.CreatePromise(out Deferred deferred);
        async void AsyncCallback()
        {
            using var asyncScope = new JSAsyncScope();
            try
            {
                await callback(deferred.Resolve);
            }
            catch (Exception ex)
            {
                deferred.Reject(ex);
            }
        }
        AsyncCallback();
    }

    /// <summary>
    /// Creates a new JS Promise with an async resolve/reject callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/>, invoke the reject function with
    /// a JS Error, or throw an exception.</param>
    /// <remarks>
    /// Any (sync or async) exception thrown by the callback will be caught and used as a promise
    /// rejection error.
    /// </remarks>
    public JSPromise(AsyncResolveRejectCallback callback)
    {
        _value = JSValue.CreatePromise(out Deferred deferred);
        async void AsyncCallback()
        {
            using var asyncScope = new JSAsyncScope();
            try
            {
                await callback(deferred.Resolve, deferred.Reject);
            }
            catch (Exception ex)
            {
                deferred.Reject(ex);
            }
        }
        AsyncCallback();
    }

    /// <summary>
    /// Registers callbacks that are invoked when a promise is fulfilled and/or rejected,
    /// and returns a new chained promise.
    /// </summary>
    public JSPromise Then(Action<JSValue>? fulfilled, Action<JSError>? rejected)
    {
        JSValue fulfilledFunction = fulfilled == null ? JSValue.Undefined :
            JSValue.CreateFunction(nameof(fulfilled), (args) =>
            {
                fulfilled(args[0]);
                return JSValue.Undefined;
            });
        JSValue rejectedFunction = rejected == null ? JSValue.Undefined :
            JSValue.CreateFunction(nameof(rejected), (args) =>
            {
                rejected(new JSError(args[0]));
                return JSValue.Undefined;
            });
        return (JSPromise)_value.CallMethod("then", fulfilledFunction, rejectedFunction);
    }

    /// <summary>
    /// Registers a callback that is invoked when a promise is rejected, and returns a new
    /// chained promise.
    /// </summary>
    public JSPromise Catch(Action<JSValue> rejected)
    {
        JSValue rejectedFunction = JSValue.CreateFunction(nameof(rejected), (args) =>
        {
            rejected(args[0]);
            return JSValue.Undefined;
        });
        return (JSPromise)_value.CallMethod("catch", rejectedFunction);
    }

    /// <summary>
    /// Registers a callback that is invoked after a promise is fulfilled or rejected, and
    /// returns a new chained promise.
    /// </summary>
    public JSPromise Finally(Action completed)
    {
        JSValue completedFunction = JSValue.CreateFunction(nameof(completed), (_) =>
        {
            completed();
            return JSValue.Undefined;
        });
        return (JSPromise)_value.CallMethod("finally", completedFunction);
    }

    /// <summary>
    /// Creates a new promise that resolves to a value of `undefined`.
    /// </summary>
    public static JSPromise Resolve()
    {
        return (JSPromise)JSRuntimeContext.Current.Import(null, "Promise").CallMethod("resolve");
    }

    /// <summary>
    /// Creates a new promise that resolves to the provided value.
    /// </summary>
    public static JSPromise Resolve(JSValue value)
    {
        return (JSPromise)JSRuntimeContext.Current.Import(null, "Promise").CallMethod("resolve", value);
    }

    /// <summary>
    /// Creates a new promise that is rejected with the provided reason.
    /// </summary>
    public static JSPromise Reject(JSValue reason)
    {
        return (JSPromise)JSRuntimeContext.Current.Import(null, "Promise").CallMethod("reject", reason);
    }

    public static JSPromise All(params JSPromise[] promises) => Select("all", promises);

    public static JSPromise All(IEnumerable<JSPromise> promises) => Select("all", promises);

    public static JSPromise All(JSArray promises) => Select("all", promises);

    public static JSPromise Any(params JSPromise[] promises) => Select("any", promises);

    public static JSPromise Any(IEnumerable<JSPromise> promises) => Select("any", promises);

    public static JSPromise Any(JSArray promises) => Select("any", promises);

    public static JSPromise Race(params JSPromise[] promises) => Select("race", promises);

    public static JSPromise Race(IEnumerable<JSPromise> promises) => Select("race", promises);

    public static JSPromise Race(JSArray promises) => Select("race", promises);

    private static JSPromise Select(string operation, IEnumerable<JSPromise> promises)
    {
        JSArray promiseArray = new();
        foreach (JSPromise promise in promises) promiseArray.Add(promise);
        return Select(operation, promiseArray);
    }

    private static JSPromise Select(string operation, JSArray promiseArray)
    {
        return (JSPromise)JSRuntimeContext.Current.Import(null, "Promise")
            .CallMethod(operation, promiseArray);
    }

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSPromise a, JSPromise b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSPromise a, JSPromise b) => !a._value.StrictEquals(b);

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

    /// <summary>
    /// Supports resolving or rejecting a created JavaScript Promise.
    /// </summary>
    public struct Deferred
    {
        private napi_deferred _handle;

        internal Deferred(napi_deferred handle)
        {
            _handle = handle;
        }

        public void Resolve(JSValue resolution)
        {
            // _handle becomes invalid after this call
            napi_resolve_deferred((napi_env)JSValueScope.Current, _handle, (napi_value)resolution)
                .ThrowIfFailed();
        }

        public void Reject(JSError rejection)
        {
            // _handle becomes invalid after this call
            napi_resolve_deferred(
                (napi_env)JSValueScope.Current, _handle, (napi_value)rejection.Value)
                .ThrowIfFailed();
        }

        public void Reject(Exception exception)
        {
            Reject(new JSError(exception));
        }
    }
}
