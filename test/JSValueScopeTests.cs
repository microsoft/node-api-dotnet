// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.CompilerServices;
using Microsoft.JavaScript.NodeApi.Interop;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi.Test;

/// <summary>
/// Unit tests for <see cref="JSValueScope"/>. Validates that scopes can be initialized and nested
/// with intended limitations, and that values can be used only within the scope (and thread)
/// with which they were created.
/// </summary>
public class JSValueScopeTests
{
    private readonly MockJSRuntime _mockRuntime = new();

    private JSValueScope TestRuntimeScope() => JSValueScopeTests.TestRuntimeScope(_mockRuntime);

    private static JSValueScope TestRuntimeScope(MockJSRuntime runtime)
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, runtime, new MockJSRuntime.SynchronizationContext());
        return JSValueScope.CreateRuntimeScope(env, context);
    }

    [Fact]
    public void CreateRuntimeScope()
    {
        using JSValueScope runtimeScope = TestRuntimeScope();
        Assert.NotNull(runtimeScope.RuntimeContext);
        Assert.Equal(JSValueScopeType.RuntimeContext, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateNestedRuntimeScope()
    {
        using JSValueScope runtimeScope = TestRuntimeScope();

        using (JSValueScope nestedScope = JSValueScope.CreateRuntimeScope())
        {
            Assert.Same(runtimeScope.RuntimeContext, nestedScope.RuntimeContext);
            Assert.Equal(JSValueScopeType.RuntimeContext, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.RuntimeContext, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateHandleScopeWithinRuntimeScope()
    {
        using JSValueScope runtimeScope = TestRuntimeScope();

        using (JSValueScope handleScope = JSValueScope.CreateHandleScope())
        {
            Assert.Equal(JSValueScopeType.Handle, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.RuntimeContext, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateHandleScopeWithinNestedRuntimeScope()
    {
        using JSValueScope runtimeScope = TestRuntimeScope();

        using (JSValueScope nestedScope = JSValueScope.CreateRuntimeScope())
        {
            using (JSValueScope handleScope = JSValueScope.CreateHandleScope())
            {
                Assert.Equal(JSValueScopeType.Handle, JSValueScope.Current.ScopeType);
            }

            Assert.Equal(JSValueScopeType.RuntimeContext, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.RuntimeContext, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateEscapableScopeWithinRuntimeScope()
    {
        using JSValueScope runtimeScope = TestRuntimeScope();

        using (JSValueScope escapableScope = JSValueScope.CreateEscapableScope())
        {
            Assert.Equal(JSValueScopeType.Escapable, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.RuntimeContext, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void HandleScopeRequiresParentScope()
    {
        Assert.Throws<InvalidOperationException>(
            () => JSValueScope.CreateHandleScope());
        Assert.Throws<InvalidOperationException>(
            () => JSValueScope.CreateEscapableScope());
    }

    private sealed class DisposableModule : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;
    }

    [Fact]
    public void DisposableModulesShareContextAndAreDisposedOnceAtTeardown()
    {
        var moduleA = new DisposableModule();
        var moduleB = new DisposableModule();

        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());

        using (JSValueScope.CreateRuntimeScope(env, context))
        {
            // Two generated modules loaded into one managed host share this context; each opens a
            // module-boundary scope and exports its instance.
            using (JSValueScope.CreateModuleScope(env))
            {
                new JSModuleBuilder<DisposableModule>().ExportModule(
                    moduleA, (JSObject)JSValue.CreateObject());
            }

            using (JSValueScope.CreateModuleScope(env))
            {
                new JSModuleBuilder<DisposableModule>().ExportModule(
                    moduleB, (JSObject)JSValue.CreateObject());
            }

            // Loading the second module must not dispose the first.
            Assert.Equal(0, moduleA.DisposeCount);
            Assert.Equal(0, moduleB.DisposeCount);
        }

        context.Dispose();

        // Each module instance is disposed exactly once at env teardown.
        Assert.Equal(1, moduleA.DisposeCount);
        Assert.Equal(1, moduleB.DisposeCount);
    }

    private sealed class EqualDisposable : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose() => DisposeCount++;

        // All instances compare equal, to prove module disposables dedupe by identity, not Equals.
        public override bool Equals(object? obj) => obj is EqualDisposable;

        public override int GetHashCode() => 0;
    }

    [Fact]
    public void ModuleDisposablesAreDedupedByIdentityNotEquality()
    {
        var moduleA = new EqualDisposable();
        var moduleB = new EqualDisposable();
        Assert.True(moduleA.Equals(moduleB));

        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());

        context.AddModuleDisposable(moduleA);
        context.AddModuleDisposable(moduleB);
        context.AddModuleDisposable(moduleA); // Re-adding the same instance is a no-op.

        context.Dispose();

        // Both distinct instances are disposed once, despite comparing equal.
        Assert.Equal(1, moduleA.DisposeCount);
        Assert.Equal(1, moduleB.DisposeCount);
    }

    [Fact]
    public void AccessValueFromClosedScope()
    {
        using JSValueScope rootScope = TestRuntimeScope();

        JSValueScope handleScope;
        JSValue objectValue;
        using (handleScope = JSValueScope.CreateHandleScope())
        {
            objectValue = JSValue.CreateObject();
            Assert.True(objectValue.IsObject());
        }

        Assert.True(handleScope.IsDisposed);
        JSValueScopeClosedException ex = Assert.Throws<JSValueScopeClosedException>(
            () => objectValue.IsObject());
        Assert.Equal(handleScope, ex.Scope);
    }

    [Fact]
    public void AccessPropertyKeyFromClosedScope()
    {
        using JSValueScope rootScope = TestRuntimeScope();

        JSValue objectValue = JSValue.CreateObject();
        JSValue propertyKey;

        JSValueScope handleScope;
        using (handleScope = JSValueScope.CreateHandleScope())
        {
            propertyKey = "test";
            Assert.True(propertyKey.IsString());
        }

        // The property key scope was closed so it's not valid to use as a method argument.
        Assert.True(handleScope.IsDisposed);
        JSValueScopeClosedException ex = Assert.Throws<JSValueScopeClosedException>(
            () => objectValue[propertyKey]);
        Assert.Equal(handleScope, ex.Scope);

        // The object value scope was not closed so it's still valid.
        Assert.True(objectValue.IsObject());
    }

    [Fact]
    public void CreateValueFromDifferentThread()
    {
        using JSValueScope rootScope = TestRuntimeScope();

        // Run in a new thread which will not have any current scope.
        TestUtils.RunInThread(() =>
        {
            Assert.Throws<JSInvalidThreadAccessException>(() => JSValueScope.Current);
            JSInvalidThreadAccessException ex = Assert.Throws<JSInvalidThreadAccessException>(
                () => new JSObject());
            Assert.Null(ex.CurrentScope);
            Assert.Null(ex.TargetScope);
        }).Wait();
    }

    [Fact]
    public void AccessValueFromDifferentThread()
    {
        using JSValueScope rootScope = TestRuntimeScope();
        JSValue objectValue = JSValue.CreateObject();

        // Run in a new thread which will not have any current scope.
        TestUtils.RunInThread(() =>
        {
            Assert.Throws<JSInvalidThreadAccessException>(() => JSValueScope.Current);
            JSInvalidThreadAccessException ex = Assert.Throws<JSInvalidThreadAccessException>(
                () => objectValue.IsObject());
            Assert.Null(ex.CurrentScope);
            Assert.Equal(rootScope, ex.TargetScope);
        }).Wait();
    }

    [Fact]
    public void AccessValueFromDifferentRootScope()
    {
        using JSValueScope rootScope1 = TestRuntimeScope();
        JSValue objectValue = JSValue.CreateObject();

        // Run in a new thread and establish another root scope there.
        TestUtils.RunInThread(() =>
        {
            // Separate runtime so rootScope2's env has its own instance data (one context per env).
            using JSValueScope rootScope2 = JSValueScopeTests.TestRuntimeScope(new MockJSRuntime());
            Assert.Equal(JSValueScopeType.RuntimeContext, JSValueScope.Current.ScopeType);
            JSInvalidThreadAccessException ex = Assert.Throws<JSInvalidThreadAccessException>(
                () => objectValue.IsObject());
            Assert.Equal(rootScope2, ex.CurrentScope);
            Assert.Equal(rootScope1, ex.TargetScope);
        }).Wait();
    }

    [Fact]
    public void EnterRuntimeContextFromDifferentThreadThrows()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());

        // A runtime context may be entered only on the thread that created it.
        TestUtils.RunInThread(() =>
        {
            Assert.Throws<JSInvalidThreadAccessException>(
                () => JSValueScope.CreateRuntimeScope(env, context));
        }).Wait();
    }

    [Fact]
    public void EnterDisposedRuntimeContextThrows()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());
        context.Dispose();

        // A disposed context's environment is torn down, so a scope must not adopt it.
        Assert.Throws<ObjectDisposedException>(
            () => JSValueScope.CreateRuntimeScope(env, context));
    }

    [Fact]
    public void DisposeRuntimeContextFromDifferentThreadThrows()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());

        // A runtime context may be disposed only on the thread that created it, because teardown
        // calls thread-affine napi.
        TestUtils.RunInThread(() =>
        {
            Assert.Throws<JSInvalidThreadAccessException>(() => context.Dispose());
        }).Wait();

        // The failed off-thread attempt left the context live, so disposing on the owning thread
        // still succeeds.
        context.Dispose();
    }

    [Fact]
    public void SynchronizationContextRejectsLazyCreateWhenContextNotCurrent()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);

        // Separate runtimes so each context has its own instance data (an env is associated with a
        // single context), letting contextB be current while contextA is not.
        var contextA = new JSRuntimeContext(env, new MockJSRuntime()); // no sync context -> lazy
        var contextB = new JSRuntimeContext(
            env, new MockJSRuntime(), new MockJSRuntime.SynchronizationContext());

        using (JSValueScope.CreateRuntimeScope(env, contextB))
        {
            // contextB is current, so lazily creating contextA's sync context (which would capture
            // the current scope's environment) must be rejected.
            Assert.Throws<InvalidOperationException>(() => contextA.SynchronizationContext);
        }

        contextA.Dispose();

        // After disposal, lazy creation is rejected too.
        Assert.Throws<ObjectDisposedException>(() => contextA.SynchronizationContext);
        contextB.Dispose();
    }

    [Fact]
    public void DisposeScopeWhileNestedScopeOpenThrows()
    {
        using JSValueScope runtimeScope = TestRuntimeScope();
        JSValueScope handleScope = JSValueScope.CreateHandleScope();

        // A scope cannot be disposed while a scope created within it is still open (LIFO order).
        Assert.Throws<InvalidOperationException>(() => runtimeScope.Dispose());

        // Disposing in the correct reverse order succeeds.
        handleScope.Dispose();
    }

    [Fact]
    public void DisposeScopeFromDifferentThreadThrows()
    {
        using JSValueScope runtimeScope = TestRuntimeScope();

        // A scope must be disposed on the thread that created it, not another thread.
        TestUtils.RunInThread(() =>
        {
            Assert.Throws<JSInvalidThreadAccessException>(() => runtimeScope.Dispose());
        }).Wait();
    }

    private sealed class ThrowOnceOnScopeCloseRuntime : MockJSRuntime
    {
        private bool _throwOnClose = true;

        private void ThrowOnce()
        {
            if (_throwOnClose)
            {
                _throwOnClose = false;
                throw new InvalidOperationException("scope close failure");
            }
        }

        public override napi_status CloseHandleScope(
            napi_env env, napi_handle_scope scope)
        {
            ThrowOnce();
            return base.CloseHandleScope(env, scope);
        }

        public override napi_status CloseEscapableHandleScope(
            napi_env env, napi_escapable_handle_scope scope)
        {
            ThrowOnce();
            return base.CloseEscapableHandleScope(env, scope);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedScopeCloseCanBeRetried(bool escapable)
    {
        var runtime = new ThrowOnceOnScopeCloseRuntime();
        using JSValueScope runtimeScope = TestRuntimeScope(runtime);
        JSValueScope scope = escapable
            ? JSValueScope.CreateEscapableScope()
            : JSValueScope.CreateHandleScope();

        Assert.Throws<InvalidOperationException>(() => scope.Dispose());
        Assert.False(scope.IsDisposed);
        Assert.Same(scope, JSValueScope.Current);

        scope.Dispose();
        Assert.True(scope.IsDisposed);
        Assert.Same(runtimeScope, JSValueScope.Current);
    }

    [Fact]
    public void DisposeRuntimeContextWhenIdleDefersUntilOutermostScopeCloses()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());

        using (JSValueScope outerScope = JSValueScope.CreateRuntimeScope(env, context))
        {
            using (JSValueScope handleScope = JSValueScope.CreateHandleScope())
            {
                // A disposal request while scopes are open must be deferred: disposing now would
                // leave the handle scope to close on a disposed context as it unwinds.
                JSValueScope.DisposeRuntimeContextWhenIdle(context);
                Assert.False(context.IsDisposed);
            }

            // The inner scope closed, but the outer scope keeps the context alive.
            Assert.False(context.IsDisposed);
        }

        // The outermost scope closed, so the context is disposed once no scope remains open.
        Assert.True(context.IsDisposed);
    }

    [Fact]
    public void DisposeRuntimeContextWhenIdleDisposesImmediatelyWithNoScope()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());

        // With no value scope open, the request is applied immediately.
        JSValueScope.DisposeRuntimeContextWhenIdle(context);
        Assert.True(context.IsDisposed);
    }

    [Fact]
    public void NestedRuntimeScopeWithDifferentContextThrows()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);

        // Separate runtimes so each context owns its own instance data.
        var contextA = new JSRuntimeContext(
            env, new MockJSRuntime(), new MockJSRuntime.SynchronizationContext());
        var contextB = new JSRuntimeContext(
            env, new MockJSRuntime(), new MockJSRuntime.SynchronizationContext());

        using (JSValueScope.CreateRuntimeScope(env, contextA))
        {
            // Every scope on a thread's stack shares one runtime context, so nesting a scope for a
            // different context is rejected.
            Assert.Throws<InvalidOperationException>(
                () => JSValueScope.CreateRuntimeScope(env, contextB));
        }

        contextA.Dispose();
        contextB.Dispose();
    }

    [Fact]
    public void RegisteringSecondContextOnEnvIsRejected()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());

        // An env is associated with a runtime context exactly once.
        Assert.Throws<InvalidOperationException>(
            () => new JSRuntimeContext(env, _mockRuntime, new MockJSRuntime.SynchronizationContext()));

        context.Dispose();

        // Even after disposal the env cannot be re-associated (the slot is tombstoned).
        Assert.Throws<InvalidOperationException>(
            () => new JSRuntimeContext(env, _mockRuntime, new MockJSRuntime.SynchronizationContext()));
    }

    [Fact]
    public void ContextRootReleasedWhenTeardownStepThrows()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(env, _mockRuntime, new ThrowingSynchronizationContext());
        var module = new DisposableModule();
        context.AddModuleDisposable(module);

        // The sync context's Dispose throws, but every later teardown phase still runs: the module
        // disposable is disposed and the context root is released. The failure surfaces after cleanup.
        Assert.Throws<InvalidOperationException>(() => context.Dispose());
        Assert.Equal(1, module.DisposeCount);
        Assert.Null(JSRuntimeContext.FromEnv(env));
    }

    private sealed class ThrowingSynchronizationContext : JSSynchronizationContext
    {
        public override void Dispose() => throw new InvalidOperationException("teardown failure");
        public override void OpenAsyncScope() { }
        public override void CloseAsyncScope() { }
    }

    // The module instance is captured through a shared holder: descriptors take the holder during
    // initialization (before the instance exists) and observe the instance once dispatch assigns it.
    // Nested handle/escapable scopes inherit the same holder, so Current.Module round-trips through it.
    [Fact]
    public void ModuleInstanceRoundTripsThroughSharedHolder()
    {
        using JSValueScope runtimeScope = TestRuntimeScope();

        // The runtime scope mints a holder; the module instance is not assigned yet.
        StrongBox<object?> holder = JSValueScope.Current.ModuleHolder!;
        Assert.NotNull(holder);
        Assert.Null(JSValueScope.Current.Module);

        object moduleInstance = new();
        using (JSValueScope handleScope = JSValueScope.CreateHandleScope())
        {
            // Inner scopes inherit the same holder instance.
            Assert.Same(holder, JSValueScope.Current.ModuleHolder);

            // Assigning through the shared holder (as dispatch does) is visible as Current.Module.
            holder.Value = moduleInstance;
            Assert.Same(moduleInstance, JSValueScope.Current.Module);
        }

        // The instance remains visible in the parent scope after the nested scope closes.
        Assert.Same(moduleInstance, JSValueScope.Current.Module);
    }

    // An escapable scope promotes one value to its parent so the value stays usable after the inner
    // scope closes, while a value that was not escaped becomes invalid once the scope is disposed.
    [Fact]
    public void EscapableScopeEscapesValue()
    {
        using JSValueScope rootScope = TestRuntimeScope();

        JSValue escaped;
        JSValue notEscaped;
        JSValueScope escapableScope;
        using (escapableScope = JSValueScope.CreateEscapableScope())
        {
            notEscaped = JSValue.CreateObject();
            escaped = escapableScope.Escape(JSValue.CreateObject());

            Assert.True(escaped.IsObject());
            Assert.True(notEscaped.IsObject());
        }

        // The escaped value was promoted to the parent scope, so it remains usable.
        Assert.True(escapableScope.IsDisposed);
        Assert.True(escaped.IsObject());

        // The value that was not escaped belonged to the now-closed scope.
        JSValueScopeClosedException ex = Assert.Throws<JSValueScopeClosedException>(
            () => notEscaped.IsObject());
        Assert.Equal(escapableScope, ex.Scope);
    }

    // With no explicit context and no parent scope, CreateRuntimeScope recovers the context from the
    // env instance data (JSRuntimeContext.FromEnv) -- the path the dynamic module entry point relies
    // on to resolve the context when no scope is on the thread yet.
    [Fact]
    public void CreateRuntimeScopeResolvesContextFromEnv()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        var context = new JSRuntimeContext(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());

        // The context registered itself in the env instance data, so FromEnv resolves it.
        Assert.Same(context, JSRuntimeContext.FromEnv(env));

        using JSValueScope runtimeScope = JSValueScope.CreateRuntimeScope(env);
        Assert.Same(context, runtimeScope.RuntimeContext);
        Assert.Same(context, JSValueScope.Current.RuntimeContext);
    }

    // JSRuntimeContext.Create is the public factory used by AOT entry points and embedders. It uses
    // the provided runtime, and a runtime scope over the context resolves it as the current context.
    [Fact]
    public void CreateRuntimeContextFactoryUsesProvidedRuntime()
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        JSRuntimeContext context = JSRuntimeContext.Create(
            env, _mockRuntime, new MockJSRuntime.SynchronizationContext());

        Assert.Same(_mockRuntime, context.Runtime);
        Assert.False(context.IsDisposed);

        using JSValueScope runtimeScope = JSValueScope.CreateRuntimeScope(env, context);
        Assert.Same(context, JSValueScope.Current.RuntimeContext);
        Assert.Same(context, JSRuntimeContext.Current);
    }

    [Fact]
    public void RuntimeContextDisposalIsInfrastructureOnly()
    {
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(JSRuntimeContext)));
        Assert.Null(typeof(JSRuntimeContext).GetMethod(nameof(IDisposable.Dispose)));
    }

    // A runtime-context scope installs the context's synchronization context as the thread's current
    // one for its lifetime (so await continuations marshal back to the JS thread) and restores the
    // previously-current one when disposed.
    [Fact]
    public void RuntimeScopeInstallsAndRestoresSynchronizationContext()
    {
        System.Threading.SynchronizationContext? previous =
            System.Threading.SynchronizationContext.Current;

        napi_env env = new(Environment.CurrentManagedThreadId);
        var syncContext = new MockJSRuntime.SynchronizationContext();
        var context = new JSRuntimeContext(env, _mockRuntime, syncContext);

        using (JSValueScope runtimeScope = JSValueScope.CreateRuntimeScope(env, context))
        {
            Assert.Same(syncContext, System.Threading.SynchronizationContext.Current);
        }

        Assert.Same(previous, System.Threading.SynchronizationContext.Current);
    }
}
