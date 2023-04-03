# node-api-dotnet Development Notes

### Requirements for Development
 - [.NET 7 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
   - _and_ [.NET 6 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
   - _and_ [.NET 4.7.2 developer pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net472)
     (Windows only)
 - [Node.js](https://nodejs.org/) version 16 or later

While `node-api-dotnet` supports .NET 6 or .NET Framework 4 at runtime, .NET 7 or later SDK is
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
dotnet pack -c Release
```
This produces both nuget and npm packages (for the current platform only) in the `out/pkg`
directory.

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
dotnet test --framework net7.0 --filter "DisplayName~aot"
```

The list of test cases is automatically derived from the set of `.js` files under the
`Test/TestCases` directory. Within each subdirectory there, all `.cs` files are compiled into one
assembly, then all `.js` test files execute against the assembly.

Most test cases run twice, once for "hosted" CLR mode and once for AOT ahead-of-time compiled mode
with no CLR.

## Debugging
With a debug build, the following environment variables trigger just-in-time debugging of the
respective components:
 - `DEBUG_NODE_API_GENERATOR` - Debug the C# source-generator when it runs during the build.
 - `DEBUG_NODE_API_RUNTIME` - Debug the .NET runtime host when it is loaded by JavaScript. (Does
 not apply to AOT-compiled modules.)

Also `TRACE_NODE_API_HOST` causes tracing information to be printed about the the process of
loading the .NET host.

## Check/fix formatting
PR builds will fail if formatting does not comply with settings in `.editorconfig`.
```
dotnet format --severity info --verbosity detailed
```

## Roadmap
[node-api-dotnet tasks](https://github.com/users/jasongin/projects/1/views/1)
