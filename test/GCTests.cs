// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

public class GCTests
{

    [Fact]
    public void GCHandles()
    {
        using NodeEmbeddingThreadRuntime nodejs =
            NodejsEmbeddingTests.CreateNodeEmbeddingThreadRuntime();

        nodejs.Run(() =>
        {
            // 3 GC handles are created in the NodeEmbeddingThreadRuntime constructor
            // to define the 'require', 'resolve', and ' import' functions.
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
            Assert.Equal(3 + 5 + 2, JSRuntimeContext.Current.GCHandleCount);
        });

        // JS GC is asynchronous, so pump a bounded number of cycles until the two temporary
        // handles are released rather than asserting after a single GC.
        long handleCount = 0;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            nodejs.GC();
            nodejs.Run(() => { handleCount = JSRuntimeContext.Current.GCHandleCount; });
            if (handleCount == 3 + 5)
            {
                break;
            }
        }

        // After GC, the handle count should have reverted back to the original set.
        Assert.Equal(3 + 5, handleCount);
    }

    [Fact]
    public void GCObjects()
    {
        using NodeEmbeddingThreadRuntime nodejs =
            NodejsEmbeddingTests.CreateNodeEmbeddingThreadRuntime();

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

        // Releasing the .NET object takes more than one GC pass (the first finalizes the JS
        // wrapper and frees its handle; a later one collects the object) and GC is async across
        // both runtimes, so pump a bounded number of cycles instead of asserting after one pass.
        for (int attempt = 0; DotnetClass.Instances != 0 && attempt < 20; attempt++)
        {
            nodejs.GC();
            nodejs.Run(() => { });
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        // The finalizer should have run, decrementing the instance count.
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
