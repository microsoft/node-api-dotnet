// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
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

    private JSValueScope TestScope(JSValueScopeType scopeType)
    {
        napi_env env = new(Environment.CurrentManagedThreadId);
        return new(scopeType, env, _mockRuntime, new MockJSRuntime.SynchronizationContext());
    }

    [Fact]
    public void CreateNoContextScope()
    {
        using JSValueScope noContextScope = TestScope(JSValueScopeType.NoContext);
        Assert.Null(noContextScope.RuntimeContext);
        Assert.Equal(JSValueScopeType.NoContext, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateRootScope()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);
        Assert.NotNull(rootScope.RuntimeContext);
        Assert.Equal(JSValueScopeType.Root, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateModuleScopeWithinNoContextScope()
    {
        using JSValueScope noContextScope = TestScope(JSValueScopeType.NoContext);

        using (JSValueScope moduleScope = TestScope(JSValueScopeType.Module))
        {
            Assert.NotNull(moduleScope.RuntimeContext);
            Assert.Equal(JSValueScopeType.Module, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.NoContext, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateModuleScopeWithinRootScope()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        using (JSValueScope moduleScope = new(JSValueScopeType.Module))
        {
            Assert.NotNull(moduleScope.RuntimeContext);
            Assert.Equal(JSValueScopeType.Module, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.Root, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateModuleScopeWithoutRoot()
    {
        using JSValueScope moduleScope = TestScope(JSValueScopeType.Module);
        Assert.NotNull(moduleScope.RuntimeContext);
        Assert.Equal(JSValueScopeType.Module, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateCallbackScope()
    {
        using JSValueScope moduleScope = TestScope(JSValueScopeType.Module);

        using (JSValueScope callbackScope = new(JSValueScopeType.Callback))
        {
            Assert.NotNull(moduleScope.RuntimeContext);
            Assert.Equal(JSValueScopeType.Callback, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.Module, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateHandleScopeWithinRoot()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        using (JSValueScope handleScope = new(JSValueScopeType.Handle))
        {
            Assert.Equal(JSValueScopeType.Handle, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.Root, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateHandleScopeWithinModule()
    {
        using JSValueScope moduleScope = TestScope(JSValueScopeType.Module);

        using (JSValueScope handleScope = new(JSValueScopeType.Handle))
        {
            Assert.Equal(JSValueScopeType.Handle, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.Module, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateHandleScopeWithinCallback()
    {
        using JSValueScope moduleScope = TestScope(JSValueScopeType.Module);

        using (JSValueScope callbackScope = new(JSValueScopeType.Callback))
        {
            using (JSValueScope handleScope = new(JSValueScopeType.Handle))
            {
                Assert.Equal(JSValueScopeType.Handle, JSValueScope.Current.ScopeType);
            }

            Assert.Equal(JSValueScopeType.Callback, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.Module, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void CreateEscapableScopeWithinCallback()
    {
        using JSValueScope moduleScope = TestScope(JSValueScopeType.Module);

        using (JSValueScope callbackScope = new(JSValueScopeType.Callback))
        {
            using (JSValueScope escapableScope = new(JSValueScopeType.Escapable))
            {
                Assert.Equal(JSValueScopeType.Escapable, JSValueScope.Current.ScopeType);
            }

            Assert.Equal(JSValueScopeType.Callback, JSValueScope.Current.ScopeType);
        }

        Assert.Equal(JSValueScopeType.Module, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void InvalidNoContextScopeNesting()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope noContextScope = new(JSValueScopeType.NoContext);
        });
        Assert.Equal(JSValueScopeType.Root, JSValueScope.Current.ScopeType);

        using JSValueScope moduleScope = new(JSValueScopeType.Module);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope noContextScope = new(JSValueScopeType.NoContext);
        });
        Assert.Equal(JSValueScopeType.Module, JSValueScope.Current.ScopeType);

        using JSValueScope callbackScope = new(JSValueScopeType.Callback);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope noContextScope = new(JSValueScopeType.NoContext);
        });
        Assert.Equal(JSValueScopeType.Callback, JSValueScope.Current.ScopeType);

        using JSValueScope handleScope = new(JSValueScopeType.Handle);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope noContextScope = new(JSValueScopeType.NoContext);
        });
        Assert.Equal(JSValueScopeType.Handle, JSValueScope.Current.ScopeType);

        using JSValueScope escapableScope = new(JSValueScopeType.Escapable);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope noContextScope = new(JSValueScopeType.NoContext);
        });
        Assert.Equal(JSValueScopeType.Escapable, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void InvalidRootContextScopeNesting()
    {
        using JSValueScope noContextScope = TestScope(JSValueScopeType.NoContext);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope rootScope = new(JSValueScopeType.Root);
        });
        Assert.Equal(JSValueScopeType.NoContext, JSValueScope.Current.ScopeType);

        using JSValueScope moduleScope = TestScope(JSValueScopeType.Module);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope rootScope = new(JSValueScopeType.Root);
        });
        Assert.Equal(JSValueScopeType.Module, JSValueScope.Current.ScopeType);

        using JSValueScope callbackScope = new(JSValueScopeType.Callback);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope rootScope = new(JSValueScopeType.Root);
        });
        Assert.Equal(JSValueScopeType.Callback, JSValueScope.Current.ScopeType);

        using JSValueScope handleScope = new(JSValueScopeType.Handle);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope rootScope = new(JSValueScopeType.Root);
        });
        Assert.Equal(JSValueScopeType.Handle, JSValueScope.Current.ScopeType);

        using JSValueScope escapableScope = new(JSValueScopeType.Escapable);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope rootScope = new(JSValueScopeType.Root);
        });
        Assert.Equal(JSValueScopeType.Escapable, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void InvalidModuleContextScopeNesting()
    {
        using JSValueScope moduleScope = TestScope(JSValueScopeType.Module);
        using JSValueScope callbackScope = new(JSValueScopeType.Callback);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope nestedModuleScope = new(JSValueScopeType.Module);
        });
        Assert.Equal(JSValueScopeType.Callback, JSValueScope.Current.ScopeType);

        using JSValueScope handleScope = new(JSValueScopeType.Handle);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope nestedModuleScope = new(JSValueScopeType.Module);
        });
        Assert.Equal(JSValueScopeType.Handle, JSValueScope.Current.ScopeType);

        using JSValueScope escapableScope = new(JSValueScopeType.Escapable);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope nestedModuleScope = new(JSValueScopeType.Module);
        });
        Assert.Equal(JSValueScopeType.Escapable, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void InvalidCallbackContextScopeNesting()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        using JSValueScope handleScope = new(JSValueScopeType.Handle);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope callbackScope = new(JSValueScopeType.Callback);
        });
        Assert.Equal(JSValueScopeType.Handle, JSValueScope.Current.ScopeType);

        using JSValueScope escapableScope = new(JSValueScopeType.Escapable);
        Assert.Throws<InvalidOperationException>(() =>
        {
            using JSValueScope callbackScope = new(JSValueScopeType.Callback);
        });
        Assert.Equal(JSValueScopeType.Escapable, JSValueScope.Current.ScopeType);
    }

    [Fact]
    public void AccessValueFromClosedScope()
    {
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        JSValueScope handleScope;
        JSValue objectValue;
        using (handleScope = new(JSValueScopeType.Handle))
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
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        JSValue objectValue = JSValue.CreateObject();
        JSValue propertyKey;

        JSValueScope handleScope;
        using (handleScope = new(JSValueScopeType.Handle))
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
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);

        // Run in a new thread which will not have any current scope.
        Task.Run(() =>
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
        using JSValueScope rootScope = TestScope(JSValueScopeType.Root);
        JSValue objectValue = JSValue.CreateObject();

        // Run in a new thread which will not have any current scope.
        Task.Run(() =>
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
        using JSValueScope rootScope1 = TestScope(JSValueScopeType.Root);
        JSValue objectValue = JSValue.CreateObject();

        // Run in a new thread and establish another root scope there.
        Task.Run(() =>
        {
            using JSValueScope rootScope2 = TestScope(JSValueScopeType.Root);
            Assert.Equal(JSValueScopeType.Root, JSValueScope.Current.ScopeType);
            JSInvalidThreadAccessException ex = Assert.Throws<JSInvalidThreadAccessException>(
                () => objectValue.IsObject());
            Assert.Equal(rootScope2, ex.CurrentScope);
            Assert.Equal(rootScope1, ex.TargetScope);
        }).Wait();
    }
}
