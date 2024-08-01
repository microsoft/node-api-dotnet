# Arrays and Collections

## Arrays

| C# Type  | JS Type            |
|----------|--------------------|
| `T[]`    | `T[]` (`Array<T>`) |

.NET arrays are **marshalled by value** to or from JS. (This would not be the preferred design, but
unfortunately there is no way to create a .NET array over "external" memory, that is memory not
allocated / managed by the .NET runtime.) This means that whenever a .NET array instance is
marshalled to or from JS, all the array items are copied. Use `IList<T>` or another collection
interface to avoid copying the items.

## Generic Interfaces

| C# Type                             | JS Type                     |
|-------------------------------------|-----------------------------|
| `IEnumerable<T>`                    | `Iterable<T>`               |
| `IAsyncEnumerable<T>`               | `AsyncIterable<T>`          |
| `IReadOnlyCollection<T>`            | `Iterable<T> \| { length }` |
| `ICollection<T>`                    | `Iterable<T> \| { length, add(), delete() }` |
| `IReadOnlySet<T>`                   | `ReadonlySet<T>`            |
| `ISet<T>`                           | `Set<T>`                    |
| `IReadOnlyList<T>`                  | `readonly T[]` (`ReadonlyArray<T>`) |
| `IList<T>`                          | `T[]` (`Array<T>`)          |
| `IReadOnlyDictionary<TKey, TValue>` | `ReadonlyMap<TKey, TValue>` |
| `IDictionary<TKey, TValue>`         | `Map<TKey, TValue>`         |

Generic collection interfaces in the `System.Collections.Generics` namespace are **marshalled by
reference**. This means passing an instance of a collection between .NET and JS does not immediately
copy any values, and any modifications to the collection affect both .NET and JS.

JavaScript collections can be adapted to .NET collection interfaces using the extension methods
in [`JSCollectionExtensions`](./dotnet/Microsoft.JavaScript.NodeApi.Interop/JSCollectionExtensions).

## Generic classes

| C# Type                          | JS Type               |
|----------------------------------|-----------------------|
| `List<T>`                        | `T[]` (`Array<T>`)    |
| `Queue<T>`                       | `T[]` (`Array<T>`)    |
| `Stack<T>`                       | `T[]` (`Array<T>`)    |
| `HashSet<T>`                     | `Set<T>`              |
| `SortedSet<T>`                   | `Set<T>`              |
| `Dictionary<TKey, TValue>`       | `Map<TKey, TValue>`   |
| `SortedDictionary<TKey, TValue>` | `Map<TKey, TValue>`   |
| `SortedList<TKey, TValue>`       | _not yet implemented_ |
| `LinkedList<T>`                  | _not yet implemented_ |
| `PriorityQueue<T, TPriority>`    | _not yet implemented_ |
| `SynchronizedCollection<T>`      | _not yet implemented_ |

Generic collection classes in the `System.Collections.Generics` namespace are **marshalled by
value**. (This is because the classes are `sealed` and cannot be overridden to proxy calls to JS.)
To avoid copying, usse collection interfaces instead, which is the
[recommended practice for public APIs](https://learn.microsoft.com/en-us/archive/blogs/kcwalina/why-we-dont-recommend-using-listt-in-public-apis)
anyway.

## ObjectModel classes

| C# Type                             | JS Type                    |
|-------------------------------------|----------------------------|
| `Collection<T>`                    | `T[]` (`Array<T>`)          |
| `ReadOnlyCollection<T>`            | `readonly T[]` (`ReadonlyArray<T>`) |
| `ReadOnlyDictionary<TKey, TValue>` | `ReadonlyMap<TKey, TValue>` |
| `KeyedCollection<TKey, TValue>`    | _not yet implemented_       |
| `ObservableCollection<T>`          | _not yet implemented_       |
| `ReadOnlyObservableCollection<T>`  | _not yet implemented_       |

Some generic collection classes in the `System.Collections.ObjectModel` namespace are supported
and are **marshalled by reference**. However, using collection interfaces instead is still
recommended.

## Generic key-value pair

| C# Type                             | JS Type               |
|-------------------------------------|-----------------------|
| `KeyValuePair<TKey, TValue>`        | `[TKey, TValue]`      |

A generic key-value pair is marshalled as a JavaScript array-tuple. This is the same element
structure as that used by JavaScript's `Map.entries()` API.

(For .NET tuple types, see [Tuples](./structs-tuples.md#tuples).)

## Non-generic interfaces and classes

| C# Type       | JS Type               |
|---------------|-----------------------|
| `IEnumerable` | _not yet implemented_ |
| `ICollection` | _not yet implemented_ |
| `IList`       | _not yet implemented_ |
| `IDictionary` | _not yet implemented_ |
| `ArrayList`   | _not yet implemented_ |
| `BitArray`    | _not yet implemented_ |
| `Hashtable`   | _not yet implemented_ |
| `Queue`       | _not yet implemented_ |
| `SortedList`  | _not yet implemented_ |
| `Stack`       | _not yet implemented_ |

Non-generic collection types in the `System.Collections` namespace are
[not yet implemented](https://github.com/microsoft/node-api-dotnet/issues/243), since they are
rarely used in modern C# code, moreover the lack of strong typing would require more complex
dynamic marshalling.

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
