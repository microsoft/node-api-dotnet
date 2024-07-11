# Arrays and Collections

## Arrays

| C# Type  | JS Type            |
|----------|--------------------|
| `T[]`    | `T[]` (`Array<T>`) |

.NET arrays are marshalled by value to or from JS. (This would not be the preferred design, but
unfortunately there is no way to create a .NET array over "external" memory, that is memory not
allocated / managed by the .NET runtime.) This means that whenever a .NET array instqance is
marshalled to or from JS, all the array items are copied. Use `IList<T>` or another collection
interface to avoid copying the items.

## Collections

| C# Type                     | JS Type               |
|-----------------------------|-----------------------|
| `IEnumerable<T>`            | `Iterable<T>`         |
| `IAsyncEnumerable<T>`       | `AsyncIterable<T>`    |
| `IReadOnlyCollection<T>`    | `ReadonlySet<T>`      |
| `ICollection<T>`            | `Set<T>`              |
| `IReadOnlySet<T>`           | `ReadonlySet<T>`      |
| `ISet<T>`                   | `Set<T>`              |
| `IReadOnlyList<T>`          | `readonly T[]` (`ReadonlyArray<T>`) |
| `IList<T>`                  | `T[]` (`Array<T>`)    |
| `IReadOnlyDictionary<T>`    | `ReadonlyMap<T>`      |
| `IDictionary<T>`            | `Map<T>`              |
| `KeyValuePair<TKey, TValue>`| `[TKey, TValue]`      |

Collections (other than .NET arrays) are marshalled by reference. This means passing an instance of
a collection between .NET and JS does not immediately copy any values, and any modifications to the
collection affect both .NET and JS.

JavaScript collections can be adapted to .NET collection interfaces using the extension methods
in [`JSCollectionExtensions`](./dotnet/Microsoft.JavaScript.NodeApi.Interop/JSCollectionExtensions).

Concrete collection classes like `List<T>`, `Dictionary<T>`, `ReadOnlyCollection<T>` are
[not yet implemented](https://github.com/microsoft/node-api-dotnet/issues/242). If and when
they are supported they will have major limitations so are not recommended. Use interfaces instead,
which is standard practice for public APIs anyway.

Non-generic collection interfaces in the `System.Collections` namespace are
[not yet implemented](https://github.com/microsoft/node-api-dotnet/issues/243), since they are
rarely used in modern C# code.

## Typed arrays

| C# Type          | JS Type          |
|------------------|------------------|
| `Memory<sbyte>`  | `Int8Array`      |
| `Memory<byte>`   | `UInt8Array`     |
| `Memory<short>`  | `Int16Array`     |
| `Memory<ushort>` | `UInt16Array`    |
| `Memory<int>`    | `Int32Array`     |
| `Memory<uint>`   | `UInt32Array`    |
| `Memory<long>`   | `BigInt64Array`  |
| `Memory<ulong>`  | `BigUInt64Array` |
| `Memory<float>`  | `Float32Array`   |
| `Memory<double>` | `Float64Array`   |
| `ReadOnlyMemory<sbyte>`  | `Int8Array`      |
| `ReadOnlyMemory<byte>`   | `UInt8Array`     |
| `ReadOnlyMemory<short>`  | `Int16Array`     |
| `ReadOnlyMemory<ushort>` | `UInt16Array`    |
| `ReadOnlyMemory<int>`    | `Int32Array`     |
| `ReadOnlyMemory<uint>`   | `UInt32Array`    |
| `ReadOnlyMemory<long>`   | `BigInt64Array`  |
| `ReadOnlyMemory<ulong>`  | `BigUInt64Array` |
| `ReadOnlyMemory<float>`  | `Float32Array`   |
| `ReadOnlyMemory<double>` | `Float64Array`   |

A JavaScript [typed array](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide/Typed_arrays)
represents a contiguous range of bytes or other type of numeric values. In .NET, similar ranges are
represented using `Memory<T>`, or `ReadOnlyMemory<T>` where `T` is a primitive numeric type. (Other
types for `T`, such as a `struct`, are not supported by the JS marshaller.)

When a typed array is marshalled between .NET and JS, the memory becomes _shared_. Only the memory
location and length are copied by the marshaller; the memory contents are not. Any modifications
(assuming it is not read-only) are seen by both sides. The memory will be garbage-collected when
no longer reachable by either side.

[`JSTypedArray<T>`](./dotnet/Microsoft.JavaScript.NodeApi/JSTypedArray-1) supports working directly
with JS typed-array values, and provides the conversions to/from `Memory<T>`.
