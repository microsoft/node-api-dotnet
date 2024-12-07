// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

public class GCTests
{
    private static string LibnodePath { get; } = GetLibnodePath();

    [SkippableFact]
    public void GCHandles()
    {
        Skip.If(
            NodejsEmbeddingTests.NodejsPlatform == null,
            "Node shared library not found at " + LibnodePath);
        using NodejsEmbeddingThreadRuntime nodejs = NodejsEmbeddingTests.CreateNodejsEnvironment();

        nodejs.Run(() =>
        {
            Assert.Equal(3, JSRuntimeContext.Current.GCHandleCount);

            JSClassBuilder<DotnetClass> classBuilder =
                new(nameof(DotnetClass), () => new DotnetClass());
            classBuilder.AddProperty(
                "property",
                (x) => x.Property,
                (x, value) => x.Property = (string)value);
            classBuilder.AddMethod("method", (x) => (args) => x.Method());
            JSObject dotnetClass = (JSObject)classBuilder.DefineClass();

            JSFunction jsCreateInstanceFunction = (JSFunction)JSValue.RunScript(
                "function jsCreateInstanceFunction(Class) { new Class() }; " +
                "jsCreateInstanceFunction");

            // 5 GC handles are expected
            // - Type: DotnetClass
            // - JSCallback: DotnetClass.constructor
            // - JSPropertyDescriptor: DotnetClass.property
            // - JSPropertyDescriptor: DotnetClass.method
            // - JSPropertyDescriptor: DotnetClass.toString
            Assert.Equal(3 + 5, JSRuntimeContext.Current.GCHandleCount);

            using JSValueScope innerScope = new(JSValueScopeType.Callback);
            jsCreateInstanceFunction.CallAsStatic(dotnetClass);

            // Two more handles should have been allocated by the JS create-instance function call.
            // - One for the 'external' type value passed to the constructor.
            // - One for the JS object wrapper.
            Assert.Equal(3 + 7, JSRuntimeContext.Current.GCHandleCount);
        });

        nodejs.GC();

        nodejs.Run(() =>
        {
            // After GC, the handle count should have reverted back to the original set.
            Assert.Equal(3 + 5, JSRuntimeContext.Current.GCHandleCount);
        });
    }

    [SkippableFact]
    public void GCObjects()
    {
        Skip.If(
            NodejsEmbeddingTests.NodejsPlatform == null,
            "Node shared library not found at " + LibnodePath);
        using NodejsEmbeddingThreadRuntime nodejs = NodejsEmbeddingTests.CreateNodejsEnvironment();

        nodejs.Run(() =>
        {
            JSClassBuilder<DotnetClass> classBuilder =
                new(nameof(DotnetClass), () => new DotnetClass());
            classBuilder.AddProperty(
                "property",
                (x) => x.Property,
                (x, value) => x.Property = (string)value);
            classBuilder.AddMethod("method", (x) => (args) => x.Method());
            JSObject dotnetClass = (JSObject)classBuilder.DefineClass();

            JSFunction jsCreateInstanceFunction = (JSFunction)JSValue.RunScript(
                "function jsCreateInstanceFunction(Class) { new Class() }; " +
                "jsCreateInstanceFunction");

            Assert.Equal(8, JSRuntimeContext.Current.GCHandleCount);

            using (JSValueScope innerScope = new(JSValueScopeType.Callback))
            {
                jsCreateInstanceFunction.CallAsStatic(dotnetClass);
            }
        });

        // One .NET object instance was created by the JS function.
        Assert.Equal(1ul, DotnetClass.Instances);

        // Request a JS GC, which should release the JS object referencing the .NET object.
        // Pump the Node event loop with an empty Run() callback to complete the GC.
        nodejs.GC();
        nodejs.Run(() => { });

        // The JS object released its reference to the .NET object, but it hasn't been GC'd yet.
        Assert.Equal(1ul, DotnetClass.Instances);

        // Request a .NET GC, and wait for finalizers (which run on another thread after the GC).
        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();

        // Now the .NET object should have been finalized/GC'd, as indicated by the
        // instance count decremented by the finalizer.
        Assert.Equal(0ul, DotnetClass.Instances);
    }

    private class DotnetClass
    {
        public static ulong Instances;

        public DotnetClass()
        {
            ++Instances;
        }

        public string Property { get; set; } = string.Empty;

#pragma warning disable CA1822 // Method does not access instance data and can be marked as static
        public void Method() { }
#pragma warning restore CA1822

        ~DotnetClass()
        {
            --Instances;
        }
    }
}
