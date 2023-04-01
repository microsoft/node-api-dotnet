
## Minimal Example .NET Node Module
The `Example.cs` class defines a Node.js add-on module that runs on .NET. The `example.js` script
loads that .NET module and calls a method on it. The script has access to type definitions and
doc-comments for the module's APIs via the auto-generated `.d.ts` file.

| Command                          | Explanation
|----------------------------------|--------------------------------------------------
| `dotnet pack ../..`              | Build Node API .NET packages.
| `npm install`                    | Install Node API .NET npm package into example project.
| `dotnet build`                   | Install Node API .NET nuget packages into example project; build example project.
| `node example.js`                | Run example JS code that calls the example module.

### .NET Framework
To use .NET Framework, apply the follwing change to `example.js`:
```diff
-const dotnet = require('node-api-dotnet');
+const dotnet = require('node-api-dotnet/net472');
```
