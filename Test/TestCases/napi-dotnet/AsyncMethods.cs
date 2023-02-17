using System;
using System.Threading.Tasks;

namespace NodeApi.TestCases;

public static class AsyncMethods
{
    // This method calls a C# async method and returns a JS Promise.
    [JSExport("async_method")]
    public static JSValue JSTest(JSCallbackArgs args)
    {
        return new JSPromise(async (resolve) =>
        {
            string greeter = (string)args[0];
            await Task.Delay(50);
            resolve((JSValue)$"Hey {greeter}!");
        });
    }

    // A JS adapter is auto-generated for this C# async method.
    [JSExport("async_method_cs")]
    public static async Task<string> CSTest(string greeter)
    {
        await Task.Delay(50);
        return $"Hey {greeter}!";
    }
}
