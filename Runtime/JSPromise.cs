using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

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

    public delegate void Resolve(Action<JSValue> resolve);

    public delegate Task AsyncResolve(Action<JSValue> resolve);

    public delegate void ResolveReject(
        Action<JSValue> resolve,
        Action<JSValue> reject); // TODO: Change reject type argument to JSError?

    public delegate Task AsyncResolveReject(
        Action<JSValue> resolve,
        Action<JSValue> reject); // TODO: Change reject type argument to JSError?

    /// <summary>
    /// Creates a new JS Promise with a resolve callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/> or throw an exception.</param>
    /// <remarks>
    /// Any exception thrown by the callback will be caught and used as a promise rejection error.
    /// </remarks>
    public JSPromise(Resolve callback)
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
    /// Creates a new JS Promise with an async resolve callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/> or throw an exception.</param>
    /// <remarks>
    /// Any (sync or async) exception thrown by the callback will be caught and used as a promise
    /// rejection error.
    /// </remarks>
    public JSPromise(AsyncResolve callback)
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
    public JSPromise(AsyncResolveReject callback)
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

    public struct Deferred
    {
        private napi_deferred _handle;

        public Deferred(napi_deferred handle)
        {
            _handle = handle;
        }

        public void Resolve(JSValue resolution)
        {
            // _handle becomes invalid after this call
            napi_resolve_deferred((napi_env)JSValueScope.Current, _handle, (napi_value)resolution)
                .ThrowIfFailed();
        }

        public void Reject(JSValue rejection)
        {
            // _handle becomes invalid after this call
            napi_resolve_deferred((napi_env)JSValueScope.Current, _handle, (napi_value)rejection)
                .ThrowIfFailed();
        }

        public void Reject(Exception ex)
        {
            // TODO: Create JSError type?
            JSValue error = JSValue.Global["Error"].CallAsConstructor(ex.Message);
            napi_resolve_deferred((napi_env)JSValueScope.Current, _handle, (napi_value)error)
                .ThrowIfFailed();
        }
    }
}
