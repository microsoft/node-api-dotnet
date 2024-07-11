# JavaScript / .NET Type Mappings

This table provides a quick reference for how different .NET and JavaScript runtime types and
concepts are handled by the JS marshaller and type-definitions generator. Follow the links for more
details about each topic.

| Topic                           | Summary|
|---------------------------------|--------|
| [Basic types](./basic-types) | `string` => `string`<br/>`bool` => `boolean`<br/>`byte`, `short`, `int`, `long`, `float`, `double` => `number`
| [Null &amp; undefined](./null-undefined) | .NET `null` => JS `undefined`<br/>JS `null` or `undefined` => .NET `null`
| [Classes &amp; interfaces](./classes-interfaces) | .NET classes can be constructed and used in JS. Class or interface instances are marshalled by reference. JS code can implement .NET interfaces.
| [Structs &amp; tuples](./structs-tuples) | .NET structs can be constructed and used in JS. Struct instances and tuples are marshalled by value.<br/>.NET `Tuple<A,B>` or `ValueTuple<A,B>` => JS `[ A, B ]` (array tuple)
| [Enums](./enums) | .NET enums are projected as TS non-const enums including [reverse mappings](https://www.typescriptlang.org/docs/handbook/enums.html#reverse-mappings).
| [Arrays &amp; collections](./arrays-collections) | .NET `T[]` or `IList<T>` => JS `T[]`<br/>.NET `IDictionary<K,V>` => JS `Map<K,V>`<br/>.NET `IEnumerable<T>` => JS `Iterable<T>`<br/>.NET `Memory<byte>` => JS `Uint8Array`
| [Delegates](./delegates) | .NET `Func<TValue, TReturn>` => JS Function `(TValue) => TRet`
| [Streams](./streams) | .NET `Stream` => Node.js `Duplex`
| [Dates &amp; times](./dates) | .NET `DateTime` => JS `Date`<br/>.NET `DateTimeOffset` => JS `Date`<br/>.NET `TimeSpan` => JS `number` (milliseconds)
| [Other special types](./other-types) | .NET `Guid` => JS `string`<br/>.NET `BigInteger` => JS `bigint`
| [Async &amp; promises](./async-promises) |.NET `Task<T>` => JS `Promise<T>`
| [Ref &amp; out params](./ref-out-params) | .NET `ref` and `out` params are returned via a result object:<br/>C# `bool F(ref string a, out int b)` =><br/>JS `f(a: string) => { a: string, b: int, result: boolean }`
| [Generics](./generics)* | .NET generics are supported in JS, with special `$` syntax and some limitations.
| [Extension methods](./extension-methods)* | .NET extension methods are supported in JS.
| [Overloaded methods](./overloaded-methods) | .NET overloaded methods can be called from JS, though overload resolution has some limitations.
| [Events](./events) | .NET events are [not yet implemented](https://github.com/microsoft/node-api-dotnet/issues/59).
| Fields | .NET `class` or `struct` fields are [not yet implemented](https://github.com/microsoft/node-api-dotnet/issues/63). Use properties instead.
| [Exceptions](./exceptions) | .NET `Exception` is thrown as JS `Error`, with combined stack trace.
| [Namespaces](./namespaces)* | .NET namespaces are preserved on the `node-api-dotnet` module object:<br/>`import dotnet from 'node-api-dotnet';`<br/>`dotnet.System.Console.WriteLine()`

\* These are only supported with the [dynamic invocation scenario](../scenarios/js-dotnet-dynamic).

