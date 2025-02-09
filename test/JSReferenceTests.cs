// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi.Test;

public class JSReferenceTests
{
    private readonly MockJSRuntime _mockRuntime = new();

    private JSValueScope TestScope(JSValueScopeType scopeType)
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        return new(scopeType, env, _mockRuntime, new MockJSRuntime.SynchronizationContext());
    }

    [Fact]
    public void GetReferenceFromSameScope()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        JSValue value = JSValue.CreateObject();
        JSReference reference = new(value);
        Assert.True(reference.GetValue().IsObject());
    }

    [Fact]
    public void GetReferenceFromParentScope()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        JSReference reference;
        using (JSValueScope handleScope = new(JSValueScopeType.Handle))
        {
            JSValue value = JSValue.CreateObject();
            reference = new JSReference(value);
        }

        Assert.True(reference.GetValue().IsObject());
    }

    [Fact]
    public void GetReferenceFromDifferentThread()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

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
        using JSValueScope rootScope1 = TestScope(JSValueScopeType.Root);

        JSValue value = JSValue.CreateObject();
        JSReference reference = new(value);

        // Run in a new thread and establish another root scope there.
        TestUtils.RunInThread(() =>
        {
            using JSValueScope rootScope2 = TestScope(JSValueScopeType.Root);
            Assert.Throws<JSInvalidThreadAccessException>(() => reference.GetValue());
        }).Wait();
    }

    [Fact]
    public void GetWeakReferenceUnavailable()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        JSValue value = JSValue.CreateObject();
        var reference = new JSReference(value, isWeak: true);

        _mockRuntime.MockReleaseWeakReferenceValue(reference.Handle);
        Assert.Throws<NullReferenceException>(() => reference.GetValue());
    }

    [Fact]
    public void TryGetWeakReferenceValue()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        JSValue value = JSValue.CreateObject();
        JSReference reference = new(value);
        Assert.True(reference.TryGetValue(out JSValue result));
        Assert.True(result.IsObject());
    }

    [Fact]
    public void TryGetWeakReferenceUnavailable()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        JSValue value = JSValue.CreateObject();
        var reference = new JSReference(value, isWeak: true);

        _mockRuntime.MockReleaseWeakReferenceValue(reference.Handle);
        Assert.False(reference.TryGetValue(out _));
    }
}
