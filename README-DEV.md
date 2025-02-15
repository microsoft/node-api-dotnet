# node-api-dotnet Development Notes

### Requirements for Development
 - [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
   - _and_ [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
   - _and_ [.NET 4.7.2 developer pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472)
     (Windows only)
 - [Node.js](https://nodejs.org/) version 18 or later

While `node-api-dotnet` supports .NET 6 or .NET Framework 4 at runtime, .NET 8 or later SDK is
required for building the AOT components.

## Build
```bash
dotnet build
```

While developing the source generator, set `DOTNET_CLI_USE_MSBUILD_SERVER=0` to prevent MSBuild
from re-using a previously-loaded (possibly outdated) version of the source generator assembly.
(See [Shut down or disable MSBuild Server](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-server?view=vs-2022#shut-down-or-disable-msbuild-server).)

## Build Packages
```bash
dotnet pack
```
This produces both nuget and npm packages (for the current platform only) in the `out/pkg`
directory. It uses `Debug` configuration by default, which is slower but allows for
[debugging](#debugging). Append `-c Relase` to change the configuration.

## Test
```bash
dotnet pack
dotnet test
```

Some tests reference the source-generator via the local nuget package; hence it is required to run
`dotnet pack` before the first time running tests, or after any changes to the generator code.
_This is unavoidable because the source-generator must run with at least .NET 6, so referencing it
via a `<ProjectReference>` would not work for .NET 4 testing because project references always
use the same target framework version._

Use `--framework` to specify a target framework, or `--filter` to run a subset of test cases:
```bash
dotnet test --framework net8.0 --filter "DisplayName~aot"
```

The list of test cases is automatically derived from the set of `.js` files under the
`Test/TestCases` directory. Within each subdirectory there, all `.cs` files are compiled into one
assembly, then all `.js` test files execute against the assembly.

Most test cases run twice, once for "hosted" CLR mode and once for AOT ahead-of-time compiled mode
with no CLR.

### Test a Private Build in another project
A project typically consumes the `Microsoft.JavaScript.NodeApi` packages from `nuget.org`. Use these
steps to set up a project to use a local build of the packages instead:
1. [Build nuget packages](#build-packages) with `dotnet pack`.
2. Note the version of the packages produced. If building from branch other than `main` the
   package version may include a git commit hash.
3. Add the package output directory to `<packageSources>` in your project's `NuGet.config` file,
   before the `nuget.org` package source. It should look like this. (Replace
   `/path/to-/node-api-dotnet` with the correct relative or absolute path on your system.)
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="/path/to/node-api-dotnet/out/pkg" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <disabledPackageSources>
    <clear />
  </disabledPackageSources>
</configuration>
```
4. In your `.csproj` or `Directory.Packages.props` file, update `<PackageReference>` elements to
   reference the version of the packages that you built locally. Include the git commit hash suffix,
   if applicable. For example:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.JavaScript.NodeApi" Version="0.4.31-g424705b2aa" />
    <PackageReference Include="Microsoft.JavaScript.NodeApi.Generator" Version="0.4.31-g424705b2aa" />
  </ItemGroup>
```
5. Stop the .NET build server to ensure it doesn't continue using a previous version of the
   generator assembly:
```bash
dotnet build-server shutdown
```

## Debugging
With a debug build, the following environment variables trigger just-in-time debugging of the
respective components:
 - `NODE_API_DEBUG_GENERATOR=1` - Debug the C# source-generator or TS type-definitions generator
 when they run during the build.
 - `NODE_API_DEBUG_RUNTIME=1` - Debug the .NET runtime host when it is loaded by JavaScript. (Does
 not apply to AOT-compiled modules.)

Setting either of these variables to `1` causes the program to print a message to the console
at startup and wait (with 20s countdown) for a debugger to attach:
```
###################### DEBUG ######################
Process "node" (21864) is waiting for debugger.
Press any key to continue without debugging... (20)
```
Set to the string `vs` to use the VS JIT Debug dialog instead. (Requires Windows and a Visual Studio
installation.)

## Tracing
The following environment variables trigger verbose tracing to the console:
 - `NODE_API_TRACE_HOST=1` - Trace messages about starting the native host and managed host and
 dynanically exporting .NET types from the managed host to JS.
   - If that is not enough, also set `COREHOST_TRACE=1` to trace .NET CLR host initialization.
   Warning: The output is very verbose.
 - `NODE_API_TRACE_RUNTIME=1` - Trace all calls and callbacks across the JS/.NET boundary.
Tracing works with both debug and release builds.

## Check/fix formatting
PR builds will fail if formatting does not comply with settings in `.editorconfig`.
```
dotnet format --severity info --verbosity detailed
```

## Benchmarks
There are a lot of micro-benchmarks to measure low-level .NET/JS interop operations. See
[bench/README.md](./bench/README.md) for details.

## Roadmap
We track major feature work on the project board:
[node-api-dotnet tasks](https://github.com/users/jasongin/projects/1/views/1)
