// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
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

    public void Post(Action action, bool allowSync = false)
    {
        if (allowSync && Current == this)
        {
            action();
        }
        else
        {
            Post(_ =>
            {
                if (IsDisposed) return;
                action();
            }, null);
        }
    }

    public void Post(Func<Task> asyncAction, bool allowSync = false)
    {
        if (allowSync && Current == this)
        {
            _ = asyncAction();
        }
        else
        {
            Post((_) =>
            {
                if (IsDisposed) return;
                _ = asyncAction();
            }, null);
        }
    }

    public void Run(Action action)
    {
        if (Current == this)
        {
            action();
        }
        else
        {
            Exception? exception = null;
            Send((_) =>
            {
                if (IsDisposed) return;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }, null);
            if (exception != null)
            {
                throw new JSException("Exception thrown from JS thread.", exception);
            }
        }
    }

    public T Run<T>(Func<T> action)
    {
        if (Current == this)
        {
            return action();
        }
        else
        {
            T result = default!;
            Exception? exception = null;
            Send((_) =>
            {
                if (IsDisposed) return;
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            }, null);
            if (exception != null)
            {
                throw new JSException("Exception thrown from JS thread.", exception);
            }
            return result;
        }
    }

    public Task RunAsync(Func<Task> asyncAction)
    {
        if (Current == this)
        {
            return asyncAction();
        }
        else
        {
            TaskCompletionSource<bool> completion = new();
            Send(async (_) =>
            {
                if (IsDisposed) return;
                try
                {
                    await asyncAction();
                    completion.SetResult(true);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }, null);
            return completion.Task;
        }
    }

    public Task<T> RunAsync<T>(Func<Task<T>> asyncAction)
    {
        if (Current == this)
        {
            return asyncAction();
        }
        else
        {
            TaskCompletionSource<T> completion = new();
            Send(async (_) =>
            {
                if (IsDisposed) return;
                try
                {
                    T result = await asyncAction();
                    completion.SetResult(result);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }, null);
            return completion.Task;
        }
    }
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
