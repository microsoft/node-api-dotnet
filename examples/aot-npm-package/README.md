
## Minimal Example .NET AOT NPM Package
The `lib/Example.cs` class defines a Node.js add-on module that is AOT-compiled, so that it does not
depend on the .NET runtime. The AOT module is then packaged as an npm package. The `app/example.js`
script loads that _native_ module via its npm package and calls a method on it. The script has
access to type definitions and doc-comments for the module's APIs via the auto-generated `.d.ts`
file that was included in the npm package.

| Command                       | Explanation
|-------------------------------|--------------------------------------------------
| `dotnet pack ../..`           | Build Node API .NET packages.
| `cd lib`<br/>`dotnet publish` | Install Node API .NET packages into lib project; build lib project and compile to native binary; pack npm package.
| `cd app`<br/> `npm install`   | Install lib project npm package into app project.
| `node example.js`             | Run example JS code that calls the library API.

### Building multi-platform npm packages with platform-specific AOT binaries
Native AOT binaries are platform-specific. The `dotnet publish` command above creates a package
only for the current OS / CPU platform (aka .NET runtime-identifier). To create a multi-platform
npm package with Native AOT binaries, run `dotnet publish` separately for each runtime-identifier,
and only create the package on the last one:
```
dotnet publish -r:win-x64 -p:PackNpmPackage=false
dotnet publish -r:win-arm64 -p:PackNpmPackage=true
```

To create a fully cross-platform packatge, it will be necessary to compile on each targeted OS
(Windows, Mac, Linux), then copy the outputs into a shared directory before creating the final
npm package.
