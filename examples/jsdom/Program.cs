using System;
using System.Diagnostics;
using System.IO;
using Microsoft.JavaScript.NodeApi.Runtime;

namespace Microsoft.JavaScript.NodeApi.Examples;

public static class Program
{
    public static void Main()
    {
        string appDir = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        string libnodePath = Path.Combine(appDir, "libnode.dll");
        using NodeEmbeddingPlatform nodejsPlatform = new(libnodePath, null);
        using NodeEmbeddingThreadRuntime nodejs = nodejsPlatform.CreateThreadRuntime(appDir,
            new NodeEmbeddingRuntimeSettings
            {
                MainScript =
                    "globalThis.require = require('module').createRequire(process.execPath);\n"
            });
        if (Debugger.IsAttached)
        {
            int pid = Process.GetCurrentProcess().Id;
            Uri inspectionUri = nodejs.StartInspector();
            Debug.WriteLine($"Node.js ({pid}) inspector listening at {inspectionUri.AbsoluteUri}");
        }

        string html = "<!DOCTYPE html><p>Hello world!</p>";
        string content = nodejs.Run(() => GetContent(nodejs, html));
        Console.WriteLine(content);
    }

    private static string GetContent(NodeEmbeddingThreadRuntime nodejs, string html)
    {
        JSValue jsdomClass = nodejs.Import(module: "jsdom", property: "JSDOM");
        JSValue dom = jsdomClass.CallAsConstructor(html);
        JSValue document = dom["window"]["document"];
        string content = (string)document.CallMethod("querySelector", "p")["textContent"];
        return content;
    }
}
