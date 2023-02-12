using System.Collections.Generic;

namespace NodeApi;

public partial struct JSArray
{
    /// <summary>
    /// Creates a JS proxy for a read-only collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="collection"></param>
    /// <param name="context"></param>
    /// <param name="toJS"></param>
    /// <returns></returns>
    public static JSProxy Proxy<T>(
        IReadOnlyCollection<T> collection,
        JSContext context,
        JSValue.From<T> toJS)
    {
        JSObject target = new JSObject();
        return new JSProxy(target, new JSProxy.Handler(context)
        {
            // There is no equivalent to IReadOnlyCollection in JS.
            // Return an iterable proxy that also has a length property.
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
                if (property.IsString() && (string)property == "length")
                {
                    return collection.Count;
                }

                return JSIterable.ProxyGet(collection, toJS, target, property);
            },
        });
    }

    /// <summary>
    /// Creates a JS proxy for a collection.
    /// </summary>
    public static JSProxy Proxy<T>(
        ICollection<T> collection,
        JSContext context,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        JSObject target = new JSObject();
        return new JSProxy(target, new JSProxy.Handler(context)
        {
            // There is no equivalent to ICollection in JS.
            // (A JS Set is slightly different, more equivalent to C# ISet<T>.)
            // Return an iterable proxy that also has a length property and add/delete methods.
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
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

                return JSIterable.ProxyGet(collection, toJS, target, property);
            },
        });
    }

    /// <summary>
    /// Creates a JS array proxy for a read-only list.
    /// </summary>
    public static JSProxy Proxy<T>(
        IReadOnlyList<T> list,
        JSContext context,
        JSValue.From<T> toJS)
    {
        JSArray target = new JSArray();
        return new JSProxy(target, new JSProxy.Handler(context)
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
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

                return target[property];
            },
        });
    }

    /// <summary>
    /// Creates a JS array proxy for a list.
    /// </summary>
    public static JSProxy Proxy<T>(
        IList<T> list,
        JSContext context,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        JSArray target = new JSArray();
        return new JSProxy(target, new JSProxy.Handler(context)
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
            {
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

                return target[property];
            },
            Set = (JSObject target, JSValue property, JSValue value, JSObject receiver) =>
            {
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
                            list.Add(default(T)!);
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
        });
    }
}

