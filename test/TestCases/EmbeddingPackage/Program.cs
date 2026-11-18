// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;

internal static class Program
{
    private static async Task Main()
    {
        using NodeEmbeddingPlatform platform = new(new NodeEmbeddingPlatformSettings());
        using NodeEmbeddingThreadRuntime runtime = platform.CreateThreadRuntime(
            AppDomain.CurrentDomain.BaseDirectory,
            new NodeEmbeddingRuntimeSettings
            {
                MainScript = "globalThis.require = require('module').createRequire(process.execPath);"
            });
        await runtime.RunAsync(async () =>
        {
            JSValue module = await runtime.ImportAsync("./esm.mjs", esModule: true);
            Console.WriteLine("ESM=" + (string)module["result"]);
            Console.WriteLine("NODE=" + (string)JSValue.Global["process"]["versions"]["node"]);
            Console.WriteLine("BITS=" + (Environment.Is64BitProcess ? "64" : "32"));
        });
    }
}
