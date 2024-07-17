# Node API for .NET: JavaScript + .NET Interop

This project enables advanced interoperability between .NET and JavaScript in the same process.

 - Load .NET assemblies and call .NET APIs in-proc from a JavaScript application.
 - Load JavaScript packages and call JS APIs in-proc from a .NET application.

Interop is high-performance and supports TypeScript type-definitions generation, async
(tasks/promises), streams, and more. It uses [Node API](https://nodejs.org/api/n-api.html) so
it is compatible with any Node.js version (without recompiling) or other JavaScript runtime that
supports Node API.

:warning: _**Status: Public Preview** - Most functionality works well, though there are some known
limitations around the edges, and there may still be minor breaking API changes._

### Documentation

Getting-started guidance, feature details, and API reference documentation are published at
https://microsoft.github.io/node-api-dotnet

### Minimal example - JS calling .NET
```JavaScript
// JavaScript
const Console = require('node-api-dotnet').System.Console;
Console.WriteLine('Hello from .NET!'); // JS writes to the .NET console API
```

### Minimal example - .NET calling JS
```C#
// C#
interface IConsole { void Log(string message); }

var nodejs = new NodejsPlatform(libnodePath).CreateEnvironment();
nodejs.Run(() =>
{
    var console = nodejs.Import<IConsole>("global", "console");
    console.Log("Hello from JS!"); // C# writes to the JS console API
});
```

## Packages

Depending on the [scenario](https://microsoft.github.io/node-api-dotnet/scenarios/),
either NPM or NuGet packages may be used:
 - NPM: [`node-api-dotnet`](https://www.npmjs.com/package/node-api-dotnet)
 - NuGet: [`Microsoft.JavaScript.NodeApi`](https://www.nuget.org/packages/Microsoft.JavaScript.NodeApi/)

See [Packages & Releases](https://microsoft.github.io/node-api-dotnet/reference/packages-releases.html)
for details.

## Development

For information about building, testing, and debugging this project, see
[README-DEV.md](./README-DEV.md).

Contributions require agreement to the
[Contributor License Agreement](https://microsoft.github.io/node-api-dotnet/contributing.html#contributor-license-agreement).

## Code of Conduct

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

![.NET + JS scene](./docs/images/dotnet-bot_scene_coffee-shop.png)
