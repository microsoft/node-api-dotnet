# TypeScript type definitions for .NET APIs

## Building type definitions
When [building a Node module project in C#](node-module.md), a type-definitions (`.d.ts`) file is
generated automatically. Or, the generator tool is available separately via the
`node-api-dotnet-generator` npm package.

## Type projections reference

### Primitive types
| C# Type  | TypeScript Projection |
|----------|-----------------------|
| `bool`   | `boolean`             |
| `sbyte`  | `number`              |
| `byte`   | `number`              |
| `short`  | `number`              |
| `ushort` | `number`              |
| `int`    | `number`              |
| `uint`   | `number`              |
| `long`   | `number`              |
| `ulong`  | `number`              |
| `nint`   | `number`              |
| `nuint`  | `number`              |
| `float`  | `number`              |
| `double` | `number`              |
| `string` | `string`              |

### Object types
| C# Type                       | TypeScript Projection |
|-------------------------------|-----------------------|
| `class Example`               | `class Example`       |
| `struct Example`              | `class Example`       |
| `interface IExample`          | `interface IExample`  |

.NET property names and method names are automatically camel-cased when projected to JavaScript /
TypeScript by a [C# node module](./node-module.md). Names are projected as-is (without any
camel-casing) when [dynamically invoking .NET APIs from JavaScript](./dynamic-invoke).

### Delegate types
| C# Type                       | TypeScript Projection                   |
|-------------------------------|-----------------------------------------|
| `Action<T1, T2, ...>`         | `(arg1: T1, arg2: T2, ...) => void`     |
| `Func<T1, T2, ... TResult>`   | `(arg1: T1, arg2: T2, ...) => TResult`  |
| `Predicate<T>`                | `(value: T) => boolean`                 |
| `delegate A Example(B value)` | `declare function Example(value: B): A` |

Named delegate types are declared as TypeScript functions. Generic delegate types are inlined.

### Enums
| C# Type                          | TypeScript Projection                |
|----------------------------------|--------------------------------------|
| `enum Example { None = 0, One }` | `enum Example { None = 0, One = 1 }` |

.NET enums are projected to JS in the style of TypeScript _non-const_ enums: JS objects with
read-only numeric properties and a reverse mapping from enum values to name strings. (Enum member
names are NOT camel-cased.)

### Array types
| C# Type     | TypeScript Projection |
|-------------|-----------------------|
| `T[]`            | `T[]` aka `Array<T>` |
| `Memory<sbyte>`  | `Int8Array`          |
| `Memory<sbyte>`  | `UInt8Array`         |
| `Memory<short>`  | `Int16Array`         |
| `Memory<ushort>` | `UInt16Array`        |
| `Memory<int>`    | `Int32Array`         |
| `Memory<uint>`   | `UInt32Array`        |
| `Memory<long>`   | `BigInt64Array`      |
| `Memory<ulong>`  | `BigUInt64Array`     |
| `Memory<float>`  | `Float32Array`       |
| `Memory<double>` | `Float64Array`       |

Regular .NET arrays are copied (passed by value) to or from JS, because there's no way to create a
.NET array over external memory.

Instead of larger arrays, use `Memory<T>` if the element type is one of the typed-array element
types. `Memory<T>` and typed arrays can share memory so that no marshalling or proxying is needed
for element access. Otherwise prefer collection interfaces (below) which are passed by proxy.

### Collection types

| C# Type                     | TypeScript Projection |
|-----------------------------|-----------------------|
| `IEnumerable<T>`            | `Iterable<T>`         |
| `IReadOnlyCollection<T>`    | `ReadonlySet<T>`      |
| `ICollection<T>`            | `Set<T>`              |
| `IReadOnlySet<T>`           | `ReadonlySet<T>`      |
| `ISet<T>`                   | `Set<T>`              |
| `IReadOnlyList<T>`          | `readonly T[]` (`ReadonlyArray<T>`) |
| `IList<T>`                  | `T[]` aka `Array<T>`  |
| `IReadOnlyDictionary<T>`    | `ReadonlyMap<T>`      |
| `IDictionary<T>`            | `Map<T>`              |
| `KeyValuePair<TKey, TValue>`| `[TKey, TValue]`      |
| `Tuple<T1, T2, ...>`        | `[T1, T2, ...]`       |
| `ValueTuple<T1, T2, ...>`   | `[T1, T2, ...]`       |

Collections exported from .NET use JavaScript proxies with .NET handlers to avoid copying items
across the .NET/JS boundary. But this implementation detail does not affect the types visible to
JavaScript code, e.g. a dictionary from .NET still satisfies `instanceof Map` in JS.

`ValueTuple`s with named properties are still projected as JS arrays, not JS objects with named
properties. So C# `(string A, int B)` becomes TypeScript `[string, number]`, not
`{ A: string, B: number }`.

### Special types

| C# Type            | TypeScript Projection |
|--------------------|-----------------------|
| `Task`             | `Promise<void>`       |
| `Task<T>`          | `Promise<T>`          |
| `DateTime`         | `Date`                |
| `DateTimeOffset`   | `[Date, number]`      |
| `TimeSpan`         | `number`              |
| `BigInteger`       | `BigInt`              |
| `Tuple<A, B, ...>` | `[A, B, ...]`         |
| `Stream`           | `Duplex`              |

Dates marshalled from JavaScript will always be `Utc` kind. A `TimeSpan` is projected to JavaScript
as a decimal number of milliseconds. A `DateTimeOffset` is projected as a tuple of the UTC date-time
and the offset in (positive or negative) milliseconds.

### Methods with `ref` or `out` parameters
JavaScript does not support `ref` or `out` parameters, so some transformations are applied
when marshalling such methods between .NET and JS.

A .NET method following the `Try*` pattern (with `bool` return value and a single `out` parameter)
is transformed to a JavaScript function with a direct return value of either the result (if the
method returned `true`) or `undefined` (if the method returned `false`).

<table>
<tr><th>C#</th><th>TypeScript</th></tr>
<tr><td>

```C#
bool TryParse(string s, out Version result)
```
</td><td>

```JavaScript
function TryParse(s: string): Version | undefined
```
</td></tr>
</table>

Any other .NET method with `ref` and/or `out` parameters is transformed to a JavaScript function
that returns an object. The object has a `result` property that is the actual return value from
the method (if not `void`), and additional properties with names matching each of the `ref` or
`out` parameters. The `out` parameters do not need to be supplied as arguments in the JS call.

<table>
<tr><th>C#</th><th>TypeScript</th></tr>
<tr><td>

```C#
string[] GetAllResults(ref string value, out int count)
```
</td><td>

```JavaScript
function GetAllResults(value: string):
    { value: string, count: int, result: string[] }
```
</td></tr>
</table>

### Generics
.NET generic types other than those listed above, and generic methods, are projected to JavaScript
in a way that the generic specializations can be accessed at runtime (unlike TypeScript generics
which have no runtime representation). See [.NET Generics in JavaScript](./generics.md) for details.
