# TypeScript Type Definitions

Type definitions enable JavaScript or TypeScript code to benefit from compile-time type checking,
editor suggestions, and documentation tips while working with .NET APIs. The type definitions can be
automatically generated for any .NET project or pre-existing NuGet packages, using either MSBuild
or a stand-alone command-line tool.

Many transformations are applied to .NET APIs when they are projected to JavaScript, in order to
accomodate differences in the type systems, runtime libraries, and common conventions between .NET
and JavaScript. The generated type definitions are the compile-time declarations that correspond
to the behavior of the JavaScript marshaller, which is the runtime component responsible for
actually converting method calls, parameters, return types, etc. between the two runtime
environments. For details, see [Marshalling between .NET and JavaScript](./js-dotnet-marshalling)
and the [JS / .NET type mappings reference](../reference/js-dotnet-types).

## Generating type definitions with MSBuild

The easiest way to generate type definitions is to leverage the provided MSBuild targets. The
`Microsoft.JavaScript.NodeApi.Generator.targets` file is automatically imported when referencing
the `Microsoft.JavaScript.NodeApi.Generator` NuGet package:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.JavaScript.NodeApi.Generator" Version="0.9.*-*" />
</ItemGroup>
```

By default, the imported targets will generate type definitions only for the _current_ project.
But if the current project is empty (contains no `Compile` items), then type definitions are
generated for all assemblies referenced by the project, including both NuGet package assemblies
and system assemblies. This use of an empty project enables leveraging MSBuild to restore packages
and generate type definitions, for the purpose of dynamically invoking those packages from
JavaScript without writing a C# module. See
[Dynamically invoke .NET APIs from JavaScript](../scenarios/js-dotnet-dynamic).

To customize some aspects of generating type definitions via MSBuild, see the
[MSBuild properties](../reference/msbuild-props) reference.

## Generating type definitions with the command-line tool

The `node-api-dotnet-generator` npm package is a standalone command-line tool that wraps the
`Microsoft.JavaScript.NodeApi.Generator` assembly and enables using it outside of MSBuild.

| Parameter     | Alias | Description |
|---------------|-------|-------------|
| `--asssembly` | `-a`  | Required path to the assembly file for which type definitions are to be generated. 
| `--framework` | `-f`  | Target framework moniker of system assembly dependencies, e.g. `net8.0`. Defaults to the .NET runtime version used when invoking the tool (currently .NET 8).
| `--pack`      | `-p`  | Application targeting pack(s) to check when resolving system assembly dependencies. Defaults to `Microsoft.NETCore.App`. May be specified more than once. Add the `Microsoft.AspNetCore.App` targeting pack for an ASP.NET app, or `Microsoft.WindowsDesktop.App` for a Windows desktop app.
| `--reference` | `-r`  | Path to an assembly that is referenced by the primary assembly. System assemblies are located automatically via the targeting packs and do not need to be specified separately. All other referenced assemblies must be provided. May be specified more than once.
| `--typedefs`  | `-t`  | Required path to output generated type definitions (`.d.ts`) file.
| `--module`    | `-m`  | Generate a JS module-loader script alongside the typedefs. May be specified more than once. Each value is either `commonjs` or `esm`, or a path to a `package.json` file with a `"type"` property specifying the module type.
| `--nowarn`    |       | Do not display warnings about APIs that cannot be projected to JavaScript.
| `--help`  | `-h` `-?` | Show command-line help.
| @&lt;file&gt;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp; || Read the specified response file for more options. Typically used when a long list of reference assembly paths may exceed the maximum command-line length.

Note each invocation generates type definitions only for one specified primary assembly, even when
multiple reference assemblies are also provided. So generating type definitions for all reference
assemblies (and system assemblies) may require many invocations.
