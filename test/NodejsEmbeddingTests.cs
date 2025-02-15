// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma warning disable CA1822 // Mark members as static

using System;
using System.Collections.Generic;
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

    // The Node.js platform may only be initialized once per process.
    internal static NodeEmbeddingPlatform NodejsPlatform { get; } =
        new(new NodeEmbeddingPlatformSettings
        {
            Args = new[] { "node", "--expose-gc" }
        });

    internal static NodeEmbeddingThreadRuntime CreateNodeEmbeddingThreadRuntime()
    {
        return NodejsPlatform.CreateThreadRuntime(
            Path.Combine(GetRepoRootDirectory(), "test"),
            new NodeEmbeddingRuntimeSettings
            {
                MainScript = MainScript,
            });
    }

    internal static void RunInNodejsEnvironment(Action action)
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();
        nodejs.SynchronizationContext.Run(action);
    }

    [Fact]
    public void LoadMainScriptNoThread()
    {
        using NodeEmbeddingRuntime runtime = NodeEmbeddingRuntime.Create(NodejsPlatform,
            new NodeEmbeddingRuntimeSettings { MainScript = MainScript });
        runtime.RunEventLoop();
    }

    [Fact]
    public void LoadMainScriptWithThread()
    {
        using NodeEmbeddingThreadRuntime runtime = CreateNodeEmbeddingThreadRuntime();
    }

    [Fact]
    public void StartEnvironment()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

        nodejs.Run(() =>
        {
            JSValue result = JSValue.RunScript("require('node:path').join('a', 'b')");
            Assert.Equal(Path.Combine("a", "b"), (string)result);
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [Fact]
    public void RestartEnvironment()
    {
        // Create and destroy a Node.js environment twice, using the same platform instance.
        StartEnvironment();
        StartEnvironment();
    }

    public interface IConsole { void Log(string message); }

    [Fact]
    public void CallFunction()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

        nodejs.SynchronizationContext.Run(() =>
        {
            JSFunction func = (JSFunction)JSValue.RunScript(
                "function jsFunction() { }; jsFunction");
            func.CallAsStatic();
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    private static string GetNormalizedStackTrace(Exception ex)
    {
        var stack = (ex.StackTrace ?? "").Replace("\r", "");
        IEnumerable<string> stackLines = stack.Split('\n');
        stackLines = stackLines.Where((line) => !line.StartsWith("---"));
        Assert.True(stackLines.All((line) => line.StartsWith("   at ")));
        stackLines = stackLines.Select((line) => line.Substring(6));
        return string.Join("\n", stackLines);
    }

    private static void ThrowJSError(JSValue message)
    {
        var throwJSError = (JSFunction)JSValue.RunScript(
            "(function throwJSError(msg) { throw new Error(msg); })");
        throwJSError.CallAsStatic(message);
    }

    private static async Task ThrowJSAsyncError(JSValue message)
    {
        var throwJSAsyncError = (JSFunction)JSValue.RunScript(
            "(function throwJSAsyncError() { return Promise.reject(new Error('test')); })");
        var promise = (JSPromise)throwJSAsyncError.CallAsStatic(message);
        await promise.AsTask();
    }

    private static JSValue ThrowDotnetException(JSCallbackArgs args)
    {
        var message = (string)args[0];
        throw new ApplicationException(message);
    }

    [Fact]
    public void DotnetToJSException()
    {
        if (JSSynchronizationContext.Current == null)
        {
            using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();
            nodejs.Run(DotnetToJSException);
            nodejs.Dispose();
            Assert.Equal(0, nodejs.ExitCode);
            return;
        }

        try
        {
            ThrowJSError("test");
        }
        catch (Exception ex)
        {
            Assert.Equal("test", ex.Message);
            var stack = GetNormalizedStackTrace(ex);
            var stackRegex = $"""
                ^throwJSError in <anonymous>:line 1
                {typeof(NodejsEmbeddingTests).FullName}.{nameof(ThrowJSError)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                {typeof(NodejsEmbeddingTests).FullName}.{nameof(DotnetToJSException)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                """.Replace("\r", "");
            Assert.Matches(stackRegex, stack);
        }
    }

    [Fact]
    public async Task DotnetToJSAsyncException()
    {
        if (JSSynchronizationContext.Current == null)
        {
            using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();
            await nodejs.RunAsync(DotnetToJSAsyncException);
            nodejs.Dispose();
            Assert.Equal(0, nodejs.ExitCode);
            return;
        }

        try
        {
            await ThrowJSAsyncError("test");
        }
        catch (Exception ex)
        {
            Assert.Equal("test", ex.Message);
            var stack = GetNormalizedStackTrace(ex);
            var stackRegex = $"""
                ^throwJSAsyncError in <anonymous>:line 1
                {typeof(NodejsEmbeddingTests).FullName}.{nameof(ThrowJSAsyncError)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                {typeof(NodejsEmbeddingTests).FullName}.{nameof(DotnetToJSAsyncException)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                """.Replace("\r", "");
            Assert.Matches(stackRegex, stack);
        }
    }

    [Fact]
    public void DotnetToJSToDotnetException()
    {
        if (JSSynchronizationContext.Current == null)
        {
            using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();
            nodejs.Run(DotnetToJSToDotnetException);
            nodejs.Dispose();
            Assert.Equal(0, nodejs.ExitCode);
            return;
        }

        JSFunction callDotnet = (JSFunction)JSValue.RunScript(
            "(function callDotnet(f, msg) { f(msg); })");
        try
        {
            callDotnet.CallAsStatic(new JSFunction(ThrowDotnetException), "test");
        }
        catch (Exception ex)
        {
            Assert.Equal("test", ex.Message);
            var stack = GetNormalizedStackTrace(ex);
            var stackRegex = $"""
                ^{typeof(NodejsEmbeddingTests).FullName}.{nameof(ThrowDotnetException)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                callDotnet in <anonymous>:line 1
                {typeof(NodejsEmbeddingTests).FullName}.{nameof(DotnetToJSToDotnetException)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                """.Replace("\r", "");
            Assert.Matches(stackRegex, stack);
        }
    }

    [Fact]
    public void DotnetToJsToDotnetToJSException()
    {
        if (JSSynchronizationContext.Current == null)
        {
            using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();
            nodejs.Run(DotnetToJsToDotnetToJSException);
            nodejs.Dispose();
            Assert.Equal(0, nodejs.ExitCode);
            return;
        }

        JSFunction callDotnet = (JSFunction)JSValue.RunScript(
            "(function callDotnet(f, msg) { f(msg); })");
        try
        {
            callDotnet.CallAsStatic(new JSFunction(ThrowJSError), "test");
        }
        catch (Exception ex)
        {
            Assert.Equal("test", ex.Message);
            var stack = GetNormalizedStackTrace(ex);
            var stackRegex = $"""
                ^throwJSError in <anonymous>:line 1
                {typeof(NodejsEmbeddingTests).FullName}.{nameof(ThrowJSError)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                callDotnet in <anonymous>:line 1
                {typeof(NodejsEmbeddingTests).FullName}.{nameof(DotnetToJsToDotnetToJSException)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                """.Replace("\r", "");
            Assert.Matches(stackRegex, stack);
        }
    }

    [Fact]
    public void JSToDotnetException()
    {
        if (JSSynchronizationContext.Current == null)
        {
            using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();
            nodejs.Run(JSToDotnetException);
            nodejs.Dispose();
            Assert.Equal(0, nodejs.ExitCode);
            return;
        }

        JSFunction catchDotnetException = (JSFunction)JSValue.RunScript(
            "(function catchDotnetException(f, msg) { try { return f(msg); } catch (e) { return e.stack; } })");
        JSValue result = catchDotnetException.CallAsStatic(new JSFunction(ThrowDotnetException), "test");

        Assert.True(result.IsString());
        string stack = (string)result;
        Assert.StartsWith("Error: test\n", stack);
        stack = stack.Substring(stack.IndexOf('\n') + 1);

        // Note a stack trace obtained from within JS is formatted in the JS style:
        //  - Four spaces before "at"
        //  - Source file:line references in parentheses
        var stackRegex = $"""
            ^    at {typeof(NodejsEmbeddingTests).FullName}.{nameof(ThrowDotnetException)}\([^)]*\) \(.*{nameof(NodejsEmbeddingTests)}\.cs:\d+\)
                at catchDotnetException \(<anonymous>:\d+(:\d+)?\)
            """.Replace("\r", "");
        Assert.Matches(stackRegex, stack);
    }

    [Fact]
    public async Task ThreadSafeFunctionException()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

        string? unhandledErrorStack = null;
        await nodejs.RunAsync(async () =>
        {
            // An exception thrown from a TSFN callback should trigger an unhandled rejection.
            // (If a handler was not registered, it would end the process.)
            nodejs.UnhandledPromiseRejection += (_, e) =>
            {
                unhandledErrorStack = (string)e.Error["stack"];
            };

            JSThreadSafeFunction tsfn = new(
                maxQueueSize: 0,
                initialThreadCount: 1,
                asyncResourceName: (JSValue)nameof(NodejsEmbeddingTests));

            await Task.Run(() =>
            {
                Assert.Null(JSSynchronizationContext.Current);

                tsfn.BlockingCall(() =>
                {
                    ThrowJSError("Test TSFN exception");
                });
            });

            tsfn.BlockingCall(() => tsfn.Unref());
        });

        Assert.NotNull(unhandledErrorStack);
        Assert.StartsWith("Error: Test TSFN exception\n", unhandledErrorStack);
        unhandledErrorStack = unhandledErrorStack.Substring(unhandledErrorStack.IndexOf('\n') + 1);

        // A stack trace obtained from the UnhandledPromiseRejection event is formatted in the JS style.
        var stackRegex = $"""
            ^    at throwJSError \(<anonymous>:1\)
                at {typeof(NodejsEmbeddingTests).FullName}.{nameof(ThrowJSError)}\([^)]*\) \(.*{nameof(NodejsEmbeddingTests)}\.cs:\d+\)
                at {typeof(NodejsEmbeddingTests).FullName}.<>c.<{nameof(ThreadSafeFunctionException)}>.*\([^)]*\) \(.*{nameof(NodejsEmbeddingTests)}\.cs:\d+\)
            """.Replace("\r", "");
        Assert.Matches(stackRegex, unhandledErrorStack);

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [Fact]
    public void ErrorPropagation()
    {
        // This is similar to DotnetToJSException() test case above, but tests propagation of the exception outside
        // the JS runtime thread. When that happens the exception is wrapped by an outer JSException.

        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

        JSException exception = Assert.Throws<JSException>(() =>
        {
            nodejs.Run(() => ThrowJSError("test"));
        });

        Assert.Equal("Exception thrown from JS thread: test", exception.Message);
        Assert.IsType<JSException>(exception.InnerException);

        exception = (JSException)exception.InnerException;
        Assert.Equal("test", exception.Message);

        var stack = GetNormalizedStackTrace(exception);
        var stackRegex = $"""
                ^throwJSError in <anonymous>:line 1
                {typeof(NodejsEmbeddingTests).FullName}.{nameof(ThrowJSError)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                {typeof(NodejsEmbeddingTests).FullName}.<>c.<{nameof(ErrorPropagation)}>.*\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                """.Replace("\r", "");
        Assert.Matches(stackRegex, stack);
    }

    [Fact]
    public async Task AsyncErrorPropagation()
    {
        // This is similar to DotnetToJSAsyncException() test case above, but tests propagation of the exception outside
        // the JS runtime thread. When that happens the exception is wrapped by an outer JSException.

        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

        JSException exception = await Assert.ThrowsAsync<JSException>(async () =>
        {
            await nodejs.RunAsync(async () => await ThrowJSAsyncError("test"));
        });

        Assert.Equal("Exception thrown from JS thread: test", exception.Message);
        Assert.IsType<JSException>(exception.InnerException);

        exception = (JSException)exception.InnerException;
        Assert.Equal("test", exception.Message);

        var stack = GetNormalizedStackTrace(exception);
        var stackRegex = $"""
                ^throwJSAsyncError in <anonymous>:line 1
                {typeof(NodejsEmbeddingTests).FullName}.{nameof(ThrowJSAsyncError)}\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                {typeof(NodejsEmbeddingTests).FullName}.<>c.<<{nameof(AsyncErrorPropagation)}>.*\([^)]*\) in .*{nameof(NodejsEmbeddingTests)}\.cs:line \d+
                """.Replace("\r", "");
        Assert.Matches(stackRegex, stack);
    }

    [Fact]
    public void ImportBuiltinModule()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

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

    [Fact]
    public void ImportCommonJSModule()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

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

    [Fact]
    public void ImportCommonJSPackage()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

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

    [Fact]
    public async Task ImportESModule()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

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

    [Fact]
    public async Task ImportESPackage()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

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

    [Fact]
    public void UnhandledRejection()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

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

    [Fact]
    public async Task WorkerIsMainThread()
    {
        await TestWorker(
            mainPrepare: () =>
            {
                Assert.True(NodeWorker.IsMainThread);
                return new NodeWorker.Options { Eval = true };
            },
            workerScript: """
                const assert = require('node:assert');
                const { isMainThread } = require('node:worker_threads');
                assert(!isMainThread);
                """,
            mainRun: (worker) => Task.CompletedTask);
    }

    [Fact]
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
            workerScript: """
                const assert = require('node:assert');
                const process = require('node:process');
                const { workerData } = require('node:worker_threads');
                assert.deepStrictEqual(process.argv.slice(2), ['test1', 'test2']);
                assert.strictEqual(typeof workerData, 'boolean');
                assert(workerData);
                """,
            mainRun: (worker) => Task.CompletedTask);
    }

    [Fact]
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
            workerScript: """
                const assert = require('node:assert');
                const { getEnvironmentData } = require('node:worker_threads');
                assert.strictEqual(getEnvironmentData('test'), true);
                """,
            mainRun: (worker) => Task.CompletedTask);
    }

    [Fact]
    public async Task WorkerMessages()
    {
        await TestWorker(
            mainPrepare: () =>
            {
                return new NodeWorker.Options { Eval = true };
            },
            workerScript: """
                const { parentPort } = require('node:worker_threads');
                parentPort.on('message', (msg) => parentPort.postMessage(msg)); // echo
                """,
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

    [Fact]
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
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();
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
    [Fact]
    public void MarshalClass()
    {
        using NodeEmbeddingThreadRuntime nodejs = CreateNodeEmbeddingThreadRuntime();

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
