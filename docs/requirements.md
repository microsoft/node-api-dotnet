# Requirements

## Runtime requirements
#### OS
 - Windows: x64, arm64
 - Mac: x64, arm64
 - Linux: x64 ([arm64 coming soon](https://github.com/microsoft/node-api-dotnet/issues/80))

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

## Build requirements

 - .NET 8 SDK
    - Optional: .NET 6 SDK, if targeting .NET 6 runtime
    - Optional: .NET Framework 4.x developer pack, if targeting .NET Framework
 - Node.js v18 or later
