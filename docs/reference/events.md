# Events

.NET Events are [not yet supported](https://github.com/microsoft/node-api-dotnet/issues/59).

It is possible to work with JS events from .NET by calling the JS `addEventListener()` or similar
method and passing a [`JSFunction`](./dotnet/Microsoft.JavaScript.NodeApi/JSFunction) callback.
But there is no automatic marshalling yet to project a JS event as a .NET event.
