## C# JSDom Example
This project is a C# executable application that uses the JSDOM library
(https://github.com/jsdom/jsdom) to parse HTML.

Before building and running this project, download or build `libnode.dll` that
_includes Node API embedding support_.
See https://microsoft.github.io/node-api-dotnet/scenarios/dotnet-js.html

| Command                 | Explanation
|-------------------------|--------------------------------------------------
| `dotnet pack ../..`     | Build Node API .NET packages.
| `npm install`           | Install JavaScript packages.
| `dotnet build`          | Install .NET nuget packages; build example project.
| `dotnet run --no-build` | Run the example project.

