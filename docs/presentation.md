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
## Project Vision

First-class in-proc interopability between .NET and JavaScript
<br/>

### Motivation
.NET and JavaScript are widely used, and often complementary, but there is not a great interop story between them.

While some limited solutions exist, there are opportunities to do better in many ways!

---
## Primary .NET / JS Interop scenarios
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
.NET app hosts Node.js or another JS engine in-proc, then uses interop capabilities for deep integration between .NET and JS code.

---
## Demo
Calling Azure OpenAI from JavaScript
using the .NET [Semantic Kernel](https://github.com/microsoft/semantic-kernel) library

---
## About Node API
 - C-style API - easy to invoke from C# or other languages
 - ABI stable - works across Node.js versions without recompiling
 - JS engine agnostic - implemented by several JS engines, not just V8

<br/>
<br/>
Developers of the Node API for .NET project are current/former members of the Node API working group.

---
## .NET / JS Interop features
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
 - .NET delegates / JS callbacks
 - .NET Streams / Node Readable, Writable, Duplex streams
 - JS class can extend a .NET class, TS can implement a .NET interface
 - .NET class extend a JS class, implement a TS interface
 - Option to work directly with JS types
   - `JSValue`, `JSObject`, `JSArray`, `JSMap`, `JSPromise`, etc.

---
## TypeScript type definitions
JavaScript or TypeScript code can reference type defintions for .NET APIs.
 - A tool generates typedef files (`.d.ts`) from .NET assemblies.
 - Nuget package MSBuild scripts automatically do this for C# Node addon projects.

---
## Interop performance
.NET / JS interop is fast because:
 - Marshaling does not use JSON serialization.
 - Compile-time or runtime code generation avoids reflection.
 - Use of shared memory and proxies minimizes data transfer.
 - `struct`, `Span<T>`, `stackalloc` minimize heap allocation & copying.

---
## Performance comparison
Warm JS to .NET calls are nearly twice as fast when compared to `edge-js` using [that project's benchmark](https://github.com/tjanczuk/edge/wiki/Performance).

|      |  HTTP | Edge.js | Node API .NET | AOT | JS (baseline) |
|-----:|------:|--------:|--------------:|----:|--------------:|
| Cold | 32039 |   38761 |          9355 | 362 |          1680 |
| Warm |  2003 |      87 |            54 |  47 |            23 |

Numbers are _microseconds_. "Warm" is an average of 10000 .NET -> JS calls (passing a medium-size object).

---
## Project status
 - Available for early experimentation
   - Send feedback, bug reports.
   - Help prioritize areas for improvement.
   - Contribute PRs!
 - Current limitations may block some advanced interop scenarios.
 - NOT production ready

---
## Roadmap
Major development areas:
 - Build/packaging/publishing pipelines
 - More marshalling: events, tuples, ref/out params, generics, ...
 - Hosting JS engines in .NET app
 - More test coverage

Project backlog: https://github.com/users/jasongin/projects/1

