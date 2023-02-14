using System.Collections.Generic;

namespace NodeApi;

public partial struct JSIterable
{
    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IEnumerable{T}"/>
    /// as a JS Iterable object.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    public static JSProxy.Handler CreateProxyHandlerForEnumerable<T>(
        JSContext context,
        JSValue.From<T> toJS)
    {
        return new JSProxy.Handler(
            context, $"{nameof(IEnumerable<T>)}<{typeof(T).Name}>")
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                IEnumerable<T> enumerable = target.Unwrap<IEnumerable<T>>();
                return ProxyGet(enumerable, target, property, toJS);
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
    public static JSProxy.Handler CreateProxyHandlerForReadOnlyCollection<T>(
        JSContext context,
        JSValue.From<T> toJS)
    {
        return new JSProxy.Handler(
            context, $"{nameof(IReadOnlyCollection<T>)}<{typeof(T).Name}>")
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

                return ProxyGet(collection, target, property, toJS);
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
    public static JSProxy.Handler CreateProxyHandlerForCollection<T>(
        JSContext context,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return new JSProxy.Handler(
            context, $"{nameof(ICollection<T>)}<{typeof(T).Name}>")
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

                return ProxyGet(collection, target, property, toJS);
            },
        };
    }

    private static JSValue ProxyGet<T>(
        IEnumerable<T> enumerable,
        JSObject target,
        JSValue property,
        JSValue.From<T> toJS)
    {
        if (IsIteratorSymbol(property) ||
            (property.IsString() && (string)property == "values"))
        {
            return CreateIteratorFunction(enumerable, toJS);
        }

        return target[property];
    }

    private static JSValue GetIteratorSymbol()
    {
        return JSValue.Global["Symbol"]["iterator"];
    }

    private static bool IsIteratorSymbol(JSValue value)
    {
        return GetIteratorSymbol().StrictEquals(value);
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
                JSPropertyDescriptor.Function(GetIteratorSymbol(), (args) =>
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
}
