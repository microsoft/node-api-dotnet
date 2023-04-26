// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Runtimes;
using Xunit;

namespace Microsoft.JavaScript.NodeApi.Test;

using static TestUtils;

public class NodejsEmbeddingTests
{
    private static string LibnodePath { get; } = GetLibnodePath();

    // The Node.js platform may only be initialized once per process.
    private static NodejsPlatform? NodejsPlatform { get; } =
        File.Exists(LibnodePath) ? new(LibnodePath) : null;

    [SkippableFact]
    public void NodejsStart()
    {
        Skip.If(NodejsPlatform == null, "Node shared library not found at " + LibnodePath);
        using NodejsEnvironment nodejs = NodejsPlatform.CreateEnvironment();

        nodejs.SynchronizationContext.Run(() =>
        {
            JSValue result = JSNativeApi.RunScript("require('node:path').join('a', 'b')");
            Assert.Equal(Path.Combine("a", "b"), (string)result);
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public void NodejsUnhandledRejection()
    {
        Skip.If(NodejsPlatform == null, "Node shared library not found at " + LibnodePath);
        using NodejsEnvironment nodejs = NodejsPlatform.CreateEnvironment();

        string? errorMessage = null;
        nodejs.UnhandledPromiseRejection += (_, e) =>
        {
            errorMessage = (string)e.Error.GetProperty("message");
        };

        nodejs.SynchronizationContext.Run(() =>
        {
            JSNativeApi.RunScript("new Promise((resolve, reject) => reject(new Error('test')))");
        });

        // The unhandled rejection event is not synchronous. Wait for it.
        for (int wait = 10; wait < 1000 && errorMessage == null; wait += 10) Thread.Sleep(10);

        Assert.Equal("test", errorMessage);
    }

    [SkippableFact]
    public void NodejsErrorPropagation()
    {
        Skip.If(NodejsPlatform == null, "Node shared library not found at " + LibnodePath);
        using NodejsEnvironment nodejs = NodejsPlatform.CreateEnvironment();

        string? exceptionMessage = null;
        string? exceptionStack = null;

        nodejs.SynchronizationContext.Run(() =>
        {
            try
            {
                JSNativeApi.RunScript(
                    "function throwError() { throw new Error('test'); }\n" +
                    "throwError();");
            }
            catch (JSException ex)
            {
                exceptionMessage = ex.Message;
                exceptionStack = ex.StackTrace;
            }
        });

        Assert.Equal("test", exceptionMessage);

        Assert.NotNull(exceptionStack);
        string[] stackLines = exceptionStack.Split('\n').Select((line) => line.Trim()).ToArray();

        // The first line of the stack trace should refer to the JS function that threw.
        Assert.StartsWith("at throwError ", stackLines[0]);

        // The stack trace should include lines that refer to the .NET method that called JS.
        Assert.Contains(
            stackLines,
            (line) => line.StartsWith($"at {typeof(NodejsEmbeddingTests).FullName}."));
    }
}
