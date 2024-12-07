// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma warning disable CA1822 // Mark members as static

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

public class NodejsEmbeddingTests
{
    private static string MainScript { get; } =
        "globalThis.require = require('module').createRequire(process.execPath);\n";

    private static string LibnodePath { get; } = GetLibnodePath();

    // The Node.js platform may only be initialized once per process.
    internal static NodejsEmbeddingPlatform? NodejsPlatform { get; } =
        File.Exists(LibnodePath)
            ? new(LibnodePath, new NodejsEmbeddingPlatformSettings
            {
                Args = new[] { "node", "--expose-gc" }
            })
            : null;

    internal static NodejsEmbeddingThreadRuntime CreateNodejsEnvironment()
    {
        Skip.If(NodejsPlatform == null, "Node shared library not found at " + LibnodePath);
        return NodejsPlatform.CreateThreadRuntime(
            Path.Combine(GetRepoRootDirectory(), "test"),
            new NodejsEmbeddingRuntimeSettings { MainScript = MainScript });
    }

    internal static void RunInNodejsEnvironment(Action action)
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();
        nodejs.SynchronizationContext.Run(action);
    }

    [SkippableFact]
    public void LoadMainScriptNoThread()
    {
        Skip.If(NodejsPlatform == null, "Node shared library not found at " + LibnodePath);
        using var runtime = new NodejsEmbeddingRuntime(NodejsPlatform,
            new NodejsEmbeddingRuntimeSettings { MainScript = MainScript });
        runtime.CompleteEventLoop();
    }

    [SkippableFact]
    public void LoadMainScriptWithThread()
    {
        using NodejsEmbeddingThreadRuntime runtime = CreateNodejsEnvironment();
    }

    [SkippableFact]
    public void StartEnvironment()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        nodejs.Run(() =>
        {
            JSValue result = JSValue.RunScript("require('node:path').join('a', 'b')");
            Assert.Equal(Path.Combine("a", "b"), (string)result);
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public void RestartEnvironment()
    {
        // Create and destroy a Node.js environment twice, using the same platform instance.
        StartEnvironment();
        StartEnvironment();
    }

    public interface IConsole { void Log(string message); }

    [SkippableFact]
    public void CallFunction()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        nodejs.SynchronizationContext.Run(() =>
        {
            JSFunction func = (JSFunction)JSValue.RunScript(
                "function jsFunction() { }; jsFunction");
            func.CallAsStatic();
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public void ImportBuiltinModule()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        nodejs.Run(() =>
        {
            JSValue fsModule = nodejs.Import("fs");
            Assert.Equal(JSValueType.Object, fsModule.TypeOf());
            Assert.Equal(JSValueType.Function, fsModule["stat"].TypeOf());

            JSValue nodeFsModule = nodejs.Import("node:fs");
            Assert.Equal(JSValueType.Object, nodeFsModule.TypeOf());
            Assert.Equal(JSValueType.Function, nodeFsModule["stat"].TypeOf());
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public void ImportCommonJSModule()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        nodejs.Run(() =>
        {
            JSValue testModule = nodejs.Import("./test-module.cjs");
            Assert.Equal(JSValueType.Object, testModule.TypeOf());
            Assert.Equal(JSValueType.Function, testModule["test"].TypeOf());
            Assert.Equal("test", testModule.CallMethod("test"));
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public void ImportCommonJSPackage()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        nodejs.Run(() =>
        {
            JSValue testModule = nodejs.Import("./test-cjs-package");
            Assert.Equal(JSValueType.Object, testModule.TypeOf());
            Assert.Equal(JSValueType.Function, testModule["test"].TypeOf());
            Assert.Equal("test", testModule.CallMethod("test"));
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public async Task ImportESModule()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        await nodejs.RunAsync(async () =>
        {
            JSValue testModule = await nodejs.ImportAsync(
                "./test-module.mjs", null, esModule: true);
            Assert.Equal(JSValueType.Object, testModule.TypeOf());
            Assert.Equal(JSValueType.Function, testModule["test"].TypeOf());
            Assert.Equal("test", testModule.CallMethod("test"));
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public async Task ImportESPackage()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        await nodejs.RunAsync(async () =>
        {
            JSValue testModule = await nodejs.ImportAsync(
                "./test-esm-package", esModule: true);
            Assert.Equal(JSValueType.Object, testModule.TypeOf());
            Assert.Equal(JSValueType.Function, testModule["test"].TypeOf());
            Assert.Equal("test", testModule.CallMethod("test"));

            // Check that module resolution handles sub-paths from conditional exports.
            // https://nodejs.org/api/packages.html#conditional-exports
            JSValue testModuleFeature = await nodejs.ImportAsync(
                "./test-esm-package/feature", esModule: true);
            Assert.Equal(JSValueType.Object, testModuleFeature.TypeOf());
            Assert.Equal(JSValueType.Function, testModuleFeature["test2"].TypeOf());
            Assert.Equal("test2", testModuleFeature.CallMethod("test2"));

            // Directly import a property from the module
            JSValue testModuleProperty = await nodejs.ImportAsync(
                "./test-esm-package", "test", esModule: true);
            Assert.Equal(JSValueType.Function, testModuleProperty.TypeOf());
            Assert.Equal("test", testModuleProperty.Call());
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public void UnhandledRejection()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        string? errorMessage = null;
        nodejs.UnhandledPromiseRejection += (_, e) =>
        {
            errorMessage = (string)e.Error.GetProperty("message");
        };

        nodejs.Run(() =>
        {
            JSValue.RunScript("new Promise((resolve, reject) => reject(new Error('test')))");
        });

        // The unhandled rejection event is not synchronous. Wait for it.
        for (int wait = 10; wait < 1000 && errorMessage == null; wait += 10) Thread.Sleep(10);

        Assert.Equal("test", errorMessage);
    }

    [SkippableFact]
    public void ErrorPropagation()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        JSException exception = Assert.Throws<JSException>(() =>
        {
            nodejs.Run(() =>
            {
                JSValue.RunScript(
                    "function throwError() { throw new Error('test'); }\n" +
                    "throwError();");
            });
        });

        Assert.Equal("Exception thrown from JS thread: test", exception.Message);
        Assert.IsType<JSException>(exception.InnerException);

        exception = (JSException)exception.InnerException;
        Assert.Equal("test", exception.Message);

        Assert.NotNull(exception.StackTrace);
        string[] stackLines = exception.StackTrace
            .Split('\n')
            .Select((line) => line.Trim())
            .ToArray();

        // The first line of the stack trace should refer to the JS function that threw.
        Assert.StartsWith("at throwError ", stackLines[0]);

        // The stack trace should include lines that refer to the .NET method that called JS.
        Assert.Contains(
            stackLines,
            (line) => line.StartsWith($"at {typeof(NodejsEmbeddingTests).FullName}."));
    }

    [SkippableFact]
    public async Task WorkerIsMainThread()
    {
        await TestWorker(
            mainPrepare: () =>
            {
                Assert.True(NodeWorker.IsMainThread);
                return new NodeWorker.Options { Eval = true };
            },
            workerScript: @"
const assert = require('node:assert');
const { isMainThread } = require('node:worker_threads');
assert(!isMainThread);
",
            mainRun: (worker) => Task.CompletedTask);
    }

    [SkippableFact]
    public async Task WorkerArgs()
    {
        await TestWorker(
            mainPrepare: () =>
            {
                return new NodeWorker.Options
                {
                    Eval = true,
#pragma warning disable CA1861 // Prefer 'static readonly' fields over constant array arguments
                    Argv = new[] { "test1", "test2" },
#pragma warning restore CA1861
                    WorkerData = true,
                };
            },
            workerScript: @"
const assert = require('node:assert');
const process = require('node:process');
const { workerData } = require('node:worker_threads');
assert.deepStrictEqual(process.argv.slice(2), ['test1', 'test2']);
assert.strictEqual(typeof workerData, 'boolean');
assert(workerData);
",
            mainRun: (worker) => Task.CompletedTask);
    }

    [SkippableFact]
    public async Task WorkerEnv()
    {
        await TestWorker(
            mainPrepare: () =>
            {
                NodeWorker.SetEnvironmentData("test", JSValue.True);
                return new NodeWorker.Options
                {
                    Eval = true,
                };
            },
            workerScript: @"
const assert = require('node:assert');
const { getEnvironmentData } = require('node:worker_threads');
assert.strictEqual(getEnvironmentData('test'), true);
",
            mainRun: (worker) => Task.CompletedTask);
    }

    [SkippableFact]
    public async Task WorkerMessages()
    {
        await TestWorker(
            mainPrepare: () =>
            {
                return new NodeWorker.Options { Eval = true };
            },
            workerScript: @"
const { parentPort } = require('node:worker_threads');
parentPort.on('message', (msg) => parentPort.postMessage(msg)); // echo
",
            mainRun: async (worker) =>
            {
                TaskCompletionSource<string> echoCompletion = new();
                worker.Message += (_, e) => echoCompletion.TrySetResult((string)e.Value);
                worker.Error += (_, e) => echoCompletion.TrySetException(
                    new JSException(e.Error));
                worker.Exit += (_, e) => echoCompletion.TrySetException(
                    new InvalidOperationException("Worker exited without echoing!"));
                worker.PostMessage("test");
                string echo = await echoCompletion.Task;
                Assert.Equal("test", echo);
            });
    }

    [SkippableFact]
    public async Task WorkerStdinStdout()
    {
        await TestWorker(
            mainPrepare: () =>
            {
                return new NodeWorker.Options
                {
                    Eval = true,
                    Stdin = true,
                    Stdout = true,
                };
            },
            workerScript: @"process.stdin.pipe(process.stdout)",
            mainRun: async (worker) =>
            {
                TaskCompletionSource<string> echoCompletion = new();
                worker.Error += (_, e) => echoCompletion.TrySetException(
                    new JSException(e.Error));
                worker.Exit += (_, e) => echoCompletion.TrySetException(
                    new InvalidOperationException("Worker exited without echoing!"));
                Assert.NotNull(worker.Stdin);
                await worker.Stdin.WriteAsync(Encoding.ASCII.GetBytes("test\n"), 0, 5);
                byte[] buffer = new byte[25];
                int count = await worker.Stdout.ReadAsync(buffer, 0, buffer.Length);
                Assert.Equal("test\n", Encoding.ASCII.GetString(buffer, 0, count));
            });
    }

    private static async Task TestWorker(
        Func<NodeWorker.Options> mainPrepare,
        string workerScript,
        Func<NodeWorker, Task> mainRun)
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();
        await nodejs.RunAsync(async () =>
        {
            NodeWorker.Options workerOptions = mainPrepare.Invoke();
            NodeWorker worker = new(workerScript, workerOptions);

            TaskCompletionSource<bool> onlineCompletion = new();
            worker.Online += (sender, e) => onlineCompletion.SetResult(true);
            TaskCompletionSource<int> exitCompletion = new();
            worker.Error += (sender, e) => exitCompletion.SetException(new JSException(e.Error));
            worker.Exit += (sender, e) => exitCompletion.TrySetResult(e.ExitCode);

            await onlineCompletion.Task;
            try
            {
                await mainRun.Invoke(worker);
            }
            finally
            {
                await worker.Terminate();
            }
            await exitCompletion.Task;
        });
    }

    /// <summary>
    /// Tests the functionality of dynamically exporting and marshalling a class type from .NET
    /// to JS (as opposed to relying on [JSExport] (compile-time code-generation) for marshalling.
    /// </summary>
    [SkippableFact]
    public void MarshalClass()
    {
        using NodejsEmbeddingThreadRuntime nodejs = CreateNodejsEnvironment();

        nodejs.Run(() =>
        {
            JSMarshaller marshaller = new();
            TypeExporter exporter = new(marshaller);
            exporter.ExportType(typeof(TestClass));
            TestClass obj = new()
            {
                Value = "test"
            };
            JSValue objJs = marshaller.ToJS(obj);
            Assert.Equal(JSValueType.Object, objJs.TypeOf());
            Assert.Equal("test", (string)objJs["Value"]);
        });
    }

    // Used for marshalling tests.
    public class TestClass
    {
        public string? Value { get; set; }
    }
}
