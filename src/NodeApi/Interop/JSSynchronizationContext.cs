// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Microsoft.JavaScript.NodeApi.Interop;

public sealed class JSSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly JSThreadSafeFunction _tsfn;

    public bool IsDisposed { get; private set; }

    public static new JSSynchronizationContext? Current
        => SynchronizationContext.Current as JSSynchronizationContext;

    public JSSynchronizationContext()
    {
        _tsfn = new JSThreadSafeFunction(
            maxQueueSize: 0,
            initialThreadCount: 1,
            asyncResourceName: (JSValue)"SynchronizationContext");

        // Unref TSFN to indicate that this TSFN is not preventing Node.JS shutdown.
        _tsfn.Unref();
    }

    public void Dispose()
    {
        if (IsDisposed) return;

        IsDisposed = true;

        // Destroy TSFN by releasing last thread use count.
        // TSFN is deleted after this point and must not be used.
        _tsfn.Release();
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        if (IsDisposed) return;

        _tsfn.NonBlockingCall(() => callback(state));
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        if (this == Current)
        {
            callback(state);
            return;
        }

        if (IsDisposed) return;

        using ManualResetEvent syncEvent = new(false);
        _tsfn.NonBlockingCall(() =>
        {
            callback(state);
            syncEvent.Set();
        });
        syncEvent.WaitOne();
    }

    /// <summary>
    /// Increment reference count for the main loop async resource.
    /// Non-zero count prevents Node.JS process from exiting.
    /// </summary>
    public void OpenAsyncScope()
    {
        _tsfn.Ref();
    }

    /// <summary>
    /// Decrement reference count for the main loop async resource.
    /// Non-zero count prevents Node.JS process from exiting.
    /// </summary>
    public void CloseAsyncScope()
    {
        _tsfn.Unref();
    }
}
