// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Test.TestUtils;

namespace Microsoft.JavaScript.NodeApi.Test;

public class GCTests
{
    private static string LibnodePath { get; } = GetLibnodePath();

    [SkippableFact]
    public void GCTest()
    {
        Skip.If(
            NodejsEmbeddingTests.NodejsPlatform == null,
            "Node shared library not found at " + LibnodePath);
        using NodejsEnvironment nodejs = NodejsEmbeddingTests.NodejsPlatform.CreateEnvironment();

        long gcHandleCount = 0;
        nodejs.SynchronizationContext.Run(() =>
        {
            Assert.Equal(0, JSRuntimeContext.Current.GCHandleCount);

            JSClassBuilder<DotnetClass> classBuilder =
                new(nameof(DotnetClass), () => new DotnetClass());
            classBuilder.AddProperty(
                "property",
                (x) => x.Property,
                (x, value) => x.Property = (string)value);
            classBuilder.AddMethod("method", (x) => (args) => DotnetClass.Method());
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
            Assert.Equal(5, JSRuntimeContext.Current.GCHandleCount);

            using JSValueScope innerScope = new(JSValueScopeType.Callback);
            jsCreateInstanceFunction.CallAsStatic(dotnetClass);

            gcHandleCount = JSRuntimeContext.Current.GCHandleCount;
        });

        // Some GC handles should have been allocated by the JS create-instance function call.
        Assert.True(gcHandleCount > 5);

        nodejs.GC();

        // After GC, the handle count should have reverted back to the original set.
        gcHandleCount = nodejs.SynchronizationContext.Run(
                () => JSRuntimeContext.Current.GCHandleCount);
        Assert.Equal(5, gcHandleCount);
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
        public static void Method() { }
#pragma warning restore CA1822

        ~DotnetClass()
        {
            --Instances;
        }
    }
}
