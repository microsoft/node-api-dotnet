// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.TestCases;

public static class AsyncMethods
{
    // This method calls a C# async method and returns a JS Promise.
    [JSExport("async_method")]
    public static JSValue JSTest(JSCallbackArgs args)
    {
        string greeter = (string)args[0];
        return new JSPromise(async (resolve) =>
        {
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

    [JSExport("async_interface")]
    public static IAsyncInterface InterfaceTest { get; set; } = new AsyncTest();

    [JSExport("async_interface_reverse")]
    public static async Task<string> ReverseInterfaceTest(
        IAsyncInterface jsInterface, string greeter)
    {
        // ConfigureAwait(false) does not return to the JS thread, but the interface callback
        // should still use the JS thread.
        await Task.Delay(50).ConfigureAwait(false);

        // Invoke a method on a JS object that implements the interface.
        return await jsInterface.TestAsync(greeter);
    }

    private class AsyncTest : IAsyncInterface
    {
        public async Task<string> TestAsync(string greeter)
        {
            await Task.Delay(50);
            return $"Hey {greeter}!";
        }
    }
}

[JSExport]
public interface IAsyncInterface
{
    public Task<string> TestAsync(string greeter);
}
