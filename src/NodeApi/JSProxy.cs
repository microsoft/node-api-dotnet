// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Enables creation of JS Proxy objects with C# handler callbacks.
/// </summary>
public readonly partial struct JSProxy : IEquatable<JSValue>
#if NET7_0_OR_GREATER
    , IJSValue<JSProxy>
#endif
{
    private readonly JSValue _value;
    private readonly JSValue _revoke = default;

    public static implicit operator JSValue(JSProxy value) => value.AsJSValue();
    public static explicit operator JSProxy?(JSValue value) => value.As<JSProxy>();
    public static explicit operator JSProxy(JSValue value)
        => value.As<JSProxy>() ?? throw new InvalidCastException("JSValue is not a Proxy.");

    private JSProxy(JSValue value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a new JS proxy for a target.
    /// </summary>
    /// <param name="jsTarget">JS target for the proxy.</param>
    /// <param name="handler">Proxy handler callbacks (traps).</param>
    /// <param name="target">Optional target object to be wrapped by the JS target,
    /// or null if the JS target will not wrap anything.</param>
    /// <param name="revocable">True if the proxy may be revoked; defaults to false.</param>
    /// <remarks>
    /// If a wrapped target object is provided, proxy callbacks my access that object by calling
    /// <see cref="JSObject.Unwrap{T}"/>.
    /// </remarks>
    public JSProxy(
        JSObject jsTarget,
        Handler handler,
        object? target = null,
        bool revocable = false)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        if (target != null)
        {
            jsTarget.Wrap(target);
        }

        JSValue proxyConstructor = JSRuntimeContext.Current.Import(null, "Proxy");

        if (revocable)
        {
            JSValue proxyAndRevoke = proxyConstructor[nameof(revocable)]
                .Call(jsTarget, handler.JSHandler);
            _value = proxyAndRevoke["proxy"];
            _revoke = proxyAndRevoke["revoke"];
        }
        else
        {
            _value = proxyConstructor.CallAsConstructor(jsTarget, handler.JSHandler);
        }
    }

    #region IJSValue<JSProxy> implementation

    // TODO: (vmoroz) Implement using instanceof
    public static bool CanBeConvertedFrom(JSValue value) => value.IsObject();

    public static JSProxy CreateUnchecked(JSValue value) => new(value);

    #endregion

    public JSValue AsJSValue() => _value;

    /// <summary>
    /// Revokes the proxy, so that further access to the target is no longer trapped by
    /// the proxy handler.
    /// </summary>
    /// <exception cref="InvalidOperationException">The proxy is not revocable.</exception>
    public void Revoke()
    {
        if (_revoke == default)
        {
            throw new InvalidOperationException("Proxy is not revokable.");
        }

        _revoke.Call();
    }

    public delegate JSValue Apply(JSObject target, JSValue thisArg, JSArray arguments);
    public delegate JSObject Construct(JSObject target, JSArray arguments, JSValue newTarget);
    public delegate bool DefineProperty(JSObject target, JSValue property, JSObject descriptor);
    public delegate bool DeleteProperty(JSObject target, JSValue property);
    public delegate JSValue Get(JSObject target, JSValue property, JSObject receiver);
    public delegate JSObject GetOwnPropertyDescriptor(JSObject target, JSValue property);
    public delegate JSObject GetPrototypeOf(JSObject target);
    public delegate bool Has(JSObject target, JSValue property);
    public delegate bool IsExtensible(JSObject target);
    public delegate JSArray OwnKeys(JSObject target);
    public delegate bool PreventExtensions(JSObject target);
    public delegate bool Set(JSObject target, JSValue property, JSValue value, JSObject receiver);
    public delegate bool SetPrototypeOf(JSObject target, JSObject prototype);

    /// <summary>
    /// Specifies handler callbacks (traps) for a JS proxy.
    /// </summary>
    public sealed class Handler : IDisposable
    {
        public Handler(string? name = null)
        {
            Name = name;
            JSHandlerReference = new Lazy<JSReference>(
                () => new JSReference(CreateJSHandler()),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Gets the name that was given to the handler (for diagnostic purposes),
        /// or null if no name was assigned.
        /// </summary>
        public string? Name { get; }

        private Lazy<JSReference> JSHandlerReference { get; }

        /// <summary>
        /// Gets the JS object with the callback methods defined on it.
        /// </summary>
        internal JSObject JSHandler => (JSObject)JSHandlerReference.Value.GetValue()!;

        public Apply? Apply { get; init; }
        public Construct? Construct { get; init; }
        public DefineProperty? DefineProperty { get; init; }
        public DeleteProperty? DeleteProperty { get; init; }
        public Get? Get { get; init; }
        public GetOwnPropertyDescriptor? GetOwnPropertyDescriptor { get; init; }
        public GetPrototypeOf? GetPrototypeOf { get; init; }
        public Has? Has { get; init; }
        public IsExtensible? IsExtensible { get; init; }
        public OwnKeys? OwnKeys { get; init; }
        public PreventExtensions? PreventExtensions { get; init; }
        public Set? Set { get; init; }
        public SetPrototypeOf? SetPrototypeOf { get; init; }

        private JSObject CreateJSHandler()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException($"{nameof(JSProxy)}.{nameof(Handler)}");
            }

            List<JSPropertyDescriptor> properties = new();

            if (Apply != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "apply",
                    (args) => Apply((JSObject)args[0], args[1], (JSArray)args[2])));
            }

            if (Construct != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "construct",
                    (args) => Construct((JSObject)args[0], (JSArray)args[1], args[2])));
            }

            if (DefineProperty != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "defineProperty",
                    (args) => DefineProperty((JSObject)args[0], args[1], (JSObject)args[2])));
            }

            if (DeleteProperty != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "deleteProperty",
                    (args) => DeleteProperty((JSObject)args[0], args[1])));
            }

            if (Get != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "get",
                    (args) => Get((JSObject)args[0], args[1], (JSObject)args[2])));
            }

            if (GetOwnPropertyDescriptor != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "getOwnPropertyDescriptor",
                    (args) => GetOwnPropertyDescriptor((JSObject)args[0], args[1])));
            }

            if (GetPrototypeOf != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "getPrototypeOf",
                    (args) => GetPrototypeOf((JSObject)args[0])));
            }

            if (Has != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "has",
                    (args) => Has((JSObject)args[0], args[1])));
            }

            if (IsExtensible != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "isExtensible",
                    (args) => IsExtensible((JSObject)args[0])));
            }

            if (OwnKeys != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "ownKeys",
                    (args) => OwnKeys((JSObject)args[0])));
            }

            if (PreventExtensions != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "preventExtensions",
                    (args) => PreventExtensions((JSObject)args[0])));
            }

            if (Set != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "set",
                    (args) => Set((JSObject)args[0], args[1], args[2], (JSObject)args[3])));
            }

            if (SetPrototypeOf != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "setPrototypeOf",
                    (args) => SetPrototypeOf((JSObject)args[0], (JSObject)args[1])));
            }

            var jsHandler = new JSObject();
            jsHandler.DefineProperties(properties.ToArray());
            return jsHandler;
        }

        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Disposes the proxy handler.
        /// </summary>
        /// <remarks>
        /// Disposing a proxy handler does not revoke or dispose proxies created using the handler.
        /// It does prevent new proxies from being created using the handler instance.
        /// </remarks>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                if (JSHandlerReference.IsValueCreated)
                {
                    JSHandlerReference.Value.Dispose();
                }
            }
        }

        public override string ToString()
        {
            return $"{nameof(JSProxy)}.{nameof(Handler)} \"{Name}\"";
        }
    }

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSProxy a, JSProxy b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSProxy a, JSProxy b) => !a._value.StrictEquals(b);

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
