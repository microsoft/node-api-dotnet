# JavaScript Value Scopes

A JavaScript value in .NET is always associated with a value scope via its
[`JSValue.Scope`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValue/Scope) property,
which returns a [`JSValueScope`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValueScope).
A value is only valid within its scope; if the scope is closed (disposed), then attempts to
access or use the value will throw
[`JSValueScopeClosedException`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValueScopeClosedException).

Values received by a .NET method that is a JS callback are associated with a `Callback`
[scope type](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValueScopeType). When the method
returns, the callback scope is closed and any values in that scope become invalid.

## Nesting and escaping scopes

To limit the lifetime of JavaScript values, use a nested scope. This can be particularly important
if values are created in many iterations of a loop. Without a nested scope, none of the values
created within the loop would be released until the method returns, which could use a lot of memory.

```C#
string[] array = …
JSFunction jsFunction = …

foreach (string item in array)
{
    using (var nestedScope = new JSValueScope())
    {
        // Passing a .NET string to JS requires converting it to JSValue.
        // The conversion is implicit; the explicit cast is for illustration.
        jsFunction.Call(thisArg: default, (JSValue)item);
    }
}
```

Use an _escapable_ scope when it's necessary to return a value out of a nested scope. An escapable
scope allows one (and only one) value to be promoted to the containing scope:

```C#
[JSExport]
public JSValue EscapableScopeExample(JSCallbackArgs args)
{
    string[] array = …
    JSFunction jsFunction = …

    foreach (string item in array)
    {
        using (var escapableScope = new JSValueScope(JSValueScopeType.Escapable))
        {
            JSValue result = jsFunction.Call(thisArg: default, (JSValue)item);
            if (!result.IsUndefined())
            {
                // If the result is not escaped, it would be released when
                // the inner scope is disposed (before the method returns).
                return escapableScope.Escape(result);
            }
        }
    }

    return default;
}
```

## Scope thread affinity

JavaScript values and value scopes can only be accessed from the JavaScript main thread, which
is the thread that invokes the .NET callback. An attempt to access a value or scope from a
different thread will throw
[`JSInvalidThreadAccessException`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSInvalidThreadAccessException).
For more details, see [JS Threading and Async Continuations](./js-threading-async).

## References

To save a value for later use in another scope, create a reference to it and save the reference.
See [JS References](./js-references).
