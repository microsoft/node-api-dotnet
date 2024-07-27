# Dynamically invoke .NET APIs from JavaScript

For examples of this scenario, see one of these directories in the repo:
 - [/examples/dotnet-dynamic/](https://github.com/microsoft/node-api-dotnet/blob/main/examples/dotnet-dynamic/)
 - [/examples/dotnet-dynamic-classlib/](https://github.com/microsoft/node-api-dotnet/blob/main/examples/dotnet-dynamic-classlib/)
 - [/examples/semantic-kernel/](https://github.com/microsoft/node-api-dotnet/blob/main/examples/semantic-kernel/)
---

1. Create a `.csproj` project (without any `.cs` source files) that will manage restoring nuget
   packages for .NET assemblies used by JS:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net6.0</TargetFramework>
       <OutDir>bin</OutDir>
       <NodeApiAssemblyJSModuleType>commonjs</NodeApiAssemblyJSModuleType>// [!code highlight]
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.JavaScript.NodeApi.Generator" Version="0.5.*" /> // [!code highlight]
       <PackageReference Include="Example.Package" Version="1.2.3" />
       <ProjectReference Include="../ClassLib/ClassLib.csproj" />
     </ItemGroup>
   </Project>
   ```
   - The `TargetFramework` should match the version of .NET that the JS application will load.
   - Add `<PackageReference>` elements for any .NET packages to be used from JavaScript.
   - Add `<ProjectReference>` elements for any locally-built .NET projects to be used from
     JavaScript.
   - Add a reference to the `Microsoft.JavaScript.NodeApi.Generator` package to get automatic
     generation of TS type-definitions for the referenced system assemblies, nuget packages, and
     projects.
   - Change `NodeApiAssemblyJSModuleType` to `esm` if using ES modules.
   - The `OutDir` is set to just `bin` to make it easier for JavaScript code to locate the
     .NET assemblies. Otherwise the JS code will need to use a different path for debug or
     release builds.

   ::: tip :sparkles: TIP
   Ensure referenced projects have `<GenerateDocumentationFile>true</GenerateDocumentationFile>`
   to enable doc-comments in the generated type definitions.
   :::

   Build the project to restore the packages, place assemblies in the `bin` directory, and generate
   type definitions:
   ```shell
   dotnet build
   ```

2. Initialize a JavaScript project if you don't have one already. (It can be in the same directory
   as the C# project, or a separate directory.) Then add a dependency on the
   `node-api-dotnet` npm package:
    ```shell
    npm init
    npm install node-api-dotnet
    ```

3. Import the `node-api-dotnet` package in your JavaScript or TypeScript code. The import syntax
   depends on the [module system](https://nodejs.org/api/esm.html) the current project is using:
   CommonJS or ES.
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

4. Load one or more .NET packages using the generated `.js` files:
    ::: code-group
    ```JavaScript [ES (TS or JS)]
    import './bin/Example.Package.js';
    import './bin/Example.Package.Two.js';
    ```
    ```JavaScript [CommonJS (TS or JS)]
    require('./bin/Example.Package.js');
    require('./bin/Example.Package.Two.js');
    ```
    :::
   ::: warning :warning: WARNING
   Do not assign the results of these `require`/`import` statements. The assemblies are
   all loaded into the `dotnet` object  (explained in the next step).
   :::

   If any of the loaded assemblies depends on other assemblies outside the core framework, they
   will be automatically loaded from the same directory. Building the `.csproj` should take care
   of bin-placing all dependencies together. If some dependencies are are in another location,
   set up a `resolving` event handler _before_ loading the target assembly:
   ```JavaScript
   dotnet.addListener('resolving', (name, version) => {
       const filePath = path.join(__dirname, 'bin', name + '.dll');
       if (fs.existsSync(filePath)) dotnet.load(filePath);
   });
   ```

5. Namespaces and types from the loaded assemblies are projected onto the top-level `dotnet` object.
   When loading multiple .NET assemblies, types from all assemblies are merged into the same
   namespace hierarchy.

   It is convenient (and more efficient!) to create aliases for
   namespace-qualified .NET types, to avoid repeating the namespace every time.
   ```JavaScript
   const ExampleStaticClass = dotnet.ExampleNamespace.ExampleStaticClass;
   const ExampleClass = dotnet.ExampleNamespace.ExampleClass;
   ExampleStaticClass.ExampleMethod();
   const exampleObj = new ExampleClass(...args);
   ```
   Of course you can access properites, pass arguments to methods, get return values, and so on.
   Most types get automatically marshalled between JavaScript and .NET as you'd expect. For
   details, see the [JavaScript / .NET type mappings reference](../reference/js-dotnet-types).

   You should notice your IDE offers documentation-comments and member completion from the type
   definitions, and if writing TypeScript code the TypeScript compiler will check against the
   type definitions.

   .NET system assemblies can also be loaded and used. For example, import `System.Runtime.js` to
   get the core types, `System.Console.js` to get console APIs, etc. Type definitions for those
   two assembiles are generated by default; to generate typedefs for additional system assemblies,
   add items to the `NodeApiSystemReferenceAssembly` MSBuild item-list in the project.
