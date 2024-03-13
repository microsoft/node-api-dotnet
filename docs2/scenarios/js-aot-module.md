# Develop a Node.js addon module in C# with .NET Native AOT

### About .NET Native AOT
AOT compiled modules load more quickly and _do not have any runtime dependency .NET_. However,
.NET Native AOT has some limitations, so you should understand the implications before starting
on this path. Some of the considerations include:
  - .NET 8 SDK or later is required at build time. (Not at run time.)
  - AOT binaries are much larger: at least 4-10 MB depending on the platform.
  - AOT code can only call other native code. That may include other .NET Native AOT assemblies,
    but NOT any managed .NET assemblies, because the .NET runtime is not loaded.
  - No dynamic loading, reflection, or runtime code-generation is possible.
  - Some .NET APIs and libraries are not compatible with AOT.
For more details, see https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/

### Enabling AOT in a C# project
To set up a project for building a Node.js addon module using C# Native AOT, start with the
[steps for building a regular .NET module](./js-dotnet-module).

Then enable .NET Native AOT compilation in the C# project:
  1. Make sure the `TargetFramework` is .NET 8 or later.
  2. Add the publishing properties to the project file:
```xml
<PublishAot>true</PublishAot>
<PublishNodeModule>true</PublishNodeModule>
```
  3. Publish the project to produce the native module (with `.node` extension):
```shell
dotnet publish
```

A native module does not depend on the `node-api-dotnet` package, so it can be removed from the
JavaScript project's `package.json`. Then update the JavaScript code to `require()` the .NET
AOT module directly. Be sure to reference the published `.node` file location, which might be
different from the built `.dll` location.

::: code-group
```JavaScript [CommonJS]
/** @type {import('./bin/ExampleModule')} */
const ExampleModule = require('./bin/ExampleModule');
```
```JavaScript [ES]
/** @type {import('./bin/ExampleModule')} */
import ExampleModule from './bin/ExampleModule';
```