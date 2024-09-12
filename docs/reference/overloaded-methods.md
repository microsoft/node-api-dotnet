# Overloaded Methods

.NET APIs commonly include multiple overloads for a method. JavaScript does not directly support
"overloaded" methods, though it is common practice to implement overload-like behavior in a JS
function by dynamically checking for different argument counts and/or types.

## Overload resolution

The JS [marshaller](../features/js-dotnet-marshalling) has support for overload resolution. It can
examine the count and types of arguments provided by the JavaScript caller and select the best
matching .NET overload accordingly.

```C#
[JSExport]
public class OverloadsExample
{
    public static void AddValue(string stringValue);
    public static void AddValue(int intValue);
    public static void AddValue(double doubleValue);
}
```
```JS
OverloadsExample.addValue('test'); // Calls AddValue(string)
OverloadsExample.addValue(77); // Calls AddValue(int)
OverloadsExample.addValue(0.5); // Calls AddValue(double)
```

Overload resolution considers the following information when selecting the best match among method
overloads:
 - **Argument count** - Resolution eliminates any overloads that do not accept the number of
   arguments that were supplied, taking into account when some .NET method parameters are
   optional or have default values.
 - **Argument JS value types** - Resolution initially does a quick filter by matching only on
   the [JavaScript value type](./dotnet/Microsoft.JavaScript.NodeApi/JSValueType) of each argument,
   e.g. JS `string` matches .NET `string`, JS `number` matches any .NET numeric type,
   JS `object` matches any .NET class, interface, or struct type.
 - **Nullability** - JS `null` or `undefined` arguments match with any method parameters that are
   .NET reference types or `Nullable<T>` value types. (Non-nullable reference type annotations are
   not considered.) 
 - **Number argument properties** - If there are multiple overloads with different .NET numeric
   types (e.g. `int` and `double`), the properties of the JS number value are used to select the
   best overload, including whether it is negative, an integer, or outside the bounds of the .NET
   numeric type.
 - **Proxied .NET object types** - When a JS argument value is actually
   [a proxy to a .NET object](./classes-interfaces.md#marshalling-net-classes-to-js),
   then the .NET type is matched to the method parameter type.
 - **JS collection types** - When an argument value is a JS collection, the JS collection type
   such as `Array` or `Map` is matched to a corresponding .NET collection type. (Generic collection
   _element_ types are not considered, since JS collections do not have specific element types.)
 - **Other special types** - Types with special marshalling behavior including [dates](./dates),
   [guids](./other-types), [Tasks/Promises](./async-promises), and [delegates](./delegates) are
   matched accordingly during overload resolution.

If overload resolution finds multiple matches, or does not find any valid matches, then a
`TypeError` is thrown.

### Performance considerations

Unlike compiled languages where the compiler can bind to the appropriate overload at compile time,
with JavaScript the overload resolution process must be repeated at every invocation of the method.
It is not super expensive, but consider avoiding calls to overloaded methods in performance-critical
code.

### Limitations

When calling .NET methods from JavaScript, the dynamic overload resolution is not 100% consistent
with C#'s compile-time overload resolution. There are some unavoidable limitations due to the
dynamic-typed nature of JavaScript, and likely some deficienceies in the implementation. While it
should work sufficiently well for the majority of cases, if you find a situation where overload
resolution is not working as expected, please [report a bug](../support).
