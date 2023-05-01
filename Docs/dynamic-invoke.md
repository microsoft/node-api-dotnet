## Dynamically invoke .NET APIs from JavaScript

For examples of this scenario, see
[../examples/dynamic-invoke/](../examples/dynamic-invoke/) or
[../examples/semantic-kernel/](../examples//semantic-kernel/).

1. Add a dependency on the `node-api-dotnet` npm package to your JavaScript project:
    ```
    npm install node-api-dotnet
    ```

2. Import the `node-api-dotnet` package in your JavaScript code:
    ```JavaScript
    const dotnet = require('node-api-dotnet');
    ```
    Or if using ES modules:
    ```JavaScript
    import dotnet from 'node-api-dotnet';
    ```

3. Load a .NET assembly from its path:
    ```JavaScript
    const ExampleAssembly = dotnet.load('path/to/ExampleAssembly.dll');
    ```
    If the assembly depends on other non-framework assemblies, set up a `resolving` event handler
    _before_ loading the target assembly:
    ```JavaScript
    dotnet.addListener('resolving', (name, version) => {
        const filePath = path.join(__dirname, 'bin', name + '.dll');
        if (fs.existsSync(filePath)) dotnet.load(filePath);
    });
    ```

4. Types in the assembly are projected as properties on the loaded assembly object. So then you can
   use those to call static methods, construct instances of classes, etc:
    ```JavaScript
    ExampleAssembly.StaticClass.ExampleMethod();
    const exampleObj = new ExampleAssembly.ExampleClass(...args);
    ```
    Of course you can access properites, pass arguments to methods, get return values, and so on.
    Most types get automatically marshalled between JavaScript and .NET as you'd expect. For
    details, see the [type projections reference](./typescript.md).

    > :warning: Generic types and methods are not yet supported very well -- with the exception of
    generic collections which work great.

5. **Optional**: Use the `node-api-dotnet-generator` tool to generate type definitions for the assembly:
    ```
    npm exec node-api-dotnet-generator -- -typedefs ExampleAssembly.d.ts --assembly path/to/ExampleAssembly.dll --reference path/to/DependencyAssembly.dll
    ```
    > :warning: Any dependencies need to be explicitly referenced with the `--reference` option.

    > :warning: You may see some warnings about types like `Span<T>` that are not (yet) supported
    for projecting to JavaScript. The warnings can be ignored if you don't plan on using those
    specific APIs.

    After generating the type definitions file, import it as a TypeScript type annotation comment:
    ```JavaScript
    /** @type import('./ExampleAssembly') */
    const ExampleAssembly = dotnet.load('path/to/ExampleAssembly.dll');
    ```
    Then you'll notice your IDE offers documentation-comments and member completion from the type
    definitions, and the TypeScript compiler will use the type definitions.

6. **Optional**: Wrap up the loading code in a convenient JavaScript module that exports the
   assembly with type definitions. For an example of this, see
   [../examples/semantic-kernel/semantic-kernel.js](../examples/semantic-kernel/semantic-kernel.js)
