---
next:
  text: Dynamic .NET from JS
  link: ./js-dotnet-dynamic
---

# JS / .NET Interop Scenarios

There are four primary scenarios enabled by this project. Choose one of them for getting-started
instructions:

 - [Dynamically invoke .NET APIs from JavaScript](./js-dotnet-dynamic)<br/>
   Dynamic invocation is easy to set up: all you need is the `node-api-dotnet` package and the
   path to a .NET assembly you want to call. It is not quite as fast as a C# addon module because
   marshalling code must be generated at runtime. It is best suited for simpler projects with
   limited interop needs.

 - [Develop a Node.js addon module in C#](./js-dotnet-module)<br/>
   A C# Node module exports specific types and methods from the module to JavaScript. It can be
   faster because marshalling code is generated at compile time, and the shape of the APIs
   exposed to JavaScript can be designed or adapted with JS interop in mind. It is best suited
   for more complex projects with advanced or high-performance interop requirements.

 - [Develop a Node.js addon module in C# with .NET Native AOT](./js-aot-module)<br/>
   A variation on the previous scenario, this is best suited for creation of a re-usable Node.js
   native addon that loads quickly and does not depend the .NET runtime. However binaries are
   platform-specific, so packaging and distribution is more difficult.

 - [Embed a JS runtime in a .NET application](./dotnet-js)<br/>
   Run Node.js (or another JS runtime) in a .NET application process, import `npm` packages
   and/or JavaScript module files and call them from .NET.

All of these scenarios support
[auto-generated TypeScript type definitions](../features/type-definitions),
[automatic efficient marshalling](../features/automatic-marshalling),
and [error propagation](../marshalling/exceptions).
