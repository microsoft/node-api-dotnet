# Exceptions

## JavaScript calling .NET

If JavaScript code calls a .NET property or method that throws an exception, the exception
gets re-thrown to JS as a JavaScript `Error`.

For now, only the base `Error` type is thrown. Eventually the JS marshaller could
[generate different `Error` subclasses for each .NET `Exception` subclass](https://github.com/microsoft/node-api-dotnet/issues/205).

To explicitly throw a specific type of error from .NET to JavaScript, use the one of the `Throw*`
methods of the [`JSError`](./dotnet/Microsoft.JavaScript.NodeApi/JSError) class.

## .NET calling JavaScript

If .NET code calls a JS function that throws an error, the error gets re-thrown to .NET as a
[`JSException`](./dotnet/Microsoft.JavaScript.NodeApi/JSException). The
[`JSException.Error`](./dotnet/Microsoft.JavaScript.NodeApi/JSException/Error) property provides
access to the error value that was thrown from JavaScript.

## Combined stack traces

When exceptions/errors thrown in .NET or JS are propagated across the boundary between runtimes,
their stack traces are automatically combined.

In this example, JavaScript code calls a .NET method that throws an exception:

```
Error: Test error thrown by .NET.
    at Microsoft.JavaScript.NodeApi.TestCases.Errors.ThrowDotnetError(String message) in D:\node-api-dotnet\test\TestCases\Errors.cs:line 13
    at Microsoft.JavaScript.NodeApi.Generated.Module.Errors_ThrowDotnetError(JSCallbackArgs __args) in napi-dotnet.NodeApi.g.cs:line 357
    at Microsoft.JavaScript.NodeApi.JSNativeApi.InvokeCallback[TDescriptor](napi_env env, napi_callback_info callbackInfo, JSValueScopeType scopeType, Func`2 getCallbackDescriptor) in JSNativeApi.cs:line 1070
    at catchDotnetError (D:\node-api-dotnet\test\TestCases\errors.js:14:12)
    at Object.<anonymous> (D:\node-api-dotnet\test\TestCases\errors.js:41:1)
```
 - Frame 5: ThrowDotnetError() - C# method that threw the exception
 - Frame 4: Errors_ThrowDotnetError() - Marshalling code (auto-generated)
 - Frame 3: InvokeCallback() - JS to .NET transition
 - Frame 2: catchDotnetError() - JS code that called the .NET method and caught its error
 - Frame 0: Top-level JS statement that called catchDotnetError()
