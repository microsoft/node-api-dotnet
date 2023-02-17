using System.Collections.Generic;

namespace NodeApi;

public partial struct JSMap
{
    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an
    /// <see cref="IReadOnlyDictionary{TKey, TValue}"/> as a JS Map.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    public static JSProxy.Handler CreateProxyHandlerForReadOnlyDictionary<TKey, TValue>(
        JSContext context,
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS,
        JSValue.To<TKey> keyFromJS)
    {
        return new JSProxy.Handler(
            context,
            $"{nameof(IReadOnlyDictionary<TKey, TValue>)}<{typeof(TKey).Name}, {typeof(TValue).Name}>")
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                IReadOnlyDictionary<TKey, TValue> dictionary =
                    target.Unwrap<IReadOnlyDictionary<TKey, TValue>>();

                if (property.IsString())
                {
                    string propertyName = (string)property;
                    if (propertyName == "size")
                    {
                        return dictionary.Count;
                    }
                    else if (propertyName == "has")
                    {
                        return JSValue.CreateFunction("has", (args) =>
                        {
                            return dictionary.ContainsKey(keyFromJS(args[0]));
                        });
                    }
                    else if (propertyName == "get")
                    {
                        return JSValue.CreateFunction("get", (args) =>
                        {
                            return dictionary.TryGetValue(keyFromJS(args[0]), out TValue? value) ?
                                valueToJS(value!) : JSValue.Undefined;
                        });
                    }

                    // TODO: More Map methods: keys(), values(), forEach()
                }

                return ProxyIterableGet(dictionary, target, property, keyToJS, valueToJS);
            },
        };
    }

    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IDictionary{TKey, TValue}"/>
    /// as a JS Map.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    public static JSProxy.Handler CreateProxyHandlerForDictionary<TKey, TValue>(
        JSContext context,
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS,
        JSValue.To<TKey> keyFromJS,
        JSValue.To<TValue> valueFromJS)
    {
        return new JSProxy.Handler(
            context, $"{nameof(IDictionary<TKey, TValue>)}<{typeof(TKey).Name}, {typeof(TValue).Name}>")
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                IDictionary<TKey, TValue> dictionary = target.Unwrap<IDictionary<TKey, TValue>>();

                if (property.IsString())
                {
                    string propertyName = (string)property;
                    switch (propertyName)
                    {
                        case "size":
                            return dictionary.Count;

                        case "has":
                            return JSValue.CreateFunction("has", (args) =>
                            {
                                return dictionary.ContainsKey(keyFromJS(args[0]));
                            });
                        case "get":
                            return JSValue.CreateFunction("get", (args) =>
                            {
                                return dictionary.TryGetValue(keyFromJS(args[0]), out TValue? value) ?
                                    valueToJS(value!) : JSValue.Undefined;
                            });
                        case "set":
                            return JSValue.CreateFunction("set", (args) =>
                            {
                                dictionary.Add(keyFromJS(args[0]), valueFromJS(args[1]));
                                return target;
                            });
                        case "delete":
                            return JSValue.CreateFunction("delete", (args) =>
                            {
                                return dictionary.Remove(keyFromJS(args[0]));
                            });
                        case "clear":
                            return JSValue.CreateFunction("clear", (args) =>
                            {
                                dictionary.Clear();
                                return JSValue.Undefined;
                            });

                            // TODO: More Map methods: keys(), values(), forEach()
                    }
                }

                return ProxyIterableGet(dictionary, target, property, keyToJS, valueToJS);
            },
        };
    }

    private static JSValue ProxyIterableGet<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> enumerable,
        JSObject target,
        JSValue property,
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS)
    {
        if (((JSValue)JSSymbol.Iterator).StrictEquals(property) ||
            (property.IsString() && (string)property == "entries"))
        {
            return JSIterable.CreateIteratorFunction(enumerable, (pair) =>
            {
                JSArray jsPair = new(2)
                {
                    [0] = keyToJS(pair.Key),
                    [1] = valueToJS(pair.Value)
                };
                return jsPair;
            });
        }

        return target[property];
    }
}

