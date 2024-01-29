## Develop a Node module in C#

For a minimal example of this scenario, see
[../examples/dotnet-module/](../examples/dotnet-module/).

1. Create a .NET Class library project that targets .NET 6 or later. (.NET 8 for AOT.)
    ```
    mkdir ExampleModule
    cd ExampleModule
    dotnet new classlib --framework net6.0
    ```

2. Add a reference to the `Microsoft.JavaScript.NodeApi` and
   `Microsoft.JavaScript.NodeApi.Generator` packages:
    ```
    dotnet add package --prerelease Microsoft.JavaScript.NodeApi
    dotnet add package --prerelease Microsoft.JavaScript.NodeApi.Generator
    ```
    > :warning: Until these packages are published, you'll need to
    [build them from source](../README-DEV.md).<br>Then add the `out/pkg` directory as a local
    package source in your `NuGet.config`.

    Afterward you should have the two references in your project file:
    ```xml
    <ItemGroup>
      <PackageReference Include="Microsoft.JavaScript.NodeApi" Version="0.4.*-*" />
      <PackageReference Include="Microsoft.JavaScript.NodeApi.Generator" Version="0.4.*-*" />
    </ItemGroup>
    ```

3. Add one or more public types to the project with the `[JSExport]` attribute. Only types
   tagged with this attribute exported to JavaScript. (It's also possible to export module-level
   properties and methods by using `[JSExport]` on `public static` properties or methods of a class
   that is otherwise not exported.)
    ```C#
    using Microsoft.JavaScript.NodeApi;

    [JSExport]
    public class Example
    {
   ```

4. Build the project, to produce the assembly (`.dll`) file.
    ```
    dotnet build
    ```
    The build also automaticaly produces a `.d.ts` file with type definitions for APIs in the
    module that are exported to JavaScript.

    > :sparkles: If you're curious, you can check out the generated marshalling code for exported APIs at<br>
    `obj\{Configuration}\{TargetFramerwork}\{RuntimeIdentifier}\generated\
    Microsoft.JavaScript.NodeApi.Generator\Microsoft.JavaScript.NodeApi.Generator.ModuleGenerator`

5. Switching over to the JavaScript project, add a dependency on the `node-api-dotnet` npm package:
    ```
    npm install node-api-dotnet
    ```
    > :warning: Until this package is published, you'll need to
    [build it from source](../README-DEV.md).<br>Then get the package from
    `out/pkg/node-api-dotnet-{version}.tgz`.

6. Import the `node-api-dotnet` package in your JavaScript or TypeScript code. The import syntax
   depends on the [module system](https://nodejs.org/api/esm.html) the current project is using.

   ES modules (TypeScript or JavaScript):
    ```JavaScript
    import dotnet from 'node-api-dotnet';
    ```
   CommonJS modules (TypeScript):
    ```TypeScript
    import * as dotnet from 'node-api-dotnet';
    ```
   CommonJS modules (JavaScript):
    ```JavaScript
    const dotnet = require('node-api-dotnet');
    ```

   To load a specific version of .NET, append the target framework moniker to the module name.
   A `.js` suffix is required when using ES modules, optional with CommonJS.
   ```JavaScript
   import dotnet from 'node-api-dotnet/net6.0.js'
   ```
   Currently the supported target frameworks are `net472`, `net6.0`, and `net8.0`.

7. Load your .NET module assembly from its path using the `dotnet.require()` function. Also provide
    a hint about type definitions (from the same path):
    ```JavaScript
    /** @type {import('./bin/ExampleModule')} */
    const ExampleModule = dotnet.require('./bin/ExampleModule');
    ```
8. Exported APIs in the assembly are projected as properties on the loaded module object. So then
   you can use those to call static methods, construct instances of classes, etc:
    ```JavaScript
    ExampleModule.StaticClass.ExampleMethod();
    const exampleObj = new ExampleModule.ExampleClass(...args);
    ```
    Of course you can access properites, pass arguments to methods, get return values, and so on.
    Most types get automatically marshalled between JavaScript and .NET as you'd expect. For
    details, see the [type projections reference](./typescript.md).

    > :warning: Generic types and methods are not yet supported very well -- with the exception of
    generic collections which work great.

9. **Optional**: Switch the node module to use .NET Native AOT compilation.

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

    To configure a .NET Node API module project for AOT, make sure the target framework is .NET 8 or
    later, and add the publishing properties to the project file:
    ```xml
    <TargetFramework>net8.0</TargetFramework>
    <PublishAot>true</PublishAot>
    <PublishNodeModule>true</PublishNodeModule>
    ```
    Then publish the project to produce the native module (with `.node` extension):
    ```
    dotnet publish
    ```

    A native module does not depend on the `node-api-dotnet` package, so it can be removed from the
    JavaScript project's `package.json`. Then update the JavaScript code to `require()` the .NET
    AOT module directly. Be sure to reference the published `.node` file location, which might be
    different from the built `.dll` location.
    ```JavaScript
    /** @type {import('./bin/ExampleModule')} */
    const ExampleModule = require('./bin/ExampleModule');
    ```
    Or if using ES modules:
    ```JavaScript
    /** @type {import('./bin/ExampleModule')} */
    import ExampleModule from './bin/ExampleModule';
    ```
