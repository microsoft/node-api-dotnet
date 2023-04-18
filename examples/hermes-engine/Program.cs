// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Hermes.Example;
using Microsoft.JavaScript.NodeApi;

JSDispatcherQueueController controller = JSDispatcherQueueController.CreateOnDedicatedThread();

HermesRuntime runtime = await HermesRuntime.Create(controller.DispatcherQueue);

await runtime.RunAsync(() =>
{
    JSNativeApi.RunScript("x = 2");
    Console.WriteLine($"Result: {(int)JSValue.Global["x"]}");
    Console.Out.Flush();

    JSNativeApi.RunScript("""
        setTimeout(function() {
            console.log('This is printed after 1 second delay');
        }, 1000);
        """);

    JSNativeApi.RunScript("""
        function createPromise(delay, value) {
          return new Promise(function(resolve) {
            setTimeout(function() {
              resolve(value);
            }, delay);
          });
        }

        var myPromise = createPromise(100, "myPromise");
        myPromise.then(msg => { console.log(msg + " completed"); })
                 .catch(() => { console.log("myPromise canceled"); });
        """);
});

await runtime.CloseAsync();
await controller.ShutdownQueueAsync();
