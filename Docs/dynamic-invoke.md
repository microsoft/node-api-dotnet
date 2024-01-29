## Dynamically invoke .NET APIs from JavaScript

For examples of this scenario, see
[../examples/dynamic-invoke/](../examples/dynamic-invoke/) or
[../examples/semantic-kernel/](../examples//semantic-kernel/).

1. (Optional but recommended) Create a `.csproj` project (without any `.cs` source files) that will
   manage restoring nuget packages for .NET assemblies used by JS:
   ```xml
   <Project Sdk="Microsoft.NET.Sdk">
     <PropertyGroup>
       <TargetFramework>net6.0</TargetFramework>
       <OutDir>bin</OutDir>
       <NodeApiAssemblyJSModuleType>commonjs</NodeApiAssemblyJSModuleType>
       <GenerateNodeApiTypeDefinitionsForReferences>true</GenerateNodeApiTypeDefinitionsForReferences>
     </PropertyGroup>
     <ItemGroup>
       <PackageReference Include="Microsoft.JavaScript.NodeApi.Generator" Version="0.4.*" />
       <PackageReference Include="Example.Package" Version="1.2.3" />
       <PackageReference Include="Example.Package.Two" Version="2.3.4" />
     </ItemGroup>
   </Project>
   ```
   - The `TargetFramework` should match the version of .NET that the JS application will load.
   - For convenience the `OutDir` can be simply set to `bin` because there are no object files
     or debug/release builds involved. The referenced assemblies (and their dependencies)
     will be placed there.
   - The `Microsoft.JavaScript.NodeApi.Generator` package reference enables automatic generation
     of TS type-definitions for the referenced assemblies.
   - Change `NodeApiAssemblyJSModuleType` to `esm` if using ES modules.

   Build the project to restore the packages, place assemblies in the `bin` directory, and generate
   type definitions:
   ```
   dotnet build
   ```

2. Add a dependency on the `node-api-dotnet` npm package to your JavaScript project:
    ```
    npm install node-api-dotnet
    ```

3. Import the `node-api-dotnet` package in your JavaScript or TypeScript code. The import syntax
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

4. Load one or more .NET packages using the generated `.js` files:
   ```JavaScript
   require('./bin/Example.Package.js');
   require('./bin/Example.Package.Two.js');
   ```
   Or if using ES modules:
   ```JavaScript
   import './bin/Example.Package.js';
   import './bin/Example.Package.Two.js';
   ```
   :warning: Do not assign the results of these `require`/`import` statements. The assemblies are
   all loaded into the `dotnet` object  (explained in the next step).

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
   StaticClass.ExampleMethod();
   const exampleObj = new ExampleClass(...args);
   ```
   Of course you can access properites, pass arguments to methods, get return values, and so on.
   Most types get automatically marshalled between JavaScript and .NET as you'd expect. For
   details, see the [type projections reference](./typescript.md).

   You should notice your IDE offers documentation-comments and member completion from the type
   definitions, and if writing TypeScript code the TypeScript compiler will check against the
   type definitions.

   .NET system assemblies can also be loaded and used. For example, import `System.Runtime.js` to
   get the core types, `System.Console.js` to get console APIs, etc. Type definitions for those
   two assembiles are generated by default; to generate typedefs for additional system assemblies,
   add items to the `NodeApiSystemReferenceAssembly` MSBuild item-list in the project.

   > :warning: Generic types and methods are not yet supported very well -- with the exception of
   generic collections which work great.
