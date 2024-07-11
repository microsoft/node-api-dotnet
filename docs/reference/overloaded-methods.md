# Overloaded Methods

.NET APIs commonly include multiple overloads for a method. JavaScript does not directly support
"overloaded" methods, though it is common practice to implement overload-like behavior in a JS
function by dynamically checking for different argument counts and/or types.

## Overload resolution

The JS [marshaller](../features/js-dotnet-marshalling) has limited support for overload
resolution. It can examine the count and types of arguments provided by the JavaScript caller and
select the best matching .NET overload accordingly.

```C#
[JSExport]
public class OverloadsExample
{
    public static void AddValue(string stringValue);
    public static void AddValue(double numberValue);
}
```
```JS
OverloadsExample.addValue('test'); // Calls AddValue(string)
OverloadsExample.addValue(77); // Calls AddValue(double)
```

Currently the overload resolution is limited to examining the JavaScript type of each argument
(`string`, `number`, `object`, etc), but that is not sufficient to select between overloads that
differ only in the _type of object_.
[More advanced overload resolution is planned.](https://github.com/microsoft/node-api-dotnet/issues/134)
