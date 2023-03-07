
## Minimal Example .NET AOT Node Module
The `Example.cs` class defines a Node.js add-on module that is AOT-compiled, so that it does not
depend on the .NET runtime. The `example.js` script loads that _native_ module as a Node.js add-on
and calls a method on it. The script has access to type definitions and doc-comments for the
module's APIs via the auto-generated `.d.ts` file.

| Command             | Explanation
|---------------------|--------------------------------------------------
| `dotnet pack ../..` | Build NodeApi .NET & npm packages.
| `dotnet publish`    | Install NodeApi .NET packages into example project; build example project and compile to native binary.
| `node example.js`   | Run example JS code that calls the example module.
