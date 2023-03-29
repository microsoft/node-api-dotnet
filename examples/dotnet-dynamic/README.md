
## Minimal Example of dynamically invoking .NET APIs
The `example.js` script loads .NET  and calls `Console.WriteLine()`.

| Command                          | Explanation
|----------------------------------|--------------------------------------------------
| `dotnet pack ../..`              | Build Node API .NET packages.
| `npm install`                    | Install `node-api-dotnet` npm package into the example project.
| `node example.js`                | Run example JS code that uses that package to dynamically invoke a .NET API.
