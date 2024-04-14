# Develop a Node.js addon module in C#

For a minimal example of this scenario, see
[/examples/dotnet-module/](https://github.com/microsoft/node-api-dotnet/blob/main/examples/dotnet-module/).

1. Create a .NET Class library project that targets .NET 6 or later. (.NET 8 for AOT.)
    ```shell
    mkdir ExampleModule
    cd ExampleModule
    dotnet new classlib --framework net6.0
    ```

2. Add a reference to the `Microsoft.JavaScript.NodeApi` and
   `Microsoft.JavaScript.NodeApi.Generator` packages:
    ```shell
    dotnet add package --prerelease Microsoft.JavaScript.NodeApi
    dotnet add package --prerelease Microsoft.JavaScript.NodeApi.Generator
    ```

    Afterward you should have the two references in your project file:
    ```xml
    <ItemGroup>
      <PackageReference Include="Microsoft.JavaScript.NodeApi" Version="0.7.*-*" />
      <PackageReference Include="Microsoft.JavaScript.NodeApi.Generator" Version="0.7.*-*" />
    </ItemGroup>
    ```

3. Add one or more public types to the project with the `[JSExport]` attribute. Types tagged
   with this attribute, along with any types referenced in public properties or methods of tagged
   types, are exported to JavaScript. It is also possible to export _module-level_
   properties and methods by using `[JSExport]` on `public static` properties or methods of a class
   that is otherwise not exported.
    ```C#
    using Microsoft.JavaScript.NodeApi;

    [JSExport]
    public class Example
    {
   ```

4. Build the project, to produce the assembly (`.dll`) file.
    ```shell
    dotnet build
    ```
    The build also automaticaly produces a `.d.ts` file with type definitions for APIs in the
    module that are exported to JavaScript.

    ::: tip :sparkles: TIP
    If you're curious, you can check out the generated marshalling code for exported APIs at<br>
    `obj/{Configuration}/{TargetFramerwork}/{RuntimeIdentifier}/generated/
    Microsoft.JavaScript.NodeApi.Generator/Microsoft.JavaScript.NodeApi.Generator.ModuleGenerator/`.
    Read [how it works](/features/how-it-works) to learn more about what's going on in there.
    :::

5. Switching over to the JavaScript project, add a dependency on the `node-api-dotnet` npm package:
    ```shell
    npm install node-api-dotnet
    ```

6. Import the `node-api-dotnet` package in your JavaScript or TypeScript code. The import syntax
   depends on the [module system](https://nodejs.org/api/esm.html) the current project is using.
    ::: code-group
    ```JavaScript [ES (TS or JS)]
    import dotnet from 'node-api-dotnet';
    ```
    ```TypeScript [CommonJS (TS)]
    import * as dotnet from 'node-api-dotnet';
    ```
    ```JavaScript [CommonJS (JS)]
    const dotnet = require('node-api-dotnet');
    ```
    :::

   To load a specific version of .NET, append the target framework moniker to the module name.
   A `.js` suffix is required when using ES modules, optional with CommonJS.
    ::: code-group
    ```JavaScript [ES (TS or JS)]
    import dotnet from 'node-api-dotnet/net6.0.js';
    ```
    ```TypeScript [CommonJS (TS)]
    import * as dotnet from 'node-api-dotnet/net6.0';
    ```
    ```JavaScript [CommonJS (JS)]
    const dotnet = require('node-api-dotnet/net6.0');
    ```
    :::
   Currently the supported target frameworks are `net472`, `net6.0`, and `net8.0`.

7. Load your .NET module assembly from its path using the `dotnet.require()` function. Optionally
   provide a hint about type definitions (from the same path):
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
    details, see the [type projections reference](../marshalling/).
