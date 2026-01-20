# Streams

| C# Type               | JS Type    |
|-----------------------|------------|
| `Stream` (read/write) | `Duplex`   |
| `Stream` (read-only)  | `Readable` |
| `Stream` (write-only) | `Writable` |

A .NET `Stream` instance is marshalled to and from Node.js
[`Duplex`](https://nodejs.org/api/stream.html#duplex-and-transform-streams),
[`Readable`](https://nodejs.org/api/stream.html#readable-streams), or
[`Writable`](https://nodejs.org/api/stream.html#writable-streams),
depending whether the stream supports reading and/or writing. JS code can seamlessly read from
or write to streams created by .NET, or .NET code can read from or write to streams created by JS.
Streamed data is transferred using shared memory (without any additional sockets or pipes), so
memory allocation and copying is minimized.

```C#
[JSExport]
public static class Example
{
    public Stream GetContent() { … }
}
```

```JS
const stream = Example.getContent();
stream.on('data', (chunk) => { console.log(chunk.toString()); });
```

The [`NodeStream`](./dotnet/Microsoft.JavaScript.NodeApi.Interop/NodeStream) class provides the
.NET `Stream` adapter over a Node.js `Duplex`, `Readable`, or `Writable` stream.

When [building a module using C# Native AOT](../scenarios/js-aot-module), the Node API module needs
the `require()` function at initialization time to load the `node:stream` module. This must be
provided via a global variable *before* loading the AOT module itself. 

```JS
global.node_api_dotnet = { require };
const Example = require(...);

const stream = Example.getContent();
stream.on('data', (chunk) => { console.log(chunk.toString()); });
```
