# Structs and Tuples

## Structs

.NET structs are projected to TypeScript as classes, however the JS marshaller handles them very
differently from [.NET classes](./classes-interfaces#marshalling-net-classes-to-js). .NET structs
are _marshalled by value_. This has a few implications:
 - Every time a .NET `struct` instance is marshalled to JS, a new instance of the corresponding JS
   `class` is constructed, with copied/marshalled property values.
 - Every time a JS `class` instance is marshalled to a .NET `struct`, a new instance of the `struct`
   is constructed, with copied/marshalled property values.
 - There is no [object lifetime](../features/js-dotnet-marshalling#object-lifetimes) relationship
   between a .NET struct instance and a JS class instance created from it.
 - Getting or setting an instance property on a JS `class` instance created from a .NET `struct`
   _does not_ invoke the .NET getter / setter code (if any).
 - Invoking an instance method on a JS `class` instance created from a .NET `struct` _does_ invoke
   the .NET instance method, though the `struct` gets entirely copied/marshalled from JS to .NET
   before every .NET method invocation.

### Read-only properties

Read-only properties of .NET `struct` types are supported by the marshaller if they are
_initializable_, that is if they are defined as `{ get; init; }` and not just `{ get; }`. When
marshalling a JS object to a .NET `struct`, the .NET properties are all initialized by marshalling
the property values from the JS object.

### Static members

Static properties and methods on a .NET `struct` work the same as on a `class`, since static members
do not deal with an instance that gets marshalled by value. (The `struct` _type_ object is still
marshalled by reference.)

## Tuples

| C# Type                                    | JS Type     |
|--------------------------------------------|-------------|
| `Tuple`<br/>`ValueTuple`                   | `[]`        |
| `Tuple<A>`<br/>`ValueTuple<A>`             | `[A]`       |
| `Tuple<A, B, …>`<br/>`ValueTuple<A, B, …>` | `[A, B, …]` |

.NET tuples, including all variations of `Tuple<>` and `ValueTuple<>`, are marshalled as JS arrays,
and the tuple types are projected as TypeScript
[array tuples](https://www.typescriptlang.org/docs/handbook/2/objects.html#tuple-types).
(Tuple field names, if any, are not used in JS.)

```C#
[JSExport]
public static (string Name, double Value) GetNameAndValue();
```
```TS
export function getNameAndValue(): [string, number];
```
