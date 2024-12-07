// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.JavaScript.NodeApi.DotNetHost;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Bench;

/// <summary>
/// Micro-benchmarks for various .NET + JS interop operations.
/// </summary>
/// <remarks>
/// These benchmarks run both .NET and Node.js code, and call between them. The benchmark
/// runner manages the GC for the .NET runtime, but it doesn't know anything about the JS runtime.
/// To avoid heavy JS GC pressure from millions of operations (which may each allocate objects),
/// these benchmarks use the `ShortRunJob` attribute (which sacrifices some precision but also
/// doesn't take as long to run).
/// </remarks>
[IterationCount(5)]
[WarmupCount(1)]
public abstract class Benchmarks
{
    public static void Main(string[] args)
    {
#if DEBUG
        IConfig config = new DebugBuildConfig();
#else
        IConfig config = DefaultConfig.Instance;
#endif

        // Example: dotnet run -c Release --filter clr
        // If no filter is specified, the switcher will prompt.
        BenchmarkSwitcher.FromAssembly(typeof(Benchmarks).Assembly).Run(args,
            ManualConfig.Create(config)
            .WithOptions(ConfigOptions.JoinSummary));
    }

    private static string LibnodePath { get; } = Path.Combine(
        GetRepoRootDirectory(),
        "bin",
        GetCurrentPlatformRuntimeIdentifier(),
        "libnode" + GetSharedLibraryExtension());

    private NodejsEmbeddingRuntime? _runtime;
    private NodejsEmbeddingNodeApiScope? _nodeApiScope;
    private JSValue _jsString;
    private JSFunction _jsFunction;
    private JSFunction _jsFunctionWithArgs;
    private JSFunction _jsFunctionWithCallback;
    private JSObject _jsInstance;
    private JSFunction _dotnetFunction;
    private JSFunction _dotnetFunctionWithArgs;
    private JSObject _dotnetClass;
    private JSObject _dotnetInstance;
    private JSFunction _jsFunctionCreateInstance;
    private JSFunction _jsFunctionCallMethod;
    private JSFunction _jsFunctionCallMethodWithArgs;
    private JSReference _reference = null!;
    private static readonly string[] s_settings = new[] { "node", "--expose-gc" };

    /// <summary>
    /// Simple class that is exported to JS and used in some benchmarks.
    /// </summary>
    private class DotnetClass
    {
        public DotnetClass() { }

        public string Property { get; set; } = string.Empty;

#pragma warning disable CA1822 // Method does not access instance data and can be marked as static
        public static void Method() { }
#pragma warning restore CA1822
    }

    /// <summary>
    /// Setup shared by both CLR and AOT benchmarks.
    /// </summary>
    protected void Setup()
    {
        NodejsEmbeddingPlatform platform = new(
            LibnodePath,
            new NodejsEmbeddingPlatformSettings { Args = s_settings });

        // This setup avoids using NodejsEmbeddingThreadRuntime so benchmarks can run on
        // the same thread. NodejsEmbeddingThreadRuntime creates a separate thread that would slow
        // down the micro-benchmarks.
        _runtime = new(platform);
        // The nodeApiScope creates JSValueScope instance that saves itself as
        // the thread-local JSValueScope.Current.
        _nodeApiScope = new(_runtime);

        // Create some JS values that will be used by the benchmarks.

        _jsString = JSValue.RunScript("'Hello Node-API .Net!'");
        _jsFunction = (JSFunction)JSValue.RunScript("function jsFunction() { }; jsFunction");
        _jsFunctionWithArgs = (JSFunction)JSValue.RunScript(
            "function jsFunctionWithArgs(a, b, c) { }; jsFunctionWithArgs");
        _jsFunctionWithCallback = (JSFunction)JSValue.RunScript(
            "function jsFunctionWithCallback(cb, ...args) { cb(...args); }; " +
            "jsFunctionWithCallback");
        _jsInstance = (JSObject)JSValue.RunScript(
            "const jsInstance = { method: (...args) => {} }; jsInstance");

        _dotnetFunction = (JSFunction)JSValue.CreateFunction(
            "dotnetFunction", (args) => JSValue.Undefined);
        _dotnetFunctionWithArgs = (JSFunction)JSValue.CreateFunction(
            "dotnetFunctionWithArgs", (args) =>
            {
                for (int i = 0; i < args.Length; i++)
                {
                    _ = args[i];
                }

                return JSValue.Undefined;
            });

        var classBuilder = new JSClassBuilder<DotnetClass>(
            nameof(DotnetClass), () => new DotnetClass());
        classBuilder.AddProperty(
            "property",
            (x) => x.Property,
            (x, value) => x.Property = (string)value);
        classBuilder.AddMethod("method", (x) => (args) => DotnetClass.Method());
        _dotnetClass = (JSObject)classBuilder.DefineClass();
        _dotnetInstance = (JSObject)((JSValue)_dotnetClass).CallAsConstructor();

        _jsFunctionCreateInstance = (JSFunction)JSValue.RunScript(
            "function jsFunctionCreateInstance(Class) { new Class() }; " +
            "jsFunctionCreateInstance");
        _jsFunctionCallMethod = (JSFunction)JSValue.RunScript(
            "function jsFunctionCallMethod(instance) { instance.method(); }; " +
            "jsFunctionCallMethod");
        _jsFunctionCallMethodWithArgs = (JSFunction)JSValue.RunScript(
            "function jsFunctionCallMethodWithArgs(instance, ...args) " +
            "{ instance.method(...args); }; " +
            "jsFunctionCallMethodWithArgs");

        _reference = new JSReference(_jsFunction);
    }

