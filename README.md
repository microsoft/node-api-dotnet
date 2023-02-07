# Call .NET APIs from JavaScript

Call nearly any .NET APIs in-proc from JavaScript code, with high performance and TypeScript type-checking. The interop uses [Node API](https://nodejs.org/api/n-api.html) so it is compatible with any Node.js version (without rebuilding) or other JavaScript engine that supports Node API.

_**Status: In Development** - Core functionality works, but many things are incomplete, and it isn't yet all packaged up nicely in a way that can be easily consumed._

### Minimal example
```JavaScript
const Console = require('node-api-dotnet').Console;
Console.WriteLine('Hello from .NET!');
```

## Feature Highlights

### Dynamically load .NET assemblies
.NET core library types are available directly on the main module. Additional .NET assemblies can be loaded by file path:
```JavaScript
const ExampleAssembly = require('node-api-dotnet')
    .loadAssembly('path/to/ExampleAssembly.dll');
const exampleObj = new ExampleAssembly.ExampleClass(...args);
```

.NET namespaces are stripped for convenience, but in case of ambiguity it's possible to get a type by full name:
```JavaScript
const MyType = ExampleAssembly['Namespace.Qualified.MyType'];
```

### Generate type definitions for .NET APIs
If writing TypeScript, or type-checked JavaScript, there is a tool to generate type `.d.ts` type definitions for .NET APIs. It also generates a small `.js` file that exports the assembly in a more natural way as a JS module:
```bash
$ node-api-dotnet-ts-gen "path/to/ExampleAssembly.dll"
Generated ExampleAssembly.js
Generated ExampleAssembly.d.ts
```
```TypeScript
import { ExampleClass } from './ExampleAssembly';
ExampleClass.ExampleMethod(...args); // This call is type-checked!
```

For reference, there is a paging [listing C# type projections to TypeScript](/Docs/typescript.md).

### Full async support
JavaScript code can `await` a call to a .NET method that returns a `Task`. The marshaler automatically sets up a `SynchronizationContext` so that the .NET result is returned back to the JS thread.
```TypeScript
import { ExampleClass } from './ExampleAssembly';
const asyncResult = await ExampleClass.GetSomethingAsync(...args);
```
.NET `Task`s are seamlessly marshaled to & from JS `Promise`s. So JS code can work naturally with a `Promise` returned from a .NET async method, and a JS `Promise` passed to .NET becomes a `JSPromise` that can be `await`ed in the C# code.

### Error propagation
Exceptions/errors thrown in .NET or JS are propagated across the boundary with stack traces.

_Under development. More to be written..._

### Develop Node.js addons with C#
A C# class library project can use the `[JSExport]` attribute to tag (and rename) APIs that are exported when the library is built as a JavaScript module. A [C# Source Generator](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) runs as part of the compilation and generates code to export the tagged APIs and marshal values between JavaScript and C#.

```C#
[JSExport] // Export class and all public members to JS.
public class ExampleClass { ... }

public static class ExampleStaticClass
{
    [JSExport("exampleFunction")] // Export as a module-level function.
    public static string StaticMethod(ExampleClass obj) { ... }

    // (Other public members in this class are not exported by default.)
}
```

The `[JSExport]` source generator enables faster startup time because the marshaling code is generated at build time rather than dynamically emitted at runtime (as when calling a pre-built assembly). The source generator also enables building ahead-of-time compiled libraries in C# that can be called by JavaScript without depending on the .NET Runtime. (More on that below.)

### Optionally work directly with JS types in C#
The class library includes an object model of for JavaScript type system. `JSValue` represents a value of any type, and there are more types like `JSObject`, `JSArray`, `JSMap`, `JSPromise`, etc. C# code can work directly with those types if desired:

```C#
[JSExport]
public static JSPromise JSAsyncExample(JSValue input)
{
    // Example of integration between C# async/await and JS promises.
    string greeter = (string)input;
    return new JSPromise(async (resolve) =>
    {
        await Task.Delay(50);
        resolve((JSValue)$"Hey {greeter}!");
    });
}
```

### Automatic efficient marshaling
There are two ways to get automatic marshaling between C# and JavaScript types:
  1. Compile a C# class library with `[JSExport]` attributes like the examples above. The source generator generates marshaling code that is compiled with the assembly.

  2. Load a pre-built .NET assembly, as in the earlier examples. The loader will use reflection to scan the APIs, then emit marshaling code on-demand for each type that is referenced. The code is logically equivalent to that from the source generator, but is instead emitted as IL using the [.NET System.Reflection.Emit APIs](https://learn.microsoft.com/en-us/dotnet/framework/reflection-and-codedom/emitting-dynamic-methods-and-assemblies). So there is a small startup cost from that reflection and IL emitting, but subsequent calls to the same APIs may be just as fast as the pre-compiled marshaling code (and are just as likely to be JITted).

The marshaler uses the strong typing information from the C# API declarations as hints about how to convert values beteen JavaScript and C#. Here's a general summary of conversions:
  - Primitives (numbers, strings, etc.) are passed by value directy.
  - C# structs have all properties passed by value (shallow copied).
  - C# classes are passed by reference. Any JS call to a C# class or interface property or method gets proxied over to the C# instance of the class. (Object GC lifetimes are synchronized accordingly.)
  - JS code may implement a C# interface, and pass that implementation back to C# code where it becomes a proxy that C# code can use.
  - C# collections like `IList<T>` and JS collections like `Map<T>` are also passed by reference; access to collection elements is proxied to whichever side the real instance of the collection is on.
  - JS `TypedArray`s are mapped to C# `Memory<T>` and passed by reference using shared memory (no proxying is needed).
  - Other types like enums, dates, and delegates are automatically marshaled as one would expect.
  - Custom marshaling and marshaling hints [may be supported later](https://github.com/jasongin/napi-dotnet/pull/25).

### Optional .NET native AOT compilation
This library supports hosting the .NET Runtime in the same process as the JavaScript engine. Alternatively, it also supports building [native ahead-of-time (AOT) compiled C#](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) libraries that are loadable as a JavaScript module _without depending on the .NET Runtime_.

There are advantages and disadvantages to either approach:
|                 | .NET Runtime | .NET Native AoT |
|-----------------|--------------|-----------------|
| API compatibility | Broad compatibility with .NET APIs | Limited compatibility with APIs designed to support AOT |
| Ease of deployment | Requires a matching version of .NET to be installed on the target system | A .NET installation is not required (though some platform libs may be required on Linux/Mac)
| Size of deployment | Compact - only IL assemblies need to be deployed | Larger due to building necessary runtime code - minimum 3 MB on Windows, ~13 MB on Linux |
| Performance     | Slightly slower startup (JIT) | Slightly faster startup (no JIT) |
| Runtime limitations | Full .NET functionality | Some .NET features like reflection and code-generation aren't supported |

### High performance
The project is designed to be as performant as possible when bridging between .NET and JavaScript. Techniques benefitting performance include:
 - Automatic marshaling avoids any use of JSON serialization, and uses generated code to avoid reflection.
 - Automatic marshalling uses shared memory or proxies when possible to minimize the amount of data transferred across the boundary.
 - Simple calls between JS and C# require **_almost_** zero memory allocation. (Maybe it will be zero eventually.)
 - Most JavaScript values are represented in C# as small structs (basically containing just a handle to the JS value), which helps avoid memory allocation.
 - Marshaling code uses modern C# performance features like `Span<T>` and `stackalloc` to minimize heap allocations and copying.

Thanks to these design choices, JS to .NET calls are [more than twice as fast](https://github.com/jasongin/napi-dotnet/pull/23) when compared to `edge-js` using [that project's benchmark](https://github.com/tjanczuk/edge/wiki/Performance).

## Requirements
 - .NET 6 or later
    - .NET 7 or later is required for AoT support.
 - Node.js v16 or later
    - Other JS engines may be supported in the future.
 - OS: Windows, Mac, or Linux
    - It should work any platform where .NET 6 is supported.

## Getting Started
_To be written: instructions for installing npm/nuget packages_

## Development
For information about building, testing, and contributing changes, see [README-DEV.md](./README-DEV.md).
