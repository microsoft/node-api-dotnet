---
marp: true
theme: gaia
backgroundColor: #fff
backgroundImage: url('https://marp.app/assets/hero-background.svg')
---
<!--
  Use the Marp CLI or Marp for VS Code to generate slides from this markdown file:
    https://github.com/marp-team/marp-cli
    https://github.com/marp-team/marp-vscode
-->

![bg vertical left:30% 75%](https://raw.githubusercontent.com/dotnet/brand/main/logo/dotnet-logo.svg)
![bg vertical left:30% 75%](https://upload.wikimedia.org/wikipedia/commons/d/d9/Node.js_logo.svg)

<br>

# **Node API for .NET**

High-performance in-proc interop<br/> between .NET and JavaScript

[github.com/microsoft/node-api-dotnet](https://github.com/microsoft/node-api-dotnet)
Jason Ginchereau [@jasongin](https://github.com/jasongin)
Vladimir Morozov [@vmoroz](https://github.com/vmoroz)

---
## .NET<->JS Interop scenarios
1. Dynamically invoke .NET APIs (system or app assemblies) from JS.
2. Develop a Node.js addon module in C#.
3. Host a JS runtime within a .NET app and call JS APIs from .NET.

<br/>

Requirements:
 - .NET 6 or later
 - Windows, Mac, or Linux

---
## Dynamically invoke .NET APIs from JS
```js
const dotnet =require('node-api-dotnet');
dotnet.Console.WriteLine('Hello from .NET!');

const MyAssembly = dotnet.load('path/to/MyAssembly.dll');
const myObj = new MyAssembly.MyClass(...args);
```

<br/><br/>
_Dynamic invocation has some limitations, e.g. some forms of generics, ref/out params. These can be reduced or eliminated with further development._

---
## Develop Node addon modules in C#
```C#
// C#
[JSExport]
public class MyClass {
    public MyClass(string[] args) { }
}
```
```ts
// TypeScript
import { MyClass } from 'my-package';
const myObj = new MyClass(...args);
```

Option: Use .NET Native AOT to avoid .NET Runtime dependency.

---
## Host a JS runtime in a .NET app
### _(Under development)_
.NET app hosts Node.js or another JS engine in-proc, then invokes JS APIs in built-in or 3rd-party modules.
```C#
// C#
var js = await JSEngine.StartAsync(...);
var myModule = await js.ImportAsync("path/to/myjsmodule");
myModule["MyClass"].Call("MyMethod", args);
```

Later it may be possible to define or generate .NET interfaces for JS modules, for strong typing.

---
## Interop features
 - Automatic marshalling for:
   - Classes & interfaces - passed by reference
   - Structs & enums - passed by value
   - Collections - passed by reference
   - TypedArrays - using shared memory
 - Async support:
   - Automatic conversion between Tasks & Promises
   - SynchronizationContext supports `await` return to JS thread

---
## More interop features
 - Error/exception propagation
 - .NET delegates <-> JS callbacks
 - .NET Streams <-> Node Readable/Writable/Duplex streams
 - JS class can extend a .NET class, TS can implement a .NET interface
 - .NET class extend a JS class, implement a TS interface
 - Option to work directly with JS types: `JSValue`, `JSObject`, `JSArray`, `JSMap`, `JSPromise`, etc.

---
## TypeScript type definitions
TypeScript code benefits from type defintions when calling .NET.
 - A tool generates typedef files (`.d.ts`) from .NET assemblies.
 - Nuget package MSBuild scripts automatically do this for C# Node addon projects.

---
## Interop performance
.NET <-> JS interop is fast:
 - Marshaling does not use JSON serialization.
 - Compile-time source generation avoids runtime reflection.
 - Use of shared memory and proxies minimizes the data transfer.
 - `struct`, `Span<T>`, `stackalloc` minimize heap allocation & copying.

JS to .NET calls are [more than twice as fast](https://github.com/jasongin/napi-dotnet/pull/23) when compared to `edge-js` using [that project's benchmark](https://github.com/tjanczuk/edge/wiki/Performance).

---
## Roadmap
Major development areas:
 - Build/packaging/publishing pipelines
 - More marshalling: events, tuples, ref/out params, generics, ...
 - Hosting JS engines in .NET app

Project backlog: https://github.com/users/jasongin/projects/1

