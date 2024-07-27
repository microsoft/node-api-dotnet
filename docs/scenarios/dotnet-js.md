# Embed a JS runtime in a .NET application

::: warning :warning: WARNING
This functionality is still experimental. It works, but the process is not as streamlined as it
should be.
:::

## Building the required `libnode` binary
This project depends on a [PR to Node.js](https://github.com/nodejs/node/pull/43542) that adds
simpler ABI-stable embedding APIs to `libnode`. Until that PR is merged, you'll need to build your
own binary from a branch. And even after it's merged, the Node.js project does not currently and
has no plans to publish official `libnode` binaries.

1. Install the [prerequisites for building Node.js](https://github.com/nodejs/node/blob/main/BUILDING.md#building-nodejs-on-supported-platforms).
(On Windows this is basically Python 3 and either VS 2022 or the C++ build tools.)

2. Clone the napi-libnode repo/branch:

```shell
mkdir libnode
cd libnode
git clone https://github.com/jasongin/nodejs -b napi-libnode-v20.9.0 --single-branch .
```

3. Build in shared-library mode:
::: code-group
```shell [Windows]
.\vcbuild.bat x64 release dll openssl-no-asm
```
```shell [Mac / Linux]
./configure --shared; make -j4
```
:::
The build may take an hour or more depending on the speed of your system.

4. A successful build produces a binary in the `out/Release` directory:
    - `libnode.dll` (Windows)
    - `libnode.dylib` (Mac)
    - `libnode.???.so` (Linux)

## Importing JS modules into .NET

1. Load `libnode` and initialize a Node.js "platform" and "environment" instance:
```C#
// Find the path to the libnode binary for the current platform.
string baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
string libnodePath = Path.Combine(baseDir, "libnode.dll");
NodejsPlatform nodejsPlatform = new(libnodePath);
var nodejs = nodejsPlatform.CreateEnvironment(baseDir);
```

There can only be one `NodejsPlatform` instance per process, so typically it would be stored
in a static variable. From the platform, multiple `NodejsEnvironment` instances may be created
and disposed.

The directory provided to the environment instance is the base for package resolution. Any packages
or modules imported in the next step must be installed (in a `node_modules` directory) in that base
directory or a parent directory.

2. Invoke a simple script from C#:
```C#
await nodejs.RunAsync(() =>
{
    JSValue.RunScript("console.log('Hello from JS!');");
});
```

Be aware of JavaScript's single-threaded execution model. **All JavaScript operations must be
performed from the JS environment thread**, unless otherwise noted. Use any of the `Run()`,
`RunAsync()`, `Post()`, or `PostAsync()` methods on the JS environment instance to switch to the
JS thread. Also keep in mind any JavaScript values of type `JSValue` (or any of the more specific
JS value struct types) are not valid after switching off the JS thread. A `JSReference` can hold
on to a JS value for future use. See also
[JS Threading and Async Continuations](../features/js-threading-async) and
[JS References](../features/js-references).

3. Import modules or module properties:
```C#
// Import * from the `fluid-framework` module. Items exported from the module will be
// available as properties on the returned JS object.
JSValue fluidPackage = nodejs.Import("fluid-framework");

// Import just the `SharedString` class from the `fluid-framework` module.
JSValue sharedStringClass = nodejs.Import("fluid-framework", "SharedString");
```
As explained above, importing modules must be done on the JS thread.

## Debugging the JavaScript code in a .NET process
```C#
int pid = Process.GetCurrentProcess().Id;
Uri inspectionUri = nodejs.StartInspector();
Debug.WriteLine($"Node.js ({pid}) inspector listening at {inspectionUri}");
```
Then attach a JavaScript debugger such as VS Code or Chrome to the inspection URI.
