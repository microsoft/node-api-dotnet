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
            console.log('This printed after about 1 second');
        }, 1000);
        """);

    JSNativeApi.RunScript("""
        function later(delay, value) {
          return new Promise(function(resolve) {
            setTimeout(function() {
              resolve(value);
            }, delay);
          });
        }

        var l1 = later(100, "l1");
        l1.then(msg => { console.log("Print:" + msg); })
          .catch(() => { console.log("l1 canceled"); });
        """);
});

await runtime.CloseAsync();
await controller.ShutdownQueueAsync();
