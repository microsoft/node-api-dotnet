# Async, Tasks, and Promises

| C# Type   | JS Type         |
|-----------|-----------------|
| `Task`    | `Promise<void>` |
| `Task<T>` | `Promise<T>`    |

## Async methods

A .NET method that returns a `Task` or `Task<T>` can be awaited in JavaScript because the
`Task` is automatically marshalled as a JS `Promise`:

```C#
[JSExport]
public static class AsyncExample
{
    public async Task<string> GetResultAsync();
}
```

```JS
const result = await AsyncExample.getResultAsync();
```

A JavaScript method that returns a `Promise` can be awaited in C# by converting the `Promise` to a
`Task`:

```C#
JSFunction exampleAsyncJSFunc = …
string result = await exampleAsyncJSFunc.CallAsStatic(arg).AsTask<string>();
```

## `JSPromise` type

The [`JSPromise`](./dotnet/Microsoft.JavaScript.NodeApi/JSPromise) type supports working directly
with JavaScript `Promise` values, while the extension methods in the
[`JSPromiseExtensions`](./dotnet/Microsoft.JavaScript.NodeApi/TaskExtensions)
class enable converting between `JSPromise` and `Task` values.

## Async execution
See [JS Threading and Async Continuations](../features/js-threading-async) for more about
coordinating asynchronous .NET and JavaScript execution.
