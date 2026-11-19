// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.JavaScript.NodeApi.Runtime;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi.Test;

public class JSErrorTests
{
    [Theory]
    [InlineData(napi_status.napi_ok, false)]
    [InlineData(napi_status.napi_pending_exception, false)]
    [InlineData(napi_status.napi_closing, false)]
    [InlineData(napi_status.napi_generic_failure, true)]
    public void ThrowErrorFallsBackWhenErrorConstructionFails(
        napi_status fallbackStatus, bool shouldThrow)
    {
        var runtime = new ErrorConstructionFailureRuntime(fallbackStatus);
        napi_env env = new(Environment.CurrentManagedThreadId);
        using var scope = new JSValueScope(
            JSValueScopeType.NoContext,
            env,
            runtime,
            new MockJSRuntime.SynchronizationContext());

        if (shouldThrow)
        {
            Assert.Throws<JSException>(() => JSError.ThrowError(
                new InvalidOperationException("test error")));
        }
        else
        {
            JSError.ThrowError(new InvalidOperationException("test error"));
        }

        Assert.Equal(1, runtime.ThrowErrorCallCount);
        Assert.Null(runtime.LastErrorCode);
        Assert.Equal("test error", runtime.LastErrorMessage);
    }

    [Theory]
    [InlineData(napi_status.napi_ok, true)]
    [InlineData(napi_status.napi_pending_exception, false)]
    [InlineData(napi_status.napi_closing, false)]
    public void ThrowErrorReturnsWhenEnvironmentCannotAcceptAnException(
        napi_status status, bool isPending)
    {
        var runtime = new TerminatingJSRuntime(status, isPending);
        napi_env env = new(Environment.CurrentManagedThreadId);
        using var scope = new JSValueScope(
            JSValueScopeType.NoContext,
            env,
            runtime,
            new MockJSRuntime.SynchronizationContext());

        JSError.ThrowError(new InvalidOperationException("test"));

        Assert.Equal(1, runtime.IsExceptionPendingCallCount);
    }

    private sealed class TerminatingJSRuntime : MockJSRuntime
    {
        private readonly napi_status _status;
        private readonly bool _isPending;

        public TerminatingJSRuntime(napi_status status, bool isPending)
        {
            _status = status;
            _isPending = isPending;
        }

        public int IsExceptionPendingCallCount { get; private set; }

        public override napi_status IsExceptionPending(napi_env env, out bool result)
        {
            IsExceptionPendingCallCount++;
            result = _isPending;
            return _status;
        }
    }

    private sealed class ErrorConstructionFailureRuntime : MockJSRuntime
    {
        private readonly napi_status _fallbackStatus;

        public ErrorConstructionFailureRuntime(napi_status fallbackStatus)
        {
            _fallbackStatus = fallbackStatus;
        }

        public int ThrowErrorCallCount { get; private set; }
        public string? LastErrorCode { get; private set; }
        public string? LastErrorMessage { get; private set; }

        public override napi_status IsExceptionPending(napi_env env, out bool result)
        {
            result = false;
            return napi_status.napi_ok;
        }

        public override napi_status CreateError(
            napi_env env, napi_value code, napi_value msg, out napi_value result)
        {
            result = default;
            throw new JSException("Environment started closing during error construction.");
        }

        public override napi_status ThrowError(napi_env env, string? code, string message)
        {
            ThrowErrorCallCount++;
            LastErrorCode = code;
            LastErrorMessage = message;
            return _fallbackStatus;
        }
    }
}
