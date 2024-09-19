# Node Worker Threads

[Node worker threads](https://nodejs.org/api/worker_threads.html) enable parallel execution of
JavaScript in the same process. They are ideal for CPU-intensive JavaScript operations. They are
less suited to I/O-intensive work, where the Node.js built-in asynchronous I/O operations are more
efficient than Workers.

The [NodeWorker](../reference/dotnet/Microsoft.JavaScript.NodeApi.Interop/NodeWorker) class enables
C# code to create Node worker threads in the same process, and communicate with them.

## JS worker threads

To create a worker, construct a new `NodeWorker` instance with the path to the worker JavaScript
file:

```C#
var worker = new NodeWorker(@".\myWorker.js", new NodeWorker.Options());
```

Or provide the worker script directly as a string, using the `Eval` option:
```C#
var worker = new NodeWorker(@"
    const assert = require('node:assert');
    const { isMainThread } = require('node:worker_threads');
    assert(!isMainThread); // This script is running as a worker.
    ", new NodeWorker.Options { Eval = true });
```

Messages (any serializable JS values) can be passed back and forth between the C# host and the JS
worker:
```C#
var worker = new NodeWorker(@"
    const { parentPort } = require('node:worker_threads');
    parentPort.on('message', (msg) => {
        parentPort.postMessage(msg); // echo
    });
    ", new NodeWorker.Options { Eval = true });

// Wait for the worker to start before sending a message.
TaskCompletionSource<bool> onlineCompletion = new();
worker.Online += (sender, e) => onlineCompletion.TrySetResult(true);
worker.Error += (sender, e) => onlineCompletion.TrySetException(new JSException(e.Error));
await onlineCompletion.Task;

// Send a message and verify the response.
TaskCompletionSource<string> echoCompletion = new();
worker.Message += (_, e) => echoCompletion.TrySetResult((string)e.Value);
worker.Error += (_, e) => echoCompletion.TrySetException(
    new JSException(e.Error));
worker.Exit += (_, e) => echoCompletion.TrySetException(
    new InvalidOperationException("Worker exited without echoing!"));
worker.PostMessage("hello");
string echo = await echoCompletion.Task;
Assert.Equal("hello", echo);
```

## C# worker threads

::: warning :construction: COMING SOON
This functionality is not available yet, but is coming soon.
:::

Instead of starting a worker with a JavaScript file, it will be possible to provide a C# delegate.
The delegate callback will be invoked on the JS worker thread; then it can orchestrate importing
JavaScript packages, callilng JS functions, or whatever is needed to do the work on the thread.
