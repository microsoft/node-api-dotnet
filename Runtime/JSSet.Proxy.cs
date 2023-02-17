using System.Collections.Generic;

namespace NodeApi;

public partial struct JSSet
{
    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IReadOnlySet{T}"/>
    /// as a JS Set.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    public static JSProxy.Handler CreateProxyHandlerForReadOnlySet<T>(
        JSContext context,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return new JSProxy.Handler(
            context, $"{nameof(IReadOnlySet<T>)}<{typeof(T).Name}>")
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

                return JSIterable.ProxyGet(set, target, property, toJS);
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
    public static JSProxy.Handler CreateProxyHandlerForSet<T>(
        JSContext context,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return new JSProxy.Handler(
            context, $"{nameof(ISet<T>)}<{typeof(T).Name}>")
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

                return JSIterable.ProxyGet(set, target, property, toJS);
            },
        };
    }
}

