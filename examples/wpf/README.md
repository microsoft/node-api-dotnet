
## Example: Calling WFP APIs from JS
The `example.js` script dynamically loads the WPF .NET assemblies and shows a simple message box.

_**.NET events** are not yet projected to JS ([#59](https://github.com/microsoft/node-api-dotnet/issues/59)). WPF capabilities will be limited until that issue is resolved._

| Command                          | Explanation
|----------------------------------|--------------------------------------------------
| `dotnet pack ../..`              | Build Node API .NET packages.
| `dotnet build`                   | Generate type definitions for WPF assemblies.
| `npm install`                    | Install `node-api-dotnet` npm package into the project.
| `node example.js`                | Run example JS code that calls WPF.
