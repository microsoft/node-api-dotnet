using System.Collections.Generic;

namespace NodeApi;

public partial struct JSIterable
{
    /// <summary>
    /// Creates a JS iterable proxy for an enumerable.
    /// </summary>
    public static JSProxy Proxy<T>(
        IEnumerable<T> enumerable,
        JSContext context,
        JSValue.From<T> toJS)
    {
        JSObject target = new();
        return new JSProxy(target, new JSProxy.Handler(context)
        {
            Get = (JSObject target, JSValue property, JSObject receiver) =>
                ProxyGet(enumerable, toJS, target, property),
        });
    }

    internal static JSValue ProxyGet<T>(
        IEnumerable<T> enumerable,
        JSValue.From<T> toJS,
        JSObject target,
        JSValue property)
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
