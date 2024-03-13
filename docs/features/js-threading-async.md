# JS Threading and Async Continuations

JavaScript engines have a single-threaded execution model. That means all access to JavaScript
data or operations must be performed from the JavaScript thread.
(It is possible run multiple JavaScript execution threads in a process using
[Web workers](https://developer.mozilla.org/en-US/docs/Web/API/Web_Workers_API) or the
[`NodejsEnvironment`](../reference/dotnet/Microsoft.JavaScript.NodeApi.Runtime/NodejsEnvironment)
class, but the thread affinity rules still apply.)

## Invalid thread access

All APIs in the `Microsoft.NodeApi.JavaScript` namespace (and child namespaces) must be used on
the JS thread, except where otherwise documented. Any attempt to access JavaScript values or
operations from the wrong thread will throw
[`JSInvalidThreadAccessException`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSInvalidThreadAccessException):

```C#
[JSExport]
public void InvalidThreadExample()
{
    Thread thread = new(() =>
    {
        var now = new JSDate(); // throws JSInvalidThreadAccessException
    });
    thread.Start();
    thread.Join();
}
```

Note that .NET tasks may run on another thread. And accessing a value from another thread is
invalid even if the value's scope has not been closed on the original thread:

```C#
[JSExport]
public async Task InvalidAsyncExample(JSValue value)
{
    await Task.Run(() => // The lambda runs on a thread-pool thread.
    {
        var valueString = (string)value; // throws JSInvalidThreadAccessException
    });
}
```

## Async continuations

Accessing JavaScript values after an `await` is valid. This works because the
[`JSSynchronizationContext`](../reference/dotnet/Microsoft.JavaScript.NodeApi.Interop/JSSynchronizationContext)
automatically manages returning to the JavaScript thread.

```C#
[JSExport]
public async Task ValidAwaitExample(JSValue value)
{
    await HelperMethodAsync();
    // The synchronization context returns to the JS thread after awaiting.

    var valueString = (string)value; // Does not throw!
}
```

However, using `ConfigureAwait(false)` disables use of the synchronization context:

```C#
[JSExport]
public async Task InvalidConfigureAwaitExample(JSValue value)
{
    await HelperMethodAsync().ConfigureAwait(false);
    // ConfigureAwait(false) prevents returning to the JS thread.

    var valueString = (string)value; // throws JSInvalidThreadAccessException
}
```
