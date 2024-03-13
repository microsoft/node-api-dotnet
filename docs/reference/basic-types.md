# Basic Types

| C# Type  | JS Type  |
|----------|----------|
| `string` | `string` |
| `bool`   | `boolean`|
| `sbyte`  | `number` |
| `byte`   | `number` |
| `short`  | `number` |
| `ushort` | `number` |
| `int`    | `number` |
| `uint`   | `number` |
| `long`   | `number` |
| `ulong`  | `number` |
| `float`  | `number` |
| `double` | `number` |
| `nint`   | _not yet implemented_ |
| `nuint`  | _not yet implemented_ |
| `decimal`| [_not yet implemented_](https://github.com/microsoft/node-api-dotnet/issues/316) |

## Conversions between .NET and JS primitive types

C# primitive types can be implicitly converted to
[`JSValue`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValue).
Conversions from `JSValue` to C# primitives are explicit. These explicit conversions _do not_ do
type coercion, so they may throw `JSException` if the JS value is not the expected type. Use
one of the `CoerceTo*` methods if JS type coercion is desired.

```C#
string dotnetString = "22";
JSValue jsString = dotnetString; // implicit conversion
string dotnetString2 = (string)jsString; // explicit conversion
int dotnetInt = (int)jsString.CoerceToNumber(); // 22
int invalidInt = (int)jsString; // throws JSException: A number was expected
```

## Nullable conversions

Nullable conversions are also supported, in case the JS value might be `null` or `undefined`:

```C#
JSValue maybeJsString = JSValue.Null;
string? maybeString = (string?)maybeJsString; // JS null => .NET null
JSValue maybeJsString2 = maybeString; // .NET null => JS undefined

JSValue maybeJsNumber = JSValue.Undefined;
int? maybeInt = (int?)maybeJsNumber; // JS undefined => .NET null
JSValue maybeJsNumber2 = maybeInt; // .NET null => JS undefined
```

See also [Null and Undefined](./null-undefined).

## Strings

Converting between a .NET `string` and a JavaScript `string` unfortunately internally allocates
memory and makes a copy of the string, because neither runtime allows a string to point to
"external" memory (memory that isn't allocated and tracked by the same runtime). So be aware that
frequently passing large strings between .NET and JS can have a performance impact. If appropriate,
consider alternatives such as a [stream](./streams) or
[`Memory<byte>`](./arrays-collections#typed-arrays).

### String encoding

JavaScript string APIs support getting and setting string values using either UTF-8 or UTF-16, while
.NET strings use UTF-16 internally. When working with UTF-8 encoded strings, it is more efficient
to directly read or write UTF-8 bytes from/to the JavaScript value without using a .NET `string`
as an intermediate value. See
[`JSValue.GetValueStringUtf8()`](./dotnet/Microsoft.JavaScript.NodeApi/JSValue/GetValueStringUtf8)
and [`JSValue.CreateStringUtf8()`](./dotnet/Microsoft.JavaScript.NodeApi/JSValue/CreateStringUtf8).

## Numeric types

All .NET numeric types are convertable to and from the JS `number` type. That means there can be
some loss in precision in either direction, except when converting between `double` and `number`
which are both 64-bit IEEE-754 values. If lossiness is a concern, use a
[`checked`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/checked-and-unchecked)
conversion to or from `double` before converting to or after converting from `JSValue`:

```C#
JSValue jsNumber = …
double doubleValue = (double)jsNumber;
int intValue = checked((int)doubleValue);
```

```C#
long longValue = …
double doubleValue = checked((double)longValue);
JSValue jsNumber = doubleValue; // Implicit conversion from double to JSValue.
```

See also [BigInteger](./other-types#biginteger).