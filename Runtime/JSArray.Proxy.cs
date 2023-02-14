using System.Collections.Generic;

namespace NodeApi;

public partial struct JSArray
{
    /// <summary>
    /// Creates a proxy handler for a proxy that wraps an <see cref="IReadOnlyList{T}"/>
    /// as a JS Array.
    /// </summary>
    /// <remarks>
    /// The same handler may be used by multiple <see cref="JSProxy"/> instances, for more
    /// efficient creation of proxies.
    /// </remarks>
    public static JSProxy.Handler CreateProxyHandlerForReadOnlyList<T>(
        JSContext context,
        JSValue.From<T> toJS)
    {
        return new JSProxy.Handler(
            context, $"{nameof(IReadOnlyList<T>)}<{typeof(T).Name}>")
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

                return target[property];
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
    public static JSProxy.Handler CreateProxyHandlerForList<T>(
        JSContext context,
        JSValue.From<T> toJS,
        JSValue.To<T> fromJS)
    {
        return new JSProxy.Handler(
            context, $"{nameof(IList<T>)}<{typeof(T).Name}>")
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

                return target[property];
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
}

