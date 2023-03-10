
## Minimal Example .NET Node Module
The `Example.cs` class defines a Node.js add-on module that runs on .NET. The `example.js` script
loads that .NET module and calls a method on it. The script has access to type definitions and
doc-comments for the module's APIs via the auto-generated `.d.ts` file.

| Command                          | Explanation
|----------------------------------|--------------------------------------------------
| `dotnet publish -f net7.0 ../..` | Build NodeApi .NET host.
| `dotnet pack ../..`              | Build NodeApi .NET & npm packages.
| `npm install`                    | Install NodeApi npm package into example project.
| `dotnet build`                   | Install NodeApi .NET packages into example project; build example project.
| `node example.js`                | Run example JS code that calls the example module.
