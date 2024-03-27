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
    public void StartEnvironment()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

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
        // Create and destory a Node.js environment twice, using the same platform instance.
        StartEnvironment();
        StartEnvironment();
    }

    public interface IConsole { void Log(string message); }

    [SkippableFact]
    public void CallFunction()
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
    public void ImportBuiltinModule()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

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
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

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
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

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
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

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
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

        await nodejs.RunAsync(async () =>
        {
            JSValue testModule = await nodejs.ImportAsync(
                "./test-esm-package", null, esModule: true);
            Assert.Equal(JSValueType.Object, testModule.TypeOf());
            Assert.Equal(JSValueType.Function, testModule["test"].TypeOf());
            Assert.Equal("test", testModule.CallMethod("test"));

            // Check that module resolution handles sub-paths from conditional exports.
            // https://nodejs.org/api/packages.html#conditional-exports
            JSValue testModuleFeature = await nodejs.ImportAsync(
                "./test-esm-package/feature", null, esModule: true);
            Assert.Equal(JSValueType.Object, testModuleFeature.TypeOf());
            Assert.Equal(JSValueType.Function, testModuleFeature["test2"].TypeOf());
            Assert.Equal("test2", testModuleFeature.CallMethod("test2"));
        });

        nodejs.Dispose();
        Assert.Equal(0, nodejs.ExitCode);
    }

    [SkippableFact]
    public void UnhandledRejection()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

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
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

        JSException exception = Assert.Throws<JSException>(() =>
        {
            nodejs.Run(() =>
            {
                JSValue.RunScript(
                    "function throwError() { throw new Error('test'); }\n" +
                    "throwError();");
            });
        });

        Assert.StartsWith("Exception thrown from JS thread.", exception.Message);
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

    /// <summary>
    /// Tests the functionality of dynamically exporting and marshalling a class type from .NET
    /// to JS (as opposed to relying on [JSExport] (compile-time code-generation) for marshalling.
    /// </summary>
    [SkippableFact]
    public void MarshalClass()
    {
        using NodejsEnvironment nodejs = CreateNodejsEnvironment();

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
