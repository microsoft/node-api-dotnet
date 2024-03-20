// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using Microsoft.JavaScript.NodeApi.Runtime;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

public class NodejsEmbeddingTests
{
    private static string LibnodePath { get; } = GetLibnodePath();

    // The Node.js platform may only be initialized once per process.
    internal static NodejsPlatform? NodejsPlatform { get; } =
        File.Exists(LibnodePath) ? new(LibnodePath, args: new[] { "node", "--expose-gc" }) : null;

    internal static NodejsEnvironment CreateNodejsEnvironment()
    {
        Skip.If(NodejsPlatform == null, "Node shared library not found at " + LibnodePath);
        return NodejsPlatform.CreateEnvironment(Path.Combine(GetRepoRootDirectory(), "test"));
    }

    internal static void RunInNodejsEnvironment(Action action)
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();
        nodejs.SynchronizationContext.Run(action);
    }

    [SkippableFact]
    public void NodejsStart()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

        nodejs.SynchronizationContext.Run(() =>
        {
            JSValue result = JSValue.RunScript("require('node:path').join('a', 'b')");
            Assert.Equal(Path.Combine("a", "b"), (string)result);
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public void NodejsRestart()
    {
        // Create and destory a Node.js environment twice, using the same platform instance.
        NodejsStart();
        NodejsStart();
    }

    public interface IConsole { void Log(string message); }

    [SkippableFact]
    public void NodejsCallFunction()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

        nodejs.SynchronizationContext.Run(() =>
        {
            JSFunction func = (JSFunction)JSValue.RunScript("function jsFunction() { }; jsFunction");
            func.CallAsStatic();
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public void NodejsImportBuiltinModule()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();
        JSMarshaller marshaller = new() { AutoCamelCase = true };

        Exception? exception = null;
        nodejs.SynchronizationContext.Run(() =>
        {
            try
            {
                JSValue fsModule = nodejs.Import("fs");
                Assert.Equal(JSValueType.Object, fsModule.TypeOf());

                JSValue nodeFsModule = nodejs.Import("node:fs");
                Assert.Equal(JSValueType.Object, nodeFsModule.TypeOf());
            }
            catch (JSException ex)
            {
                exception = new Exception(ex.ToString());
            }
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
        Assert.Null(exception);
    }

    [SkippableFact]
    public void NodejsImportCommonJSModule()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();
        JSMarshaller marshaller = new() { AutoCamelCase = true };

        Exception? exception = null;
        nodejs.SynchronizationContext.Run(() =>
        {
            try
            {
                JSValue testModule = nodejs.Import("./test-cjs.js");
                Assert.Equal(JSValueType.Object, testModule.TypeOf());
                Assert.Equal(JSValueType.Function, testModule["test"].TypeOf());
            }
            catch (JSException ex)
            {
                exception = new Exception(ex.ToString());
            }
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
        Assert.Null(exception);
    }

    [SkippableFact]
    public void NodejsImportESModule()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();
        JSMarshaller marshaller = new() { AutoCamelCase = true };

        Exception? exception = null;
        nodejs.SynchronizationContext.Run(async () =>
        {
            try
            {
                JSReference importReference = new JSReference(nodejs.Import("./import.cjs"));
                Func<string, Task<JSValue>> importESModule = (string path) =>
                {
                    JSFunction importFunction = (JSFunction)importReference.GetValue()!.Value;
                    return ((JSPromise)importFunction.CallAsStatic(path)).AsTask();
                };

                JSValue esModule = await importESModule("./test-esm.mjs");

                Assert.Equal(JSValueType.Object, esModule.TypeOf());
                Assert.Equal(JSValueType.Function, esModule["test"].TypeOf());
            }
            catch (JSException ex)
            {
                exception = new Exception(ex.ToString());
            }
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
        Assert.Null(exception);
    }

    [SkippableFact]
    public void NodejsUnhandledRejection()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

        string? errorMessage = null;
        nodejs.UnhandledPromiseRejection += (_, e) =>
        {
            errorMessage = (string)e.Error.GetProperty("message");
        };

        nodejs.SynchronizationContext.Run(() =>
        {
            JSValue.RunScript("new Promise((resolve, reject) => reject(new Error('test')))");
        });

        // The unhandled rejection event is not synchronous. Wait for it.
        for (int wait = 10; wait < 1000 && errorMessage == null; wait += 10) Thread.Sleep(10);

        Assert.Equal("test", errorMessage);
    }

    [SkippableFact]
    public void NodejsErrorPropagation()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

        string? exceptionMessage = null;
        string? exceptionStack = null;

        nodejs.SynchronizationContext.Run(() =>
        {
            try
            {
                JSValue.RunScript(
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
