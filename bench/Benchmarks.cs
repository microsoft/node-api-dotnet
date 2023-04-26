// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.JavaScript.NodeApi.Runtimes;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Bench;

public abstract class Benchmarks
{
    public static void Main(string[] args)
    {
        // Example: dotnet run -c Release --filter aot
        // If no filter is specified, the switcher will prompt.
        BenchmarkSwitcher.FromAssembly(typeof(Benchmarks).Assembly).Run(args);
    }

    public class NonAot : Benchmarks
    {
        // Non-AOT-only benchmarks may go here
    }

    [SimpleJob(RuntimeMoniker.NativeAot70)]
    public class Aot : Benchmarks
    {
        // AOT-only benchmarks may go here
    }

    private static string LibnodePath { get; } = Path.Combine(
        GetRepoRootDirectory(),
        "bin",
        "win-x64", // TODO
        "libnode" + GetSharedLibraryExtension());

    private napi_env _env;
    private JSValue _function;
    private JSValue _callback;

    [GlobalSetup]
    public void Setup()
    {
        NodejsPlatform platform = new(LibnodePath);

        // This setup avoids using NodejsEnvironment so benchmarks can run on the same thread.
        // NodejsEnvironment creates a separate thread that would slow down the micro-benchmarks.
        _env = JSNativeApi.CreateEnvironment(
            (napi_platform)platform, (error) => Console.WriteLine(error), null);

        // The new scope instance saves itself as the thread-local JSValueScope.Current.
        JSValueScope scope = new(JSValueScopeType.Root, _env);

        // Create some JS values that will be used by the benchmarks.
        _function = JSNativeApi.RunScript("function callMeBack(cb) { cb(); }; callMeBack");
        _callback = JSValue.CreateFunction("callback", (args) => JSValue.Undefined);
    }

    [Benchmark]
    public void CallJS()
    {
        _function.Call(thisArg: default, _callback);
    }
}

