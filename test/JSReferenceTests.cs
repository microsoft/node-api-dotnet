// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using Microsoft.JavaScript.NodeApi.Interop;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi.Test;

public class JSReferenceTests
{
    private readonly MockJSRuntime _mockRuntime = new();

    private JSValueScope TestScope()
        => TestScope(new MockJSRuntime.SynchronizationContext());

    private JSValueScope TestScope(JSSynchronizationContext synchronizationContext)
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(env, _mockRuntime, synchronizationContext);
        return JSValueScope.CreateRuntimeScope(env, context);
    }

    private static JSValueScope TestScope(MockJSRuntime runtime)
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, runtime, new MockJSRuntime.SynchronizationContext());
        return JSValueScope.CreateRuntimeScope(env, context);
    }

    [Fact]
    public void GetReferenceFromSameScope()
    {
        using JSValueScope rootScope = TestScope();

        JSValue value = JSValue.CreateObject();
        JSReference reference = new(value);
        Assert.True(reference.GetValue().IsObject());
    }

    [Fact]
    public void GetReferenceFromParentScope()
    {
        using JSValueScope rootScope = TestScope();

        JSReference reference;
        using (JSValueScope handleScope = JSValueScope.CreateHandleScope())
        {
            JSValue value = JSValue.CreateObject();
            reference = new JSReference(value);
        }

        Assert.True(reference.GetValue().IsObject());
    }

    [Fact]
    public void GetReferenceFromDifferentThread()
    {
        using JSValueScope rootScope = TestScope();

        JSValue value = JSValue.CreateObject();
        JSReference reference = new(value);

        // Run in a new thread which will not have any current scope.
        TestUtils.RunInThread(() =>
        {
            Assert.Throws<JSInvalidThreadAccessException>(() => reference.GetValue());
        }).Wait();
    }

    [Fact]
    public void GetReferenceFromDifferentRootScope()
    {
        using JSValueScope rootScope1 = TestScope();

        JSValue value = JSValue.CreateObject();
        JSReference reference = new(value);

        // Run in a new thread and establish another root scope there.
        TestUtils.RunInThread(() =>
        {
            // Separate runtime so rootScope2's env has its own instance data (one context per env).
            using JSValueScope rootScope2 = JSReferenceTests.TestScope(new MockJSRuntime());
            Assert.Throws<JSInvalidThreadAccessException>(() => reference.GetValue());
        }).Wait();
    }

    [Fact]
    public void GetWeakReferenceUnavailable()
    {
        using JSValueScope rootScope = TestScope();

        JSValue value = JSValue.CreateObject();
        var reference = new JSReference(value, isWeak: true);

        _mockRuntime.MockReleaseWeakReferenceValue(reference.Handle);
        Assert.Throws<NullReferenceException>(() => reference.GetValue());
    }

    [Fact]
    public void TryGetWeakReferenceValue()
    {
        using JSValueScope rootScope = TestScope();

        JSValue value = JSValue.CreateObject();
        JSReference reference = new(value);
        Assert.True(reference.TryGetValue(out JSValue result));
        Assert.True(result.IsObject());
    }

    [Fact]
    public void TryGetWeakReferenceUnavailable()
    {
        using JSValueScope rootScope = TestScope();

        JSValue value = JSValue.CreateObject();
        var reference = new JSReference(value, isWeak: true);

        _mockRuntime.MockReleaseWeakReferenceValue(reference.Handle);
        Assert.False(reference.TryGetValue(out _));
    }

    // A reference with a runtime context posts its cleanup to the JS thread instead of deleting it
    // inline. The finalizer must never throw when it runs on a thread with no current scope, and
    // the posted delete must actually release the native reference once the JS thread pumps it.
    [Fact]
    public void FinalizeContextReferenceFromDifferentThreadDoesNotThrow()
    {
        var syncContext = new MockJSRuntime.RecordingSynchronizationContext();
        using JSValueScope rootScope = TestScope(syncContext);

        JSValue value = JSValue.CreateObject();
        var reference = new FinalizerTestReference(value);
        napi_ref handle = reference.Handle;
        Assert.True(_mockRuntime.HasReference(handle));

        TestUtils.RunInThread(() => reference.SimulateFinalize()).Wait();

        Assert.True(reference.IsDisposed);

        // The delete is deferred to the JS thread, not run inline on the finalizer thread.
        Assert.True(_mockRuntime.HasReference(handle));
        Assert.Equal(1, syncContext.PendingCount);

        // Pumping the sync context runs the posted delete, releasing the native reference.
        Assert.Equal(1, syncContext.RunPendingCallbacks());
        Assert.False(_mockRuntime.HasReference(handle));
    }

    // Explicit Dispose() from a thread with no current scope must not throw. The pre-refactor
    // no-context path asserted thread access and threw JSInvalidThreadAccessException here; every
    // reference is now context-backed, so the delete is posted to the JS thread instead.
    [Fact]
    public void DisposeReferenceFromDifferentThreadPostsDelete()
    {
        var syncContext = new MockJSRuntime.RecordingSynchronizationContext();
        using JSValueScope rootScope = TestScope(syncContext);

        JSValue value = JSValue.CreateObject();
        var reference = new JSReference(value);
        napi_ref handle = reference.Handle;
        Assert.True(_mockRuntime.HasReference(handle));

        TestUtils.RunInThread(() => reference.Dispose()).Wait();

        Assert.True(reference.IsDisposed);

        // The delete is deferred to the JS thread, not run inline on the disposing thread.
        Assert.True(_mockRuntime.HasReference(handle));
        Assert.Equal(1, syncContext.PendingCount);

        // Pumping the sync context runs the posted delete, releasing the native reference.
        Assert.Equal(1, syncContext.RunPendingCallbacks());
        Assert.False(_mockRuntime.HasReference(handle));
    }

    // The finalizer invokes the virtual Dispose(bool), so a derived override can throw before or
    // after the base implementation runs. ~JSReference() must catch at its entry point, otherwise
    // the exception escapes the finalizer and terminates the process. This drives real GC
    // finalization of an override that throws; if the guarantee held only for the base method, the
    // test host would crash instead of completing.
    [Fact]
    public void FinalizerSwallowsExceptionsFromDerivedDisposeOverride()
    {
        using JSValueScope rootScope = TestScope();

        CreateAndAbandonThrowingReference();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    // Creates a throwing reference in a separate non-inlined frame and keeps no reference to it, so
    // it becomes eligible for finalization once this method returns.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CreateAndAbandonThrowingReference()
    {
        JSValue value = JSValue.CreateObject();
        _ = new ThrowingFinalizerReference(value);
    }

    // Exposes the protected finalizer code path (Dispose(disposing: false)) so a test can invoke it
    // directly on a non-JS thread, deterministically reproducing what the GC finalizer does.
    private sealed class FinalizerTestReference : JSReference
    {
        public FinalizerTestReference(JSValue value) : base(value) { }

        // Invokes the finalizer code path (Dispose(disposing: false)) on this instance and returns
        // whether it completed. Reads instance state so it is not flagged as a static candidate.
        public bool SimulateFinalize()
        {
            Dispose(disposing: false);
            return IsDisposed;
        }
    }

    // A reference whose Dispose(bool) override throws, to verify the finalizer entry point catches
    // exceptions from derived overrides and not just from the base implementation.
    private sealed class ThrowingFinalizerReference : JSReference
    {
        public ThrowingFinalizerReference(JSValue value) : base(value) { }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            throw new InvalidOperationException("Simulated failure in a derived finalizer.");
        }
    }
}
