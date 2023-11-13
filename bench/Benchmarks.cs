// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
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

    [ThreadStatic]
    private static JSRuntime s_currentRuntime = null!;
    private JSRuntime _jsRuntime = null!;
    private napi_env _env;

    private JSFunction _jsFunction;
    private JSFunction _jsFunctionWithArgs;
    private JSFunction _jsFunctionWithCallback;
    private JSObject _jsInstance;
    private JSFunction _dotnetFunction;
    private JSFunction _dotnetFunctionWithArgs;
    private JSObject _dotnetClass;
    private JSFunction _dotnetClassConstructor;
    private JSObject _dotnetInstance;
    private JSValue _jsMethodName;
    private JSValue _jsArg1;
    private JSValue _jsArg2;
    private JSValue _jsArg3;
    private JSReference _reference = null!;

    private napi_value _jsUndefinedHandle;
    private napi_value _jsFunctionHandle;
    private napi_value _jsFunctionWithCallbackHandle;
    private napi_value _jsInstanceHandle;
    private napi_value _dotnetFunctionHandle;
    private napi_value _dotnetFunctionWithArgsHandle;
    private napi_value _dotnetClassConstructorHandle;
    private napi_value _jsMethodNameHandle;
    private napi_value _jsArg1Handle;
    private napi_value _jsArg2Handle;
    private napi_value _jsArg3Handle;
    private napi_ref _jsReferenceHandle;

    /// <summary>
    /// Simple class that is exported to JS and used in some benchmarks.
    /// </summary>
    private class DotnetClass
    {
        public DotnetClass() { }

        public string Property { get; set; } = string.Empty;

#pragma warning disable CA1822 // Method does not access instance data and can be marked as static
#pragma warning disable IDE0060 // Unused parameter
        public static void Method() { }
        public static void MethodWithArgs(string arg1, string arg2)
        {
        }
#pragma warning restore IDE0060
#pragma warning restore CA1822
    }

    /// <summary>
    /// Setup shared by both CLR and AOT benchmarks.
    /// </summary>
    protected unsafe void Setup()
    {
        NodejsPlatform platform = new(LibnodePath/*, args: new[] { "node", "--expose-gc" }*/);
        _jsRuntime = platform.Runtime;
        s_currentRuntime = _jsRuntime;

        // This setup avoids using NodejsEnvironment so benchmarks can run on the same thread.
        // NodejsEnvironment creates a separate thread that would slow down the micro-benchmarks.
        platform.Runtime.CreateEnvironment(platform, Console.WriteLine, null, out _env)
            .ThrowIfFailed();

        // The new scope instance saves itself as the thread-local JSValueScope.Current.
        JSValueScope scope = new(JSValueScopeType.Root, _env, platform.Runtime);

        // Create some JS values that will be used by the benchmarks.

        _jsFunction = (JSFunction)JSNativeApi.RunScript("function jsFunction() { }; jsFunction");
        _jsFunctionWithArgs = (JSFunction)JSNativeApi.RunScript(
            "function jsFunctionWithArgs(a, b, c) { }; jsFunctionWithArgs");
        _jsFunctionWithCallback = (JSFunction)JSNativeApi.RunScript(
            "function jsFunctionWithCallback(cb, ...args) { cb(...args); }; " +
            "jsFunctionWithCallback");
        _jsInstance = (JSObject)JSNativeApi.RunScript(
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
        _dotnetClassConstructor = (JSFunction)(JSValue)_dotnetClass;

        _dotnetInstance = (JSObject)JSNativeApi.CallAsConstructor(_dotnetClass);

        // Allocating string values is somewhat expensive relative to other operations.
        // So they are allocated once and reused by multiple benchmark runs.
        // There are separate benchmarks targeting string allocation specifically.
        _jsMethodName = "method";
        _jsArg1 = "1";
        _jsArg2 = "2";
        _jsArg3 = "3";

        _reference = new JSReference(_jsFunction);

        // Get the handles for the JS values that will be used by the baseline benchmarks.
        // Also create separate callback functions that use only the low-level handle APIs.

        _jsUndefinedHandle = JSValue.Undefined.Handle;
        _jsFunctionHandle = ((JSValue)_jsFunction).Handle;
        _jsFunctionWithCallbackHandle = ((JSValue)_jsFunctionWithCallback).Handle;
        _jsInstanceHandle = ((JSValue)_jsInstance).Handle;
        _jsRuntime.CreateFunction(
            _env,
            name: null,
            new napi_callback(s_handleFunctionCallback),
            data: default,
            out _dotnetFunctionHandle).ThrowIfFailed();
        _jsRuntime.CreateFunction(
            _env,
            name: null,
            new napi_callback(s_handleFunctionWithArgsCallback),
            data: default,
            out _dotnetFunctionWithArgsHandle).ThrowIfFailed();

        ReadOnlySpan<napi_property_descriptor> properties =
            ReadOnlySpan<napi_property_descriptor>.Empty;
        _jsRuntime.DefineClass(
            _env,
            nameof(DotnetClass) + "2",
            new napi_callback(s_handleConstructorCallback),
            data: default,
            properties,
            out _dotnetClassConstructorHandle).ThrowIfFailed();

        _jsMethodNameHandle = _jsMethodName.Handle;
        _jsArg1Handle = _jsArg1.Handle;
        _jsArg2Handle = _jsArg2.Handle;
        _jsArg3Handle = _jsArg3.Handle;
        _jsReferenceHandle = _reference.Handle;
    }

    // Benchmarks in the base class run in both CLR and AOT environments.

    [BenchmarkCategory(nameof(CallJSFunction)), Benchmark(Baseline = true)]
    public void CallJSFunctionHandle()
    {
        _jsRuntime.CallFunction(
            _env, recv: _jsUndefinedHandle, _jsFunctionHandle, ReadOnlySpan<napi_value>.Empty, out _)
            .ThrowIfFailed();
    }

    [BenchmarkCategory(nameof(CallJSFunction)), Benchmark(Baseline = false)]
    public void CallJSFunction()
    {
        _jsFunction.CallAsStatic();
    }

    [BenchmarkCategory(nameof(CallJSFunction)), Benchmark]
    public unsafe void CallJSFunctionWithArgsHandle()
    {
        ReadOnlySpan<napi_value> args = stackalloc napi_value[]
        {
            _jsArg1Handle,
            _jsArg2Handle,
            _jsArg3Handle,
        };
        _jsRuntime.CallFunction(
            _env, recv: _jsUndefinedHandle, _jsFunctionHandle, args, out _)
            .ThrowIfFailed();
    }

    [BenchmarkCategory(nameof(CallJSFunction)), Benchmark]
    public void CallJSFunctionWithArgs()
    {
        _jsFunctionWithArgs.CallAsStatic(_jsArg1, _jsArg2, _jsArg3);
    }

    [BenchmarkCategory(nameof(CallJSMethod)), Benchmark(Baseline = true)]
    public void CallJSMethodHandle()
    {
        _jsRuntime.GetProperty(_env, _jsInstanceHandle, _jsMethodNameHandle, out napi_value method)
            .ThrowIfFailed();
        ReadOnlySpan<napi_value> emptyArgs = ReadOnlySpan<napi_value>.Empty;
        _jsRuntime.CallFunction(_env, _jsInstanceHandle, method, emptyArgs, out _)
            .ThrowIfFailed();
    }

    [BenchmarkCategory(nameof(CallJSMethod)), Benchmark]
    public void CallJSMethod()
    {
        _jsInstance.CallMethod(_jsMethodName);
    }

    [BenchmarkCategory(nameof(CallJSMethod)), Benchmark]
    public void CallJSMethodWithArgsHandle()
    {
        _jsRuntime.GetProperty(_env, _jsInstanceHandle, _jsMethodNameHandle, out napi_value method)
            .ThrowIfFailed();
        ReadOnlySpan<napi_value> args = stackalloc napi_value[]
        {
            _jsArg1Handle,
            _jsArg2Handle,
            _jsArg3Handle,
        };
        _jsRuntime.CallFunction(_env, _jsInstanceHandle, method, args, out _)
            .ThrowIfFailed();
    }

    [BenchmarkCategory(nameof(CallJSMethod)), Benchmark]
    public void CallJSMethodWithArgs()
    {
        _jsInstance.CallMethod(_jsMethodName, _jsArg1, _jsArg2, _jsArg3);
    }

    [BenchmarkCategory(nameof(CallDotnetFunction)), Benchmark(Baseline = true)]
    public unsafe void CallDotnetFunctionHandle()
    {
        ReadOnlySpan<napi_value> args = stackalloc napi_value[] { _dotnetFunctionHandle };
        _jsRuntime.CallFunction(
            _env, recv: _jsUndefinedHandle, _jsFunctionWithCallbackHandle, args, out _)
            .ThrowIfFailed();
    }

    [BenchmarkCategory(nameof(CallDotnetFunction)), Benchmark]
    public void CallDotnetFunction()
    {
        _jsFunctionWithCallback.CallAsStatic(_dotnetFunction);
    }

    [BenchmarkCategory(nameof(CallDotnetFunction)), Benchmark]
    public void CallDotnetFunctionWithArgsHandle()
    {
        ReadOnlySpan<napi_value> args = stackalloc napi_value[]
        {
            _dotnetFunctionWithArgsHandle,
            _jsArg1Handle,
            _jsArg2Handle,
            _jsArg3Handle,
        };
        _jsRuntime.CallFunction(
            _env, recv: _jsUndefinedHandle, _jsFunctionWithCallbackHandle, args, out _)
            .ThrowIfFailed();
    }

    [BenchmarkCategory(nameof(CallDotnetFunction)), Benchmark]
    public void CallDotnetFunctionWithArgs()
    {
        _jsFunctionWithCallback.CallAsStatic(_dotnetFunctionWithArgs, _jsArg1, _jsArg2);
    }

    [WarmupCount(1), IterationCount(5)]
    [BenchmarkCategory(nameof(CallDotnetConstructor)), Benchmark(Baseline = false)]
    public void CallDotnetConstructor()
    {
        _dotnetClassConstructor.CallAsConstructor();
    }

    [WarmupCount(1), IterationCount(5)]
    [BenchmarkCategory(nameof(CallDotnetConstructor)), Benchmark(Baseline = true)]
    public void CallDotnetConstructorHandle()
    {
        ReadOnlySpan<napi_value> emptyArgs = ReadOnlySpan<napi_value>.Empty;
        _jsRuntime.NewInstance(_env, _dotnetClassConstructorHandle, emptyArgs, out _)
            .ThrowIfFailed();
    }

    [BenchmarkCategory(nameof(CallDotnetMethod)), Benchmark(Baseline = true)]
    public void CallDotnetMethod()
    {
        _dotnetInstance.CallMethod(_jsMethodName);
    }

    [BenchmarkCategory(nameof(CallDotnetMethod)), Benchmark]
    public void CallDotnetMethodWithArgs()
    {
        _dotnetInstance.CallMethod(_jsMethodName, _jsArg1, _jsArg2, _jsArg3);
    }

    [BenchmarkCategory(nameof(ReferenceGet)), Benchmark(Baseline = true)]
    public void ReferenceGetHandle()
    {
        _jsRuntime.GetReferenceValue(_env, _jsReferenceHandle, out _)
            .ThrowIfFailed();
    }

    [BenchmarkCategory(nameof(ReferenceGet)), Benchmark]
    public void ReferenceGet()
    {
        _ = _reference.GetValue()!.Value;
    }

    [BenchmarkCategory(nameof(ReferenceCreateAndDipose)), Benchmark(Baseline = true)]
    public void ReferenceCreateAndDiposeHandle()
    {
        _jsRuntime.CreateReference(
                  _env,
                  _jsFunctionHandle,
                  1,
                  out napi_ref reference).ThrowIfFailed(reference);
        _jsRuntime.DeleteReference(_env, reference).ThrowIfFailed();
    }

    [BenchmarkCategory(nameof(ReferenceCreateAndDipose)), Benchmark]
    public void ReferenceCreateAndDipose()
    {
        using JSReference reference = new(_jsFunction);
    }

    [BenchmarkCategory(nameof(CreateJSString)), Benchmark(Baseline = true)]
    public unsafe void CreateJSStringHandle()
    {
        _jsRuntime.CreateString(_env, "test".AsSpan(), out napi_value value)
            .ThrowIfFailed(value);
    }

    [BenchmarkCategory(nameof(CreateJSString)), Benchmark]
    public void CreateJSString()
    {
        _ = JSValue.CreateStringUtf16("test");
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
            _jsFunctionCallMethodDynamic = (JSFunction)JSNativeApi.RunScript(
                "function jsFunctionCallMethodDynamic(dotnet) " +
                "{ dotnet.System.Object.ReferenceEquals(null, null); }; " +
                "jsFunctionCallMethodDynamic");

            // Implement IFormatProvider in JS and pass it to a .NET method.
            _jsFunctionCallMethodDynamicInterface = (JSFunction)JSNativeApi.RunScript(
                "function jsFunctionCallMethodDynamicInterface(dotnet)  {" +
                "    const formatProvider = { GetFormat: (type) => null };" +
                "    dotnet.System.String.Format(formatProvider, '', null, null);" +
                "}; " +
                "jsFunctionCallMethodDynamicInterface");
        }

        // CLR-only (non-AOT) benchmarks

        [BenchmarkCategory(nameof(DynamicCallDotnetMethod)), Benchmark(Baseline = true)]
        public void DynamicCallDotnetMethod()
        {
            _jsFunctionCallMethodDynamic.CallAsStatic(_jsHost);
        }

        [BenchmarkCategory(nameof(DynamicCallDotnetMethod)), Benchmark]
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

#if NETFRAMEWORK
    private static readonly napi_finalize.Delegate s_finalizeGCHandle = FinalizeGCHandle;
    private static readonly napi_callback.Delegate s_handleConstructorCallback = HandleConstructorCallback;
    private static readonly napi_callback.Delegate s_handleFunctionCallback = HandleFunctionCallback;
    private static readonly napi_callback.Delegate s_handleFunctionWithArgsCallback = HandleFunctionWithArgsCallback;
#else
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_handleConstructorCallback = &HandleConstructorCallback;
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_handleFunctionCallback = &HandleFunctionCallback;
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, napi_callback_info, napi_value> s_handleFunctionWithArgsCallback = &HandleFunctionWithArgsCallback;
    private static readonly unsafe delegate* unmanaged[Cdecl]
        <napi_env, nint, nint, void> s_finalizeGCHandle = &FinalizeGCHandle;
#endif

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value HandleConstructorCallback(napi_env env, napi_callback_info cbinfo)
    {
        var jsRuntime = s_currentRuntime;
        jsRuntime.GetCallbackInfo(env, cbinfo, out int argc, out nint data);
        var dotnetInstance = new DotnetClass();
        GCHandle dotnetInstanceGCHandle = GCHandle.Alloc(dotnetInstance);
        jsRuntime.CreateObject(env, out napi_value jsInstance).ThrowIfFailed();
        jsRuntime.Wrap(
            env,
            jsInstance,
            (nint)dotnetInstanceGCHandle,
            new napi_finalize(s_finalizeGCHandle),
            finalize_hint: default,
            out napi_ref _).ThrowIfFailed();
        return jsInstance;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value HandleFunctionCallback(napi_env env, napi_callback_info cbinfo)
    {
        var jsRuntime = s_currentRuntime;
        jsRuntime.GetUndefined(env, out napi_value jsUndefined);
        return jsUndefined;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe napi_value HandleFunctionWithArgsCallback(napi_env env, napi_callback_info cbinfo)
    {
        var jsRuntime = s_currentRuntime;
        jsRuntime.GetCallbackInfo(env, cbinfo, out int argc, out nint data).ThrowIfFailed();
        Span<napi_value> args = stackalloc napi_value[argc];
        jsRuntime.GetCallbackArgs(env, cbinfo, args, out napi_value thisArg).ThrowIfFailed();
        jsRuntime.GetUndefined(env, out napi_value jsUndefined).ThrowIfFailed();
        return jsUndefined;
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static unsafe void FinalizeGCHandle(napi_env env, nint data, nint hint)
    {
        GCHandle handle = GCHandle.FromIntPtr(data);
        handle.Free();
    }
}
