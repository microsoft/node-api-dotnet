# Project Overview

This project enables advanced interoperability between .NET and JavaScript in the same process.

 - Load .NET assemblies and call .NET APIs in-proc from a JavaScript application.
 - Load JavaScript packages call JS APIs in-proc from a .NET application.

Interop is [high-performance](/features/performance) and supports [TypeScript type-definitions
generation](/features/type-definitions), [async (tasks/promises)](/marshalling/async-promises),
[streams](/marshalling/streams), [exception propagation](/marshalling/exceptions), and more. It is
built on [Node API](https://nodejs.org/api/n-api.html) so it is compatible with any Node.js version
(without recompiling) or other JavaScript runtime that supports Node API, such as Deno.

:warning: _**Status: Public Preview** - Most functionality works well, though there are some known
limitations around the edges, and there may still be minor breaking API changes._

### Minimal example - JS calling .NET
```JavaScript
const Console = require('node-api-dotnet').System.Console;
Console.WriteLine('Hello from .NET!');
```

### Minimal example - .NET calling JS
```C#
interface IConsole { void Log(string message); }

var nodejs = new NodejsPlatform(libnodePath).CreateEnvironment();
nodejs.Run(() => {
    var console = nodejs.Import<IConsole>("global", "console");
    console.Log("Hello from JS!");
});
```

For more complete example projects, see the
[examples directory in the repo](https://github.com/microsoft/node-api-dotnet/tree/main/examples).
Or proceed to the next page to learn about the different supported scenarios and how to get started
with your own project.