# Marshalling between .NET and JavaScript

In this project, the term "marshalling" refers the process of passing complex values between .NET
and JavaScript runtimes while applying necessary conversions and adapters. The marshaller component
of `node-api-dotnet` enables JavaScript code to call .NET APIs, pass complex parameters and receive
results, implement .NET interfaces, handle exceptions, work with shared memory, all in a manner
that is strongly-typed and (mostly) natural. Marshalling is bi-directional, so it also supports
.NET code calling JS APIs, and callbacks (delegates) in either direction.

This page describes how the marshaller works generally. For details about how specific types and
language constructs are handled by the marshaller, refer to
[JavaScript / .NET Type Mappings](../reference/js-dotnet-types.md).

## Adapting JavaScript calls to .NET

At a lower level, all calls from JS come into .NET as invocations of the
[`JSCallback`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSCallback) delegate:
```C#
delegate JSValue JSCallback(JSCallbackArgs args);
```

An important job of the marshaller is to adapt that low-level callback to an invocation of a .NET
method with specific parameter and return types. For a typical method call, this involves the
following steps:

1. Get the .NET object for the `this` argument. For a static method call (or constructor) the
`this` value is ignored. But for an instance method it is a JS object that "wraps" a .NET
object (see [Object lifetimes](#object-lifetimes) below), so the .NET object is obtained by calling
[`JSValue.Unwrap()`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValue/Unwrap) on the
`this` value.
2. Marshal each of the parameters (of type
[`JSValue`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSValue)) to their .NET equivalents.
This may involve use of conversions and adapters, managing object lifetimes, and other details
[depending on the types being marshalled](../reference/js-dotnet-types).
3. If the method is overloaded, [resolve the correct overload](../reference/overloaded-methods).
4. Invoke the .NET method.
5. Marshal the .NET return value (if not void) back to a `JSValue`.
6. Handle any .NET exception and re-throw as JS `Error` with combined stack trace.
See [Exceptions](../reference/exceptions).

It could be possible to reflect on the .NET method to be invoked to discover parameter and return
type info, and use that type info from reflection to drive the marshalling logic on every
invocation. But .NET reflection is inefficient, and not supported by .NET Native AOT. So for better
performance and native AOT compability, the JS marshaller relies on code-generation to minimize
reflection.

## Marshalling code generation

The [`JSMarshaller`](../reference/dotnet/Microsoft.JavaScript.NodeApi.DotNetHost/JSMarshaller)
class is responsible for generating code for converting values between JS and .NET environments.
Code generation is achieved by building
[expression trees](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/)
for converting basic types, and then combining the expression trees to convert more complex types.

While expression trees have some limitations in that they cannot represent some more advanced C#
language features, they are sufficiently expressive to support the requirements of marshalling JS
values to and from .NET, which mostly inovlves straightforward calls to constructors, properties,
and methods. Generics are more complex to work with but still supported by expression trees.

### Runtime code generation

The `JSMarshaller` uses .NET reflection on classes, methods, parameters, etc. to generate
marshalling expressions, then dynamically
[compiles the expressions to delegates](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/expression-trees/expression-trees-execution).
Once compiled, the delegates are cached in memory so repeated invocations no longer require
reflection or code-generation. The compiled delegates are registered as Node API JS callbacks for a
[`JSFunction`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSFunction) or
[`JSClassBuilder`](../reference/dotnet/Microsoft.JavaScript.NodeApi.Interop/JSClassBuilder-1),
so JS calls will directly invoke the compiled delegates.

Runtime code-generation is used exclusively when
[dynamically invoking .NET APIs from JS](../scenarios/js-dotnet-dynamic.md), since in that scenario
there is no compile step.

### Compile-time code generation

The `JSMarshaller` can also be used in the context of a
[C# source generator](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview)
to generate marshalling code at compile time. In this scenario, the marshaller still generates
expression trees, but then the expression trees are emitted as C# source code. (Emitting expression
trees as C# code is not a capability provided by the expressions library, but it is not very
difficult to [traverse an expression tree and emit C# syntax for each node](https://github.com/microsoft/node-api-dotnet/blob/main/src/NodeApi.Generator/ExpressionExtensions.cs), especially when the
trees are known to use a limited subset of node types.)

Compile-time code generation is used when
[building a .NET module for Node.js](../scenarios/js-dotnet-module.md). Referencing the
`Microsoft.JavaScript.NodeApi.Generator` nuget package from a C# project registers the source
generator, which then processes any
[`[JSExport]`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSExportAttribute) or
[`[JSModule]`](../reference/dotnet/Microsoft.JavaScript.NodeApi.Interop/JSModuleAttribute)
attributes in the project being compiled. The generated C# marshalling code is then compiled
into the project output assembly. With this approach, runtime reflection and code generation
are not required, so the startup time is faster compared to dynamic invocation. (However the
performance of subsequent marshalling operations should be the same.)

The other major benefit of compile-time code generation is that it works with .NET Native AOT,
where runtime reflection is not supported. That makes it possible to
[build a native Node.js module](../scenarios/js-aot-module.md) that does not depend on the .NET
runtime being installed or redistributed.

## Object lifetimes

.NET and JavaScript runtimes both use garbage-collection to free up memory after objects are no
longer reachable by the code execution. So when objects are passed between .NET and JS
environments, the marshaller manages the object lifetimes to prevent memory leaks and prevent
objects from being released when they are still referenced from the other side.

### Marshalling by reference vs by value

The lifetime management applies to .NET `class` and `interface` types (including collections),
which are marshalled by reference. That means if a .NET `class` instance (including an unknown
class that implements some declared `interface`) is passed to and from JavaScript, then the same
instance is received every time; in other words the values will have reference equality across
multiple marshalling operations.

The lifetime management and reference equality does not apply to .NET `struct` types, which are
marshalled by value -- even though JavaScript does not have stack-allocated value-types like .NET.
This means every time a .NET `struct` instance is passed to JS, a new temporary JS object is
created, similar to
[.NET boxing](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/boxing-and-unboxing).
