// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi.Interop;

internal static class JSCollectionProxies
{
    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IEnumerable{T}"/>
    /// as a JS Iterable object.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    internal static JSProxy.Handler CreateIterableProxyHandlerForEnumerable<T>(
        JSValue.From<T> toJS)
    {
        return new JSProxy.Handler(
            $"{nameof(IEnumerable<T>)}<{typeof(T).Name}>")
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                IEnumerable<T> enumerable = target.Unwrap<IEnumerable<T>>();
                return ProxyIterableGet(enumerable, target, property, toJS);
            },
        };
    }

    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IAsyncEnumerable{T}"/>
    /// as a JS AsyncIterable object.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    internal static JSProxy.Handler CreateAsyncIterableProxyHandlerForAsyncEnumerable<T>(
        JSValue.From<T> toJS)
    {
        return new JSProxy.Handler(
            $"{nameof(IAsyncEnumerable<T>)}<{typeof(T).Name}>")
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                IAsyncEnumerable<T> enumerable = target.Unwrap<IAsyncEnumerable<T>>();
                return ProxyAsyncIterableGet(enumerable, target, property, toJS);
            },
        };
    }

    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IReadOnlyCollection{T}"/>
    /// as a JS Iterable object with an additional `length` property.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    internal static JSProxy.Handler CreateIterableProxyHandlerForReadOnlyCollection<T>(
        JSValue.From<T> toJS)
    {
        return new JSProxy.Handler(
            $"{nameof(IReadOnlyCollection<T>)}<{typeof(T).Name}>")
        {
            // There is no equivalent to IReadOnlyCollection in JS.
            // Return an iterable proxy that also has a length property.
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                IReadOnlyCollection<T> collection = target.Unwrap<IReadOnlyCollection<T>>();

                if (property.IsString() && (string)property == "length")
                {
                    return collection.Count;
                }

                return ProxyIterableGet(collection, target, property, toJS);
            },
        };
    }

    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="ICollection{T}"/> as a
    /// JS Iterable object with an additional `length` property and `add` and `delete` methods.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    internal static JSProxy.Handler CreateIterableProxyHandlerForCollection<T>(
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return new JSProxy.Handler(
            $"{nameof(ICollection<T>)}<{typeof(T).Name}>")
        {
            // There is no equivalent to ICollection in JS.
            // (A JS Set is slightly different, more equivalent to C# ISet<T>.)
            // Return an iterable proxy that also has a length property and add/delete methods.
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                ICollection<T> collection = target.Unwrap<ICollection<T>>();

                if (property.IsString())
                {
                    string propertyName = (string)property;
                    if (propertyName == "length")
                    {
                        return collection.Count;
                    }
                    else if (propertyName == "add")
                    {
                        return JSValue.CreateFunction("add", (args) =>
                        {
                            collection.Add(fromJS(args[0]));
                            return args.ThisArg;
                        });
                    }
                    else if (propertyName == "delete")
                    {
                        return JSValue.CreateFunction("delete", (args) =>
                        {
                            return collection.Remove(fromJS(args[0]));
                        });
                    }
                }

                return ProxyIterableGet(collection, target, property, toJS);
            },
        };
    }

    private static JSValue ProxyIterableGet<T>(
        IEnumerable<T> enumerable,
        JSObject target,
        JSValue property,
        JSValue.From<T> toJS)
    {
        if (((JSValue)JSSymbol.Iterator).StrictEquals(property) ||
            (property.IsString() && (string)property == "values"))
        {
            return CreateIteratorFunction(enumerable, toJS);
        }

        return target[property];
    }

    private static JSValue CreateIteratorFunction<T>(
        IEnumerable<T> enumerable,
        JSValue.From<T> toJS)
    {
        return JSValue.CreateFunction("values", (args) =>
        {
            IEnumerator<T> enumerator = enumerable.GetEnumerator();
            JSObject iterator = new();
            iterator.DefineProperties(
                JSPropertyDescriptor.Function(JSSymbol.Iterator, (args) =>
                {
                    // The iterator is also iterable.
                    return args.ThisArg;
                }, JSPropertyAttributes.DefaultProperty),
                JSPropertyDescriptor.Function("next", (args) =>
                {
                    JSObject nextResult = new();
                    if (enumerator.MoveNext())
                    {
                        nextResult["value"] = toJS(enumerator.Current);
                    }
                    else
                    {
                        nextResult["done"] = JSValue.True;
                    }
                    return nextResult;
                }, JSPropertyAttributes.DefaultProperty));
            return iterator;
        });
    }

    private static JSValue ProxyAsyncIterableGet<T>(
        IAsyncEnumerable<T> enumerable,
        JSObject target,
        JSValue property,
        JSValue.From<T> toJS)
    {
        if (((JSValue)JSSymbol.AsyncIterator).StrictEquals(property))
        {
            return CreateAsyncIteratorFunction(enumerable, toJS);
        }

        return target[property];
    }

    private static JSValue CreateAsyncIteratorFunction<T>(
        IAsyncEnumerable<T> enumerable,
        JSValue.From<T> toJS)
    {
        return JSValue.CreateFunction("asyncValues", (args) =>
        {
            IAsyncEnumerator<T> enumerator = enumerable.GetAsyncEnumerator();
            JSObject iterator = new();
            iterator.DefineProperties(
                JSPropertyDescriptor.Function(JSSymbol.AsyncIterator, (args) =>
                {
                    // The iterator is also iterable.
                    return args.ThisArg;
                }, JSPropertyAttributes.DefaultProperty),
                JSPropertyDescriptor.Function("next", (args) =>
                {
                    return enumerator.MoveNextAsync().AsPromise((result) =>
                    {
                        JSObject nextResult = new();
                        if (result)
                        {
                            nextResult["value"] = toJS(enumerator.Current);
                        }
                        else
                        {
                            nextResult["done"] = JSValue.True;
                        }
                        return nextResult;
                    });
                }, JSPropertyAttributes.DefaultProperty));
            return iterator;
        });
    }


    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IReadOnlyList{T}"/>
    /// as a JS Array.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    internal static JSProxy.Handler CreateArrayProxyHandlerForReadOnlyList<T>(
        JSValue.From<T> toJS)
    {
        return new JSProxy.Handler(
            $"{nameof(IReadOnlyList<T>)}<{typeof(T).Name}>")
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                IReadOnlyList<T> list = target.Unwrap<IReadOnlyList<T>>();

                if (property.IsNumber())
                {
                    return toJS(list[(int)property]);
                }
                else if (property.IsString())
                {
                    string propertyName = (string)property;
                    if (propertyName == "length")
                    {
                        return list.Count;
                    }
                }

                return ProxyIterableGet(list, target, property, toJS);
            },
        };
    }

    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IList{T}"/>
    /// as a JS Array.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    internal static JSProxy.Handler CreateArrayProxyHandlerForList<T>(
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return new JSProxy.Handler(
            $"{nameof(IList<T>)}<{typeof(T).Name}>")
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                IList<T> list = target.Unwrap<IList<T>>();

                if (property.IsNumber())
                {
                    return toJS(list[(int)property]);
                }
                else if (property.IsString())
                {
                    string propertyName = (string)property;
                    if (propertyName == "length")
                    {
                        return list.Count;
                    }
                }

                return ProxyIterableGet(list, target, property, toJS);
            },
            Set = (JSObject target, JSValue property, JSValue value, JSObject receiver) =>
            {
                var list = (IList<T>)((JSValue)target).Unwrap();

                if (property.IsNumber())
                {
                    list[(int)property] = fromJS(value);
                    return true;
                }
                else if (property.IsString())
                {
                    string propertyName = (string)property;
                    if (propertyName == "length")
                    {
                        int newLength = (int)value;

                        while (list.Count < newLength)
                        {
                            list.Add(default!);
                        }

                        while (list.Count > newLength)
                        {
                            list.RemoveAt(list.Count - 1);
                        }

                        return true;
                    }
                }

                return false;
            },
        };
    }

    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IReadOnlySet{T}"/>
    /// as a JS Set.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    internal static JSProxy.Handler CreateSetProxyHandlerForReadOnlySet<T>(
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return new JSProxy.Handler(
            $"{nameof(IReadOnlySet<T>)}<{typeof(T).Name}>")
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                IReadOnlySet<T> set = target.Unwrap<IReadOnlySet<T>>();

                if (property.IsString())
                {
                    string propertyName = (string)property;
                    if (propertyName == "size")
                    {
                        return set.Count;
                    }
                    else if (propertyName == "has")
                    {
                        return JSValue.CreateFunction("has", (args) =>
                        {
                            return set.Contains(fromJS(args[0]));
                        });
                    }

                    // TODO: More Set methods: keys(), entries(), forEach()
                }

                return ProxyIterableGet(set, target, property, toJS);
            },
        };
    }

    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="ISet{T}"/>
    /// as a JS Set.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    internal static JSProxy.Handler CreateSetProxyHandlerForSet<T>(
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return new JSProxy.Handler(
            $"{nameof(ISet<T>)}<{typeof(T).Name}>")
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                ISet<T> set = target.Unwrap<ISet<T>>();

                if (property.IsString())
                {
                    string propertyName = (string)property;
                    switch (propertyName)
                    {
                        case "size":
                            return set.Count;

                        case "has":
                            return JSValue.CreateFunction("has", (args) =>
                            {
                                return set.Contains(fromJS(args[0]));
                            });
                        case "add":
                            return JSValue.CreateFunction("add", (args) =>
                            {
                                return set.Add(fromJS(args[0]));
                            });
                        case "delete":
                            return JSValue.CreateFunction("delete", (args) =>
                            {
                                return set.Remove(fromJS(args[0]));
                            });
                        case "clear":
                            return JSValue.CreateFunction("clear", (args) =>
                            {
                                set.Clear();
                                return JSValue.Undefined;
                            });

                            // TODO: More Set methods: keys(), entries(), forEach()
                    }
                }

                return ProxyIterableGet(set, target, property, toJS);
            },
        };
    }

    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an
    /// <see cref="IReadOnlyDictionary{TKey, TValue}"/> as a JS Map.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    internal static JSProxy.Handler CreateMapProxyHandlerForReadOnlyDictionary<TKey, TValue>(
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS,
        JSValue.To<TKey> keyFromJS)
    {
        return new JSProxy.Handler(
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
    internal static JSProxy.Handler CreateMapProxyHandlerForDictionary<TKey, TValue>(
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS,
        JSValue.To<TKey> keyFromJS,
        JSValue.To<TValue> valueFromJS)
    {
        return new JSProxy.Handler(
            $"{nameof(IDictionary<TKey, TValue>)}<{typeof(TKey).Name}, {typeof(TValue).Name}>")
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
            return CreateIteratorFunction(enumerable, (pair) =>
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
