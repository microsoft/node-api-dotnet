## Build Node.js addons in .NET

**Status: Experimental**

This library uses a combination of [C# Source Generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) and [.NET Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) to produce Node.js native addons written in C# that _do not depend on the .NET runtime_.

### Notes
 - **.NET 7 or later is required** at build time because this project relies on recent improvements to .NET native AOT capabilities.

 - Tested on Windows and Ubuntu so far. It should work on any platform supported by .NET Native AOT.

 - The built C# `.node` native library is a single file that is somewhat large due to static-linking the necessary .NET AOT libraries. The minimal example is about 3 MB on Windows and 13 MB on Linux (x64 release builds).

### Build
```
dotnet publish -r win-x64
```
Select an appropriate [.NET runtime identifier](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog) for your platform, or even a different platform for cross-compilation.

### Run
```
node .\Example\Example.js
```
The simple example script uses `require()` to load the C# native addon module that was just built, and calls a method on it.

### How it works
1. A C# class defines some instance properties and methods to be exposed to Node.js as a module. [See the example class.](./Example/Example.cs)
2. The module class is tagged with a `[NodeApi.Module]` attribute. (In the future, additional C# classes in the same library may be exported indirectly via properties on the module.)
3. At build time, the source generator finds the class with that attribute and then emits code to register the module as a Node-API addon and export the module class's properties and methods.
4. The registration method has a special [`[UnmanagedCallersOnly]` attribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedcallersonlyattribute) that causes it to be exported from the native library as an ordinary native entrypoint.
5. When Node.js loads the module, it calls the registration method like any other native entrypoint.
6. Helper classes in the .NET `NodeApi` library in this project facilitate interop and marshalling via a Node runtime .NET object model and low-level [`napi_` functions](https://nodejs.org/api/n-api.html) that are called via P/Invoke.

The intention for the `NodeApi` helper classes here is to follow a similar pattern to the [`node-addon-api` C++ classes](https://github.com/nodejs/node-addon-api/), with some minor differences allowing for .NET conventions.
