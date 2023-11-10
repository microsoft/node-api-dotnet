# Node API for .NET: JavaScript + .NET Interop

This project enables advanced interoperability between .NET and JavaScript in the same process.

 - Load .NET assemblies and call .NET APIs in-proc from a JavaScript application.
 - Load JavaScript packages call JS APIs in-proc from a .NET application.

Interop is high-performance and supports TypeScript type-definitions generation, async
(tasks/promises), streams, and more. It uses [Node API](https://nodejs.org/api/n-api.html) so
it is compatible with any Node.js version (without recompiling) or other JavaScript runtime that
supports Node API.

:warning: _**Status: Public Preview** - Most functionality works well, though there are some known
limitations around the edges, and there may still be minor breaking API changes._

[Instructions for getting started are below.](#getting-started)

### Minimal example - JS calling .NET
```JavaScript
// JavaScript
const Console = require('node-api-dotnet').System.Console;
Console.WriteLine('Hello from .NET!');
```

### Minimal example - .NET calling JS
```C#
// C#
interface IConsole { void Log(string message); }

var nodejs = new NodejsPlatform(libnodePath).CreateEnvironment();
nodejs.Run(() => {
    var console = nodejs.Import<IConsole>("global", "console");
    console.Log("Hello from JS!");
});
```

For more examples, see the [examples](./examples/) directory.

## Feature Highlights
 - [Load and call .NET assemblies from JS](#load-and-call-net-assemblies-from-js)
 - [Load and call JavaScript packages from .NET](#load-and-call-javascript-packages-from-net)
 - [Generate TS type definitions for .NET APIs](#generate-ts-type-definitions-for-net-apis)
 - [Full async support](#full-async-support)
 - [Error propagation](#error-propagation)
 - [Develop Node.js addons with C#](#develop-nodejs-addons-with-c)
 - [Optionally work directly with JS types in C#](#optionally-work-directly-with-js-types-in-c)
 - [Automatic efficient marshaling](#automatic-efficient-marshaling)
 - [Stream across .NET and JS](#stream-across-net-and-js)
 - [Optional .NET native AOT compilation](#optional-net-native-aot-compilation)
 - [High performance](#high-performance)
 - [Work with .NET generic APIs in JS](#generics)

### Load and call .NET assemblies from JS
The `node-api-dotnet` package manages hosting the .NET runtime in the JS process
(if not using AOT - see below). The .NET core library types are available immediately;
additional .NET assemblies can be loaded by file path:
```JavaScript
// JavaScript
const dotnet = require('node-api-dotnet');
dotnet.load('path/to/ExampleAssembly.dll');
const exampleObj = new dotnet.ExampleNamespace.ExampleClass(...args);
```

All .NET namespaces are projected onto the top-level `dotnet` object. When loading multiple .NET
assemblies, types from all assemblies are merged into the same namespace hierarchy.

### Load and call JavaScript packages from .NET
Calling JavaScript from .NET requires hosting a JS runtime such as Node.js in the .NET app.
Then JS packages can be imported either directly as JS values or by declaring C# interfaces for
the JS types and using automatic marshalling.

All interaction with a JavaScript environment must be from its thread, via the
`Run()`, `RunAsync()`, or `Post()` methods on the JS environment object.
```C#
// C#
interface IExample
{
    void ExampleMethod();
}

var nodejsPlatform = new NodejsPlatform(libnodePath);
var nodejs = nodejsPlatform.CreateEnvironment();

nodejs.Run(() => {
    // Import a module property, then call a function on it.
    var example1 = nodejs.Import("example-npm-package", "ExampleObject");
    example1.CallMethod("exampleMethod");

    // Import the module property using an interface, and call the same function.
    var example2 = nodejs.Import<IExample>("example-npm-package", "ExampleObject");
    example2.ExampleMethod();
});
```

In the future, it may be possible to automatically generate .NET API definitions from TypeScript
type definitions.

### Generate TS type definitions for .NET APIs
If writing TypeScript, or type-checked JavaScript, there is a tool to generate type `.d.ts` type
definitions for .NET APIs. It also generates a small `.js` file that makes it simple to load the
assembly DLL and type-definitions together.
```bash
$ npm exec node-api-dotnet-generator --assembly ExampleAssembly.dll --typedefs ExampleAssembly.d.ts
```
```TypeScript
// TypeScript
import './ExampleAssembly.js'; // TS also loads the adjacent .d.ts file.
dotnet.ExampleNamespace.ExampleClass.ExampleMethod(...args); // This call is type-checked!
```

(CommonJS modules must use `require()` instead of `import`.)

For reference, there is a [list of C# type projections to TypeScript](/Docs/typescript.md).

### Full async support
JavaScript code can `await` a call to a .NET method that returns a `Task`. The marshaller
automatically sets up a `SynchronizationContext` so that the .NET result is returned back to the
JS thread.
```TypeScript
// TypeScript
import { ExampleClass } from './ExampleAssembly';
const asyncResult = await ExampleClass.GetSomethingAsync(...args);
```
.NET `Task`s are seamlessly marshaled to & from JS `Promise`s. So JS code can work naturally with
a `Promise` returned from a .NET async method, and a JS `Promise` passed to .NET becomes a
`JSPromise` that can be `await`ed in the C# code.

### Error propagation
Exceptions/errors thrown in .NET or JS are propagated across the boundary with stack traces.
An unhandled .NET exception is thrown back to a JS caller as an `Error` with a stack trace that
includes both .NET and JS frames, and source line numbers if symbols are available. For example:
```
Error: Test error thrown by JS.
    at Microsoft.JavaScript.NodeApi.TestCases.Errors.ThrowDotnetError(String message) in D:\node-api-dotnet\test\TestCases\Errors.cs:line 13
    at Microsoft.JavaScript.NodeApi.Generated.Module.Errors_ThrowDotnetError(JSCallbackArgs __args) in napi-dotnet.NodeApi.g.cs:line 357
    at Microsoft.JavaScript.NodeApi.JSNativeApi.InvokeCallback[TDescriptor](napi_env env, napi_callback_info callbackInfo, JSValueScopeType scopeType, Func`2 getCallbackDescriptor) in JSNativeApi.cs:line 1070
    at catchDotnetError (D:\node-api-dotnet\test\TestCases\errors.js:14:12)
    at Object.<anonymous> (D:\node-api-dotnet\test\TestCases\errors.js:41:1)
```
Similarly, an unhandled JS `Error` is thrown back to a .NET caller as a `JSException` with a
combined stack trace.

### Develop Node.js addons with C#
A C# class library project can use the `[JSExport]` attribute to tag (and rename) APIs that are
exported when the library is built as a JavaScript module. A [C# Source Generator](
https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) runs as
part of the compilation and generates code to export the tagged APIs and marshal values between
JavaScript and C#.

```C#
// C#
[JSExport] // Export class and all public members to JS.
public class ExampleClass { ... }

public static class ExampleStaticClass
{
    [JSExport("exampleFunction")] // Export as a module-level function.
    public static string StaticMethod(ExampleClass obj) { ... }

    // (Other public members in this class are not exported by default.)
}
```

The `[JSExport]` source generator enables faster startup time because the marshaling code is
generated at build time rather than dynamically emitted at runtime (as when calling a pre-built
assembly). The source generator also enables building ahead-of-time compiled libraries in C# that
can be called by JavaScript without depending on the .NET Runtime. (More on that below.)

### Optionally work directly with JS types in C#
The class library includes an object model for the JavaScript type system. `JSValue` represents a
value of any type, and there are more types like `JSObject`, `JSArray`, `JSMap`, `JSPromise`, etc.
C# code can work directly with those types if desired:

```C#
// C#
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
  1. Compile a C# class library with `[JSExport]` attributes like the examples above. The source
  generator produces marshaling code that is compiled with the assembly.

  2. Load a pre-built .NET assembly, as in the earlier examples. The loader will use reflection to
  scan the APIs, then emit marshaling code on-demand for each type that is referenced by JS. The
  dynamic marshalling code is derived from the same expression trees that are used for compile-time
  source-generation, but is generated and at runtime and compiled with
  [`LambdaExpression.Compile()`](https://learn.microsoft.com/en-us/dotnet/api/system.linq.expressions.lambdaexpression.compile).
  So there is a small startup cost from that reflection and compilation, but subsequent calls to
  the same APIs may be just as fast as the pre-compiled marshaling code (and are just as likely
  to be JITted).

The marshaller uses the strong typing information from the C# API declarations as hints about how to
convert values beteen JavaScript and C#. Here's a general summary of conversions:
  - Primitives (numbers, strings, etc.) are passed by value directy.
  - C# structs have all properties passed by value (shallow copied).
  - C# classes are passed by reference. Any JS call to a C# class or interface property or method
  gets proxied over to the C# instance of the class. (Object GC lifetimes are synchronized
  accordingly.)
  - JS code may implement a C# interface, and pass that implementation back to C# code where it
  becomes a proxy that C# code can use.
  - C# collections like `IList<T>` and JS collections like `Map<T>` are also passed by reference;
  access to collection elements is proxied to whichever side the real instance of the collection
  is on.
  - JS `TypedArray`s are mapped to C# `Memory<T>` and passed by reference using shared memory
  (no proxying is needed).
  - Other types like enums, dates, and delegates are automatically marshaled as one would expect.
  - Custom marshaling and marshaling hints [may be supported later](
    https://github.com/jasongin/napi-dotnet/pull/25).

### Stream across .NET and JS
.NET `Stream`s are automatically marshalled to and from Node.js `Duplex` (or `Readable` or
`Writable`) streams. That means JS code can seamlessly read from or write to streams created
by .NET. Or .NET code can read from or write to streams created by JS. Streamed data is
transferred using shared memory (without any additional sockets or pipes), so memory allocation
and copying is minimized.

### Optional .NET native AOT compilation
This library supports hosting the .NET Runtime in the same process as the JavaScript runtime.
Alternatively, it also supports building [native ahead-of-time (AOT) compiled C#](
https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) libraries that are
loadable as a JavaScript module _without depending on the .NET Runtime_.

There are advantages and disadvantages to either approach:
|                     | .NET Runtime | .NET Native AOT |
|---------------------|--------------|-----------------|
| API compatibility   | Broad compatibility with .NET APIs | Limited compatibility with APIs designed to support AOT |
| Ease of deployment  | Requires a matching version of .NET to be installed on the target system | A .NET installation is not required (though some platform libs may be required on Linux/Mac)
| Size of deployment  | Compact - only IL assemblies need to be deployed | Larger due to bundling necessary runtime code - minimum ~3 MB per platform |
| Performance         | Slightly slower startup (JIT) | Slightly faster startup (no JIT) |
| Runtime limitations | Full .NET functionality | Some .NET features like reflection and code-generation aren't supported |

### High performance
The project is designed to be as performant as possible when bridging between .NET and JavaScript.
Techniques benefitting performance include:
 - Automatic marshaling avoids any use of JSON serialization, and uses generated code to avoid
 reflection.
 - Automatic marshalling uses shared memory or proxies when possible to minimize the amount of
 data transferred across the boundary.
 - Simple calls between JS and C# require **_almost_** zero memory allocation. (Maybe it will be
 zero eventually.)
 - Most JavaScript values are represented in C# as small structs (basically containing just a
 handle to the JS value), which helps avoid memory allocation.
 - Marshaling code uses modern C# performance features like `Span<T>` and `stackalloc` to minimize
 heap allocations and copying.

Thanks to these design choices, JS to .NET calls are [more than twice as fast](
    https://github.com/jasongin/napi-dotnet/pull/23) when compared to `edge-js` using
    [that project's benchmark](https://github.com/tjanczuk/edge/wiki/Performance).

### Generics
Even though the JavaScript runtime type system lacks generics, it is possible to work with .NET
generic types and methods from JavaScript:

```JavaScript
// JavaScript
System.Enum.Parse$(System.DayOfWeek)('Tuesday');
```

For details, see [Using .NET Generics in JavaScript](./docs/generics.md).

## Getting Started
#### Requirements
 - .NET 6 or later
    - .NET 8 or later is required for AOT support.
    - .NET Framework 4.7.2 or later is supported at runtime,
      but .NET 6 SDK is still required for building.
 - Node.js v16 or later
    - Other JS runtimes may be supported in the future.
 - OS: Windows, Mac, or Linux
    - It should work on any platform where .NET 6 is supported.

#### Instructions
For calling .NET from JS, choose between one of the following scenarios:
 - [Dynamically invoke .NET APIs from JavaScript](./Docs/dynamic-invoke.md)<br/>
   Dynamic invocation is simpler to set up: all you need is the `node-api-dotnet` npm package and
   the path to a .NET assembly you want to call. But it has some limitations (not all kinds of APIs
   are supported), and is not quite as fast as a C# module, because marshalling code must be
   generated at runtime.
 - [Develop a Node module in C#](./Docs/node-module.md)<br/>
   A C# Node module is appropriate for an application that has more advanced interop needs. It can
   be faster because marshalling code can be generated at compile time, and the shape of the APIs
   exposed to JavaScript can be adapted with JS interop in mind.

For calling JS from .NET, more documentation will be added soon. For now, see the
[`winui-fluid` example code](./examples/winui-fluid/).

Generated TypeScript type definitions can be utilized with any of these aproaches.

## Development
For information about building, testing, and contributing changes to this project, see
[README-DEV.md](./README-DEV.md).

## Contributing

This project welcomes contributions and suggestions.  Most contributions require you to agree to a
Contributor License Agreement (CLA) declaring that you have the right to, and actually do, grant us
the rights to use your contribution. For details, visit https://cla.opensource.microsoft.com.

When you submit a pull request, a CLA bot will automatically determine whether you need to provide
a CLA and decorate the PR appropriately (e.g., status check, comment). Simply follow the
instructions provided by the bot. You will only need to do this once across all repos using our CLA.

This project has adopted the
[Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the
[Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or
comments.

## Trademarks

This project may contain trademarks or logos for projects, products, or services. Authorized use of
Microsoft trademarks or logos is subject to and must follow
[Microsoft's Trademark & Brand Guidelines](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks/usage/general).
Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion
or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those
third-party's policies.

<br/>
<br/>

![.NET + JS scene](./Docs/images/dotnet-bot_scene_coffee-shop.png)