    private static JSValueScope NewJSScope() => new(JSValueScopeType.Callback);

    // Benchmarks in the base class run in both CLR and AOT environments.

    [Benchmark]
    public void JSValueToString()
    {
        _jsString.GetValueStringUtf16();
    }

    [Benchmark]
    public void JSValueToStringAsCharArray()
    {
        _ = new string(_jsString.GetValueStringUtf16AsCharArray());
    }

    [Benchmark]
    public void CallJSFunction()
    {
        _jsFunction.CallAsStatic();
    }

    [Benchmark]
    public void CallJSFunctionWithArgs()
    {
        _jsFunctionWithArgs.CallAsStatic("1", "2", "3");
    }

    [Benchmark]
    public void CallJSMethod()
    {
        _jsInstance.CallMethod("method");
    }

    [Benchmark]
    public void CallJSMethodWithArgs()
    {
        _jsInstance.CallMethod("method", "1", "2", "3");
    }

    [Benchmark]
    public void CallDotnetFunction()
    {
        _jsFunctionWithCallback.CallAsStatic(_dotnetFunction);
    }

    [Benchmark]
    public void CallDotnetFunctionWithArgs()
    {
        _jsFunctionWithCallback.CallAsStatic(_dotnetFunctionWithArgs, "1", "2", "3");
    }

    [Benchmark]
    public void CallDotnetConstructor()
    {
        _jsFunctionCreateInstance.CallAsStatic(_dotnetClass);
    }

    [Benchmark]
    public void CallDotnetMethod()
    {
        _jsFunctionCallMethod.CallAsStatic(_dotnetInstance);
    }

    [Benchmark]
    public void CallDotnetMethodWithArgs()
    {
        _jsFunctionCallMethodWithArgs.CallAsStatic(_dotnetInstance, "1", "2", "3");
    }

    [Benchmark]
    public void ReferenceGet()
    {
        _ = _reference.GetValue();
    }

    [Benchmark]
    public void ReferenceCreateAndDipose()
    {
        using JSReference reference = new(_jsFunction);
    }

    [ShortRunJob]
    [MemoryDiagnoser(displayGenColumns: false)]
    public class Clr : Benchmarks
    {
        private JSObject _jsHost;
        private JSFunction _jsFunctionCallMethodDynamic;
        private JSFunction _jsFunctionCallMethodDynamicInterface;

        [GlobalSetup]
        public new void Setup()
        {
            base.Setup();

            // CLR-only (non-AOT) setup

            JSObject hostModule = new();
            _ = new ManagedHost(hostModule);
            _jsHost = hostModule;
            _jsFunctionCallMethodDynamic = (JSFunction)JSValue.RunScript(
                "function jsFunctionCallMethodDynamic(dotnet) " +
                "{ dotnet.System.Object.ReferenceEquals(null, null); }; " +
                "jsFunctionCallMethodDynamic");

            // Implement IFormatProvider in JS and pass it to a .NET method.
            _jsFunctionCallMethodDynamicInterface = (JSFunction)JSValue.RunScript(
                "function jsFunctionCallMethodDynamicInterface(dotnet)  {" +
                "    const formatProvider = { GetFormat: (type) => null };" +
                "    dotnet.System.String.Format(formatProvider, '', null, null);" +
                "}; " +
                "jsFunctionCallMethodDynamicInterface");
        }

        // CLR-only (non-AOT) benchmarks

        [Benchmark]
        public void DynamicCallDotnetMethod()
        {
            _jsFunctionCallMethodDynamic.CallAsStatic(_jsHost);
        }

        [Benchmark]
        public void DynamicCallDotnetMethodWithInterface()
        {
            _jsFunctionCallMethodDynamicInterface.CallAsStatic(_jsHost);
        }
    }

    [ShortRunJob(RuntimeMoniker.NativeAot80)]
    public class Aot : Benchmarks
    {
        [GlobalSetup]
        public new void Setup()
        {
            base.Setup();
        }

        // AOT-only benchmarks
    }
}
