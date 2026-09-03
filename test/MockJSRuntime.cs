// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;
using Xunit;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime.napi_status;

namespace Microsoft.JavaScript.NodeApi.Test;

/// <summary>
/// Mocks just enough JS runtime behavior to support unit-testing the library API
/// layer above the JS runtime.
/// </summary>
internal class MockJSRuntime : JSRuntime
{
    private static nint s_handleCounter = 0;

    private nint _instanceData;
    private readonly List<nint> _handleScopes = new();
    private readonly List<nint> _escapableScopes = new();
    private readonly Dictionary<nint, MockJSValue> _values = new();
    private readonly Dictionary<nint, MockJSRef> _references = new();

    private class MockJSValue
    {
        public napi_valuetype ValueType { get; init; }
        public object? Value { get; set; }
    }

    private class MockJSRef
    {
        public nint ValueHandle { get; set; }
        public uint RefCount { get; set; }
    }

    public override napi_status GetInstanceData(
        napi_env env, out nint result)
    {
        result = _instanceData;
        return napi_ok;
    }

    public override napi_status SetInstanceData(
        napi_env env, nint data, napi_finalize finalize_cb, nint finalize_hint)
    {
        _instanceData = data;
        return napi_ok;
    }

    public override napi_status OpenHandleScope(
        napi_env env, out napi_handle_scope result)
    {
        nint scope = ++s_handleCounter;
        _handleScopes.Add(scope);
        result = new napi_handle_scope(scope);
        return napi_ok;
    }

    public override napi_status CloseHandleScope(
        napi_env env, napi_handle_scope scope)
    {
        Assert.True(_handleScopes.Remove(scope.Handle));
        return napi_ok;
    }

    public override napi_status OpenEscapableHandleScope(
        napi_env env, out napi_escapable_handle_scope result)
    {
        nint scope = ++s_handleCounter;
        _escapableScopes.Add(scope);
        result = new napi_escapable_handle_scope(scope);
        return napi_ok;
    }

    public override napi_status CloseEscapableHandleScope(
        napi_env env, napi_escapable_handle_scope scope)
    {
        Assert.True(_escapableScopes.Remove(scope.Handle));
        return napi_ok;
    }

    public override napi_status EscapeHandle(
        napi_env env,
        napi_escapable_handle_scope scope,
        napi_value escapee,
        out napi_value result)
    {
        // Promote the value to the parent scope by mirroring it under a new handle, mimicking
        // napi_escape_handle returning a new value that is valid in the outer scope.
        if (!_values.TryGetValue(escapee.Handle, out MockJSValue? mockValue))
        {
            result = default;
            return napi_invalid_arg;
        }

        nint handle = ++s_handleCounter;
        _values.Add(handle, new MockJSValue
        {
            ValueType = mockValue.ValueType,
            Value = mockValue.Value,
        });
        result = new napi_value(handle);
        return napi_ok;
    }

    public override napi_status CreateString(
        napi_env env, ReadOnlySpan<char> utf16Str, out napi_value result)
    {
        nint handle = ++s_handleCounter;
        _values.Add(handle, new MockJSValue
        {
            ValueType = napi_valuetype.napi_string,
            Value = utf16Str.ToString(),
        });
        result = new napi_value(handle);
        return napi_ok;
    }

    public override napi_status CreateObject(
        napi_env env, out napi_value result)
    {
        nint handle = ++s_handleCounter;
        _values.Add(handle, new MockJSValue { ValueType = napi_valuetype.napi_object });
        result = new napi_value(handle);
        return napi_ok;
    }

    public override napi_status DefineProperties(
        napi_env env, napi_value js_object, ReadOnlySpan<napi_property_descriptor> properties)
        => napi_ok;

    public override napi_status GetValueType(
        napi_env env, napi_value value, out napi_valuetype result)
    {
        if (_values.TryGetValue(value.Handle, out MockJSValue? mockValue))
        {
            result = mockValue.ValueType;
            return napi_ok;
        }
        else
        {
            result = default;
            return napi_invalid_arg;
        }
    }

