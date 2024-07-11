# Packages & Releases

## NuGet packages

[`Microsoft.JavaScript.NodeApi`](https://www.nuget.org/packages/Microsoft.JavaScript.NodeApi/) -
Contains the core functionality for interop between .NET and JavaScript, including runtime
code-generation for dynamic marshalling. See the [.NET API reference](../reference/dotnet/).

[`Microsoft.JavaScript.NodeApi.Generator`](https://www.nuget.org/packages/Microsoft.JavaScript.NodeApi.Generator/) -
Contains the MSBuild tasks and supporting assemblies for generating marshalling code at compile
time, and for generating TypeScript type defintions for .NET APIs.

## NPM packages

[`node-api-dotnet`](https://www.npmjs.com/package/node-api-dotnet) - Supports loading .NET
assemblies into a Node.js process. Contains the native .NET hosting modules
(`Microsoft.JavaScript.NodeApi.node`, built with .NET Native AOT) for all supported platforms,
the runtime assemblies (`Microsoft.JavaScript.NodeApi.dll`) for all supported target frameworks,
and loader scripts. See the [JavaScript API reference](../reference/js/).

[`node-api-dotnet-generator`](https://www.npmjs.com/package/node-api-dotnet-generator) - A Node.js
command-line tool that is a wrapper around the .NET generator assembly. It enables generating
TypeScript type definitions without using MSBuild. See the CLI reference at
[TypeScript Type Definitions](../features/type-definitions).

## Releases

Packages are published to nuget and npm automatically, usually around an hour after any PR
is merged.

While the project is in pre-release phase, there may be occasional breaking API changes between
versions less than 1.0. Starting with v1.0 (expected late 2024), it will follow
[semantic versioning practices](https://semver.org/).
