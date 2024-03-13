# .NET Native AOT

This project supports loading .NET libraries into a JavaScript application process,
or loading JavaScript libraries into a .NET application process. In either case the .NET
code can be
[ahead-of-time (AOT) compiled](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/),
which makes it executable _without depending on the .NET Runtime_.

There are advantages and disadvantages to either approach:
|                     | .NET Runtime | .NET Native AOT |
|---------------------|--------------|-----------------|
| API compatibility   | Broad compatibility with .NET APIs | Limited compatibility with APIs designed to support AOT |
| Ease of deployment  | Requires a matching version of .NET to be installed on the target system | A .NET installation is not required (though some platform libs may be required on Linux)
| Size of deployment  | Compact - only IL assemblies need to be deployed | Larger due to bundling necessary runtime code - minimum ~3 MB per platform |
| Performance         | Slightly slower startup (JIT) | Slightly faster startup (no JIT) |
| Runtime limitations | Full .NET functionality | Some .NET features like reflection and code-generation aren't supported |

To use C# to create a Node.js addon using Native AOT, see
[.NET Native AOT for Node.js](../scenarios/js-aot-module).

There is no documentation or example code yet specific to hosting JavaScript in a .NET Native AOT
application, but it is not very different from non-AOT
[Embedding JS in .NET](../scenarios/dotnet-js).

## AOT limitations

Some features in this project are not available in a Native AOT environment because they depend
on runtime reflection or code-generation:
 - [Dynamically loading and invoking .NET APIs](../scenarios/js-dotnet-dynamic) - Only APIs tagged
   with `[JSExport]` and
   [compiled with the source-generator](./js-dotnet-marshalling#compile-time-code-generation) can
   be called in an AOT module.
 - [.NET namespaces](../reference/namespaces) - Namespaces are used only with dynamic invocation.
   APIs exported by an AOT module do not use JS namespaces.
 - [Constructing generic classes or invoking generic methods](../reference/generics) - AOT modules
   can only export non-generic types and methods.
   ([Generic collections](../reference/arrays-collections) are supported though.)
 - [Calling .NET extension methods using extension syntax](../reference/extension-methods).
   Extension methods can still be called using static-method syntax, but AOT modules should design
   exported APIs to not require extension methods.
 - Implementing a .NET interface with a JavaScript class - this requires code-generation to [emit
   a .NET class that implements the interface as a proxy to the JS object](
   https://github.com/microsoft/node-api-dotnet/blob/main/src/NodeApi.DotNetHost/JSInterfaceMarshaller.cs).
   AOT modules should not export APIs that expect an interface to be implemented by JS.
