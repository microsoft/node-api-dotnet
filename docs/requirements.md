# Requirements

## Runtime requirements
#### OS
 - Windows: x64, arm64
 - Mac: x64, arm64
 - Linux: x64, arm64

#### .NET
 - For .NET runtime-dependent applications, .NET 4.7.2 or later, .NET 6, or .NET 8 runtime
   must be installed.
 - For .NET Native AOT applications, .NET is not required on the target system.
    - On Linux, AOT binaries may depend on optional system packages. See
    [Install .NET on Linux](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
    and browse to the distro specific dependencies.

#### Node.js
 - Node.js v18 or later
    - Other JS runtimes may be supported in the future.

### Embedding (LibNode)

Embedding Node.js in a .NET application uses LibNode rather than an installed Node.js executable.
LibNode 26.x, built from Node.js 26, supports:

 - Windows x64/arm64, with the Universal C Runtime (UCRT).
 - Linux x64/arm64 with glibc >= 2.35. Alpine Linux and other musl-based distributions are not supported.
 - macOS x64/arm64, version 13.5 or later.

There is no x86 build. The host must be a 64-bit process, including .NET Framework applications
and IIS application pools; 32-bit processes and 32-bit IIS app pools are not supported.
See [Upgrading from LibNode 20.x](./scenarios/dotnet-js#upgrading-from-libnode-20-x) for bitness settings.

## Build requirements

 - .NET 8 SDK
    - Optional: .NET 6 SDK, if targeting .NET 6 runtime
    - Optional: .NET Framework 4.x developer pack, if targeting .NET Framework
 - Node.js v18 or later
