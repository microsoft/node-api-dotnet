# JavaScript types in .NET

While [automatic marshalling](./js-dotnet-marshalling.md) can conveniently convert JavaScript values
to and from almost any specific .NET types, the marshaller has some limitations, particularly
because the [mappings between JS and .NET types](../reference/js-dotnet-types.md) can be inexact.
So sometimes it is necessary for .NET code to interact directly with JavaScript types, precisely
preserving all of the JavaScript type's semantics.

## The `JSValue` type

The [`JSValue`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValue) type represents _any_
kind of JavaScript value. It can be `undefined`, `null`, a primitive (`number`, `string`, etc.)
or an object. The value type can be checked via the
[`JSValue.TypeOf()`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValue/TypeOf) method,
which is equivalent to the JS
[`typeof`](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Operators/typeof)
operator. `default(JSValue)` is equivalent to the JS `undefined` value.

The `JSValue` type has methods for doing any kind of operation or conversion on the value, though
many of those may throw a `JSException` if the value is not the correct type. And such an exception
would be [re-thrown](../reference/exceptions) to JS as a `TypeError`.

Available conversions include casting to and from .NET primitive type:
```C#
JSValue jsString = …
string s = (string)jsString;
JSValue jsString2 = s; // Conversions to JSValue are implicit.
```

Internally, a `JSValue` contains only two fields:
 - A [`napi_value`](https://nodejs.org/api/n-api.html#napi_value) native handle to the JavaScript
   value
 - A reference to a [`JSValueScope`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValueScope)
   instance, which is primarily a wrapper around a
   [`napi_handle_scope`](https://nodejs.org/api/n-api.html#napi_handle_scope)

`JSValue` is a `struct` because a JavaScript value is not valid outside of its
[scope](./js-value-scopes). (We've considered making it a
[`ref struct`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct)
but that design would have too many limitations.) Being a value-type also reduces memory allocations
when passing JS values to .NET, improving interop performance.

Use a [`JSReference`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSReference) to store a
JavaScript value on the heap and track it across multiple invocations.

## Other JavaScript value types
A `JSValue` can be cast to another `struct` that represents a more specific value type:

```C#
void CopyToStringArray(JSValue jsArray, string[] destination)
    => ((JSArray)jsArray).CopyTo(destination, 0, (value) => (string)value);
```

The specific value types provide properties and methods specific to their type, and are often easier
to work with:
 - [`JSArray`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSArray)
 - [`JSBigInt`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSBigInt)
 - [`JSDate`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSDate)
 - [`JSFunction`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSFunction)
 - [`JSIterable`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSIterable)
 - [`JSMap`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSMap)
 - [`JSObject`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSObject)
 - [`JSPromise`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSPromise)
 - [`JSProxy`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSProxy)
 - [`JSSet`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSSet)
 - [`JSSymbol`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSSymbol)
 - [`JSTypedArray`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSTypedArray-1)

The value types all implement `IEquatable<JSValue>`, which evaluates JavaScript strict equality.

The .NET `ToString()` method of any type of JS value returns the same result as calling `toString()`
from JavaScript.

## Using JavaScript value types in .NET APIs

When exporting a .NET method to JavaScript, use
[`JSCallbackArgs`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSCallbackArgs) to declare a
method that can accept any number of JavaScript values as arguments:
```C#
[JSExport]
public JSValue ExampleWithVariableJSArgs(JSCallbackArgs args) { … }
```

Mixing .NET types and JS types in a method signature is also allowed:
```C#
[JSExport]
public JSValue ExampleWithJSArray(string s, JSValue value, JSArray array) { … }
```
