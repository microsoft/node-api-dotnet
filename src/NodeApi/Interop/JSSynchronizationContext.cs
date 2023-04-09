// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using Microsoft.NodeApi;

namespace Microsoft.JavaScript.NodeApi.Interop;

public abstract class JSSynchronizationContext : SynchronizationContext, IDisposable
{
    public bool IsDisposed { get; private set; }

    public static new JSSynchronizationContext? Current
        => SynchronizationContext.Current as JSSynchronizationContext;

    public static JSSynchronizationContext Create()
    {
        if (JSThreadSafeFunction.IsAvailable)
        {
            return new JSTsfnSynchronizationContext();
        }
        else if (JSDispatcherQueue.GetForCurrentThread() is JSDispatcherQueue queue)
        {
            return new JSDispatcherSynchronizationContext(queue);
        }
        else
        {
            throw new JSException("Cannot create synchronization context.");
        }
    }

    protected JSSynchronizationContext() { }

    public virtual void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }

    public abstract void OpenAsyncScope();

    public abstract void CloseAsyncScope();
}

public sealed class JSTsfnSynchronizationContext : JSSynchronizationContext
{
    private readonly JSThreadSafeFunction _tsfn;

    public JSTsfnSynchronizationContext()
    {
        _tsfn = new JSThreadSafeFunction(
            maxQueueSize: 0,
            initialThreadCount: 1,
            asyncResourceName: (JSValue)"SynchronizationContext");

        // Unref TSFN to indicate that this TSFN is not preventing Node.JS shutdown.
        _tsfn.Unref();
    }

    public override void Dispose()
    {
        if (IsDisposed) return;

        base.Dispose();

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
    public override void OpenAsyncScope()
    {
        _tsfn.Ref();
    }

    /// <summary>
    /// Decrement reference count for the main loop async resource.
    /// Non-zero count prevents Node.JS process from exiting.
    /// </summary>
    public override void CloseAsyncScope()
    {
        _tsfn.Unref();
    }
}

public sealed class JSDispatcherSynchronizationContext : JSSynchronizationContext
{
    private readonly JSDispatcherQueue _queue;

    public JSDispatcherSynchronizationContext(JSDispatcherQueue queue)
    {
        _queue = queue;
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        if (IsDisposed) return;

        _queue.TryEnqueue(() => callback(state));
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        if (this == Current)
        {
            callback(state);
            return;
        }

        if (IsDisposed) return;

        using var syncEvent = new ManualResetEvent(initialState: false);
        bool isQueued = _queue.TryEnqueue(() =>
        {
            callback(state);
            syncEvent.Set();
        });

        if (isQueued)
        {
            syncEvent.WaitOne();
        }
    }

    public override void OpenAsyncScope() { }

    public override void CloseAsyncScope() { }
}