    public override napi_status CreateReference(
        napi_env env, napi_value value, uint initialRefcount, out napi_ref result)
    {
        nint handle = ++s_handleCounter;
        _references.Add(handle, new MockJSRef
        {
            ValueHandle = value.Handle,
            RefCount = initialRefcount,
        });
        result = new napi_ref(handle);
        return napi_ok;
    }

    public override napi_status GetReferenceValue(
        napi_env env, napi_ref @ref, out napi_value result)
    {
        if (_references.TryGetValue(@ref.Handle, out MockJSRef? mockRef))
        {
            result = new napi_value(mockRef.ValueHandle);
            return napi_ok;
        }
        else
        {
            result = default;
            return napi_invalid_arg;
        }
    }

    public override napi_status RefReference(
        napi_env env, napi_ref @ref, out uint result)
    {
        if (_references.TryGetValue(@ref.Handle, out MockJSRef? mockRef))
        {
            result = ++mockRef.RefCount;
            return napi_ok;
        }
        else
        {
            result = default;
            return napi_invalid_arg;
        }
    }

    public override napi_status UnrefReference(
        napi_env env, napi_ref @ref, out uint result)
    {
        if (_references.TryGetValue(@ref.Handle, out MockJSRef? mockRef))
        {
            result = --mockRef.RefCount;
            if (result == 0)
            {
                _references.Remove(@ref.Handle);
            }

            return napi_ok;
        }
        else
        {
            result = default;
            return napi_invalid_arg;
        }
    }

    public override napi_status DeleteReference(napi_env env, napi_ref @ref)
    {
        return _references.Remove(@ref.Handle) ? napi_ok : napi_invalid_arg;
    }

    /// <summary>
    /// Reports whether a reference handle is still present (that is, has not been deleted by
    /// <see cref="DeleteReference"/>). Lets a test assert that a deferred delete actually ran.
    /// </summary>
    public bool HasReference(napi_ref @ref) => _references.ContainsKey(@ref.Handle);

    /// <summary>
    /// Simulates the behavior of the JS runtime when a weakly-referenced value is released.
    /// </summary>
    public void MockReleaseWeakReferenceValue(napi_ref @ref)
    {
        if (_references.TryGetValue(@ref.Handle, out MockJSRef? mockRef))
        {
            mockRef.ValueHandle = 0;
        }
    }

    // Mocking the sync context prevents the runtime mock from having to implement APIs
    // to support initializing the thread-safe-function for the sync context.
    // Unit tests that use the mock runtime don't currently use the sync context.
    public class SynchronizationContext : JSSynchronizationContext
    {
        public override void CloseAsyncScope() => throw new NotImplementedException();
        public override void OpenAsyncScope() => throw new NotImplementedException();
    }

    // A synchronization context that records posted callbacks instead of running them, so a test
    // can deterministically pump the queue with <see cref="RunPendingCallbacks"/> and assert that
    // the posted work (for example a deferred DeleteReference) actually executed.
    public class RecordingSynchronizationContext : JSSynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _posted = new();

        public int PendingCount => _posted.Count;

        public override void Post(SendOrPostCallback callback, object? state)
        {
            if (IsDisposed) return;
            _posted.Enqueue((callback, state));
        }

        public override void Send(SendOrPostCallback callback, object? state)
        {
            if (IsDisposed) return;
            callback(state);
        }

        // Runs every callback posted so far and returns how many were run.
        public int RunPendingCallbacks()
        {
            int count = 0;
            while (_posted.Count > 0)
            {
                (SendOrPostCallback callback, object? state) = _posted.Dequeue();
                callback(state);
                count++;
            }

            return count;
        }

        public override void CloseAsyncScope() { }
        public override void OpenAsyncScope() { }
    }
}
