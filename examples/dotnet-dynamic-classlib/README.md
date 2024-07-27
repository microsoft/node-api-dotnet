
## Example of dynamically invoking .NET APIs from a referenced project
The `example.js` script loads .NET, loads the `ClassLib` assembly, and calls `new Class1().Hello()`.

| Command                          | Explanation
|----------------------------------|--------------------------------------------------
| `dotnet pack ../..`              | Build Node API .NET packages.
| `dotnet build`                   | Build the `ClassLib` project and generate type definitions.
| `npm install`                    | Install `node-api-dotnet` npm package into the example project.
| `node example.js`                | Run example JS code that dynamically invokes the class library API.
