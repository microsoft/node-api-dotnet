
## Invoking .NET APIs from an Electron app

| Command                          | Explanation
|----------------------------------|--------------------------------------------------
| `dotnet pack ../..`              | Build Node API .NET packages.
| `npm install`                    | Install `node-api-dotnet` and `electron` npm packages into the example project.
| `npm run start`                  | Run the example Electron app.

The example is from the [Electron "Quick Start" app](
    https://www.electronjs.org/docs/latest/tutorial/quick-start),
with some small modifications to additionally load .NET and display the .NET version obtained from
`System.Environment.Version`.

The `node-api-dotnet` package is loaded in the _main_ Electron process immediately after creating
the window. Then the .NET version value is sent via IPC to the _renderer_ process where it is
displayed on the HTML page.

---

This is not intended to be an attempt to replace or compete with the great work at
[Electron.NET](https://github.com/ElectronNET/Electron.NET). That project runs a full ASP.NET +
Blazor stack inside an Electron app, enabling use of Blazor components to build a cross-platform
client application UI.

However if you're building a more traditional Electron JS app and you just need to call a few
.NET APIs from JS code, then the example here offers a more lightweight approach.
