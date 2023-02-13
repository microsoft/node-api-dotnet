using System;
using System.Collections.Generic;
using System.Threading;

namespace NodeApi;

/// <summary>
/// Enables creation of JS Proxy objects with C# handler callbacks.
/// </summary>
public readonly partial struct JSProxy
{
    private readonly JSValue _value;
    private readonly JSValue _revoke;

    public static explicit operator JSProxy(JSValue value) => new(value);
    public static implicit operator JSValue(JSProxy arr) => arr._value;

    private JSProxy(JSValue value)
    {
        _value = value;
    }

    public JSProxy(JSObject target, Handler handler, bool revocable = false)
    {
        JSValue proxyConstructor = handler.Context.Import("Proxy");

        if (revocable)
        {
            JSValue proxyAndRevoke = proxyConstructor[nameof(revocable)]
                .Call(target, handler.Object);
            _value = proxyAndRevoke["proxy"];
            _revoke = proxyAndRevoke["revoke"];
        }
        else
        {
            _value = proxyConstructor.CallAsConstructor(target, handler.Object);
        }
    }

    public void Revoke()
    {
        if (!_revoke.Handle.HasValue)
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

    public sealed class Handler
    {
        public Handler(JSContext context)
        {
            Context = context;
        }

        internal JSContext Context { get; }

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

        internal JSObject Object => (JSObject)Reference.Value.GetValue()!;

        private Lazy<JSReference> Reference => new(
            CreateHandler, LazyThreadSafetyMode.ExecutionAndPublication);

        private JSReference CreateHandler()
        {
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

            var obj = JSValue.CreateObject();
            obj.DefineProperties(properties.ToArray());
            return Context.TrackReference(obj);
        }
    }
}
