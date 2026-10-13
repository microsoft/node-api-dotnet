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
}
