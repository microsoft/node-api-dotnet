# Other Special Types

## BigInteger

| C# Type      | JS Type  |
|--------------|----------|
| `BigInteger` | `BigInt` |

.NET [`BigInteger`](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.biginteger)
converts to and from JavaScript
[`BigInt`](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/BigInt)
with no loss in precision. (Conversion internally allocates and copies memory for the number data.)
The [`JSBigInt`](./dotnet/Microsoft.JavaScript.NodeApi/JSBigInt) class supports working directly
with JS `BigInt` values, and converting to/from .NET `BigInteger`.

## Guid

| C# Type | JS Type  |
|---------|----------|
| `Guid`  | `string` |

A .NET `Guid` is marshalled to JS as a `string` in the default `"D"` format. Marshalling in the
other direction supports any format accepted by
[`Guid.Parse()`](https://learn.microsoft.com/en-us/dotnet/api/system.guid.parse).
