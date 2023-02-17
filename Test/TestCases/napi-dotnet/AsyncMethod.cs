using System.Threading.Tasks;

namespace NodeApi.TestCases;

public static class AsyncMethod
{
    // This method calls C# async method and returns JS Promise
    [JSExport("async_method")]
    public static JSValue Test(JSCallbackArgs args)
    {
        JSValue result = JSValue.CreatePromise(out JSDeferred deferred);
        AsyncGreeter(deferred, (string)args[0]);
        return result;
    }

    // The async method must create JSSynchronizationContext at the beginning to use
    // the JS thread after we return from a background thread.
    // Below the `Task.Delay(50)` is executed in a background thread.
    private static async void AsyncGreeter(JSDeferred deferred, string greeter)
    {
        using var asyncScope = new JSAsyncScope();
        await Task.Delay(50);
        deferred.Resolve((JSValue)$"Hey {greeter}!");
    }
}
