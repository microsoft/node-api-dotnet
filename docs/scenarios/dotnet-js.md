# Embed a JS runtime in a .NET application

::: warning :warning: WARNING
This functionality is still experimental. It works, but the process is not as streamlined as it
should be.
:::

## Acquiring the required `libnode` binary
Reference [`Microsoft.JavaScript.LibNode`](https://www.nuget.org/packages/Microsoft.JavaScript.LibNode).
It provides `libnode` built from Node.js 26.13.1 with the ABI-stable embedding API, for
`win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and `osx-arm64`.
The embedding API is not yet in upstream Node.js and changes between versions; use the LibNode
version pinned in `Directory.Packages.props`, the only one tested with each release.
To restore a single platform, reference `Microsoft.JavaScript.LibNode.<rid>` instead.
Also reference `Microsoft.JavaScript.NodeApi` for the .NET APIs.
See [Embedding (LibNode) requirements](../requirements#embedding-libnode) for OS requirements.

## Importing JS modules into .NET

1. Load `libnode` and initialize a Node.js "platform" and "runtime" instance:
```C#
// Use default discovery for the native library; baseDir controls module resolution.
string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
NodeEmbeddingPlatform nodejsPlatform = new(new NodeEmbeddingPlatformSettings());
NodeEmbeddingThreadRuntime nodejsRuntime = nodejsPlatform.CreateThreadRuntime(baseDir,
    new NodeEmbeddingRuntimeSettings
    {
        MainScript =
            "globalThis.require = require('module').createRequire(process.execPath);\n"
    });
```

By default, modern .NET uses native-library dependency resolution (including `.deps.json`).
On .NET Framework and when using the .NET Standard assembly, discovery first checks
`runtimes/<rid>/native` under the application base directory. Both paths fall back to the OS
library search paths. To override discovery, set `NodeEmbeddingPlatformSettings.LibNodePath`
to the full path of the native library.

There can only be one
[`NodeEmbeddingPlatform`](../reference/dotnet/Microsoft.JavaScript.NodeApi.Runtime/NodeEmbeddingPlatform)
instance per process, so typically it would be stored in a static variable. From the platform,
multiple
[`NodeEmbeddingRuntime`](../reference/dotnet/Microsoft.JavaScript.NodeApi.Runtime/NodeEmbeddingRuntime)
or
[`NodeEmbeddingThreadRuntime`](../reference/dotnet/Microsoft.JavaScript.NodeApi.Runtime/NodeEmbeddingThreadRuntime)
instances may be created and disposed.

The directory provided to the
[`NodeEmbeddingThreadRuntime`](../reference/dotnet/Microsoft.JavaScript.NodeApi.Runtime/NodeEmbeddingThreadRuntime)
instance is the base for package resolution. Any packages or modules imported in the next step must
be installed (in a `node_modules` directory) in that base directory or a parent directory.

2. Invoke a simple script from C#:
```C#
await nodejsRuntime.RunAsync(() =>
{
    JSValue.RunScript("console.log('Hello from JS!');");
});
```

Be aware of JavaScript's single-threaded execution model. **All JavaScript operations must be
performed from the JS environment thread**, unless otherwise noted. Use any of the `Run()`,
`RunAsync()`, `Post()`, or `PostAsync()` methods on the JS `NodeEmbeddingThreadRuntime` instance
to switch to the JS thread. Also keep in mind any JavaScript values of type `JSValue` (or any of
the more specific JS value struct types) are not valid after switching off the JS thread.
A `JSReference` can hold on to a JS value for future use. See also
[JS Threading and Async Continuations](../features/js-threading-async) and
[JS References](../features/js-references).

3. Import modules or module properties:
```C#
// Import * from the `fluid-framework` module. Items exported from the module will be
// available as properties on the returned JS object.
JSValue fluidPackage = nodejsRuntime.Import("fluid-framework");

// Import just the `SharedString` class from the `fluid-framework` module.
JSValue sharedStringClass = nodejsRuntime.Import("fluid-framework", "SharedString");
```
As explained above, importing modules must be done on the JS thread.

## Debugging the JavaScript code in a .NET process
```C#
int pid = Process.GetCurrentProcess().Id;
Uri inspectionUri = nodejsRuntime.StartInspector();
Debug.WriteLine($"Node.js ({pid}) inspector listening at {inspectionUri}");
```
Then attach a JavaScript debugger such as VS Code or Chrome to the inspection URI.

## Upgrading from LibNode 20.x

- `NodeEmbeddingRuntime.RunEventLoopNoWait()` now returns at once without waiting for more work.
  A `true` return value means the loop is still alive, not that it waited for an event. Use
  `RunEventLoopOnce()` or `RunEventLoop()` when you need to wait.
- LibNode no longer ships x86 binaries. Use x64 or arm64, including for the application process.
- The default `Import()` path uses `require()`. In Node.js 26, this can load ES modules too,
  unless their module graph uses top-level await (`ERR_REQUIRE_ASYNC_MODULE`). Use
  `esModule: true` for dynamic `import()`, which returns a promise resolving to the module
  namespace (or requested property); `ImportAsync()` awaits that promise.
- .NET Framework apps must run 64-bit. Keep the SDK default AnyCPU with `Prefer32Bit` false,
  or set `PlatformTarget` to `x64`/`arm64` or `RuntimeIdentifier` to `win-x64`/`win-arm64`.
  Do not target x86 or enable `Prefer32Bit`. IIS app pools must also run 64-bit.
