// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Manages the synchronization context for a JavaScript environment, allowing callbacks and
/// asynchronous continuations to be invoked on the JavaScript thread that runs the environment.
/// </summary>
/// <remarks>
/// All JavaScript values are bound to the thread that runs the JS environment and can only be
/// accessed from the same thread. Attempts to access a JavaScript value from a different thread
/// will throw <see cref="JSInvalidThreadAccessException" />.
/// <para/>
/// Use of <see cref="Task.ConfigureAwait(bool)"/> with <c>continueOnCapturedContext:false</c>
/// can prevent execution from returning to the JS thread, though it isn't necessarily a problem
/// as long as there is a top-level continuation that uses <c>continueOnCapturedContext:true</c>
/// (the default) to return to the JS thread.
/// <para/>
/// Code that makes explicit use of .NET threads or thread pools may need to capture the
/// <see cref="JSSynchronizationContext.Current" /> context (before switching off the JS thread)
/// and hold it for later use to call back to JS via
/// <see cref="JSSynchronizationContext.Post(Action, bool)"/>,
/// <see cref="JSSynchronizationContext.Run"/>, or <see cref="JSSynchronizationContext.RunAsync"/>.
/// </remarks>
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

    /// <summary>
    /// Runs an action on the JS thread, without waiting for completion.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <param name="allowSync">True to allow the action to run immediately if the current
    /// synchronization context is this one. By default the action will always be scheduled
    /// for later execution.
    /// </param>
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

    /// <summary>
    /// Runs an asynchronous action on the JS thread, without waiting for completion.
    /// </summary>
    /// <param name="asyncAction">The action to run.</param>
    /// <param name="allowSync">True to allow the action to run immediately if the current
    /// synchronization context is this one. By default the action will always be scheduled
    /// for later execution.
    /// </param>
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

    /// <summary>
    /// Runs an action on the JS thread, and waits for completion.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <exception cref="JSException">Any exception thrown by the action is wrapped in a
    /// JS exception. The original exception is available via the
    /// <see cref="Exception.InnerException" /> property.</exception>
    public void Run(Action action)
    {
        if (Current == this)
        {
            action();
        }
        else
        {
            JSException? exception = null;
            Send((_) =>
            {
                if (IsDisposed) return;
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    exception = new JSException(ex);
                }
            }, null);
            if (exception != null)
            {
                throw exception;
            }
        }
    }

    /// <summary>
    /// Runs an action on the JS thread, and waits for the return value.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <exception cref="JSException">Any exception thrown by the action is wrapped in a
    /// JS exception. The original exception is available via the
    /// <see cref="Exception.InnerException" /> property.</exception>
    public T Run<T>(Func<T> action)
    {
        if (Current == this)
        {
            return action();
        }
        else
        {
            T result = default!;
            JSException? exception = null;
            Send((_) =>
            {
                if (IsDisposed) return;
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    exception = new JSException(ex);
                }
            }, null);
            if (exception != null)
            {
                throw exception;
            }
            return result;
        }
    }

    /// <summary>
    /// Runs an action on the JS thread, and asynchronously waits for completion.
    /// </summary>
    /// <param name="asyncAction">The action to run.</param>
    public Task RunAsync(Func<Task> asyncAction)
    {
        if (Current == this)
        {
            return asyncAction();
        }
        else
        {
            TaskCompletionSource<bool> completion = new();
            Post(async (_) =>
            {
                if (IsDisposed) return;
                try
                {
                    await asyncAction();
                    completion.SetResult(true);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(new JSException(ex));
                }
            }, null);
            return completion.Task;
        }
    }

    /// <summary>
    /// Runs an action on the JS thread, and asynchronously waits for the return value.
    /// </summary>
    /// <param name="asyncAction">The action to run.</param>
    public Task<T> RunAsync<T>(Func<Task<T>> asyncAction)
    {
        if (Current == this)
        {
            return asyncAction();
        }
        else
        {
            TaskCompletionSource<T> completion = new();
            Post(async (_) =>
            {
                if (IsDisposed) return;
                try
                {
                    T result = await asyncAction();
                    completion.SetResult(result);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(new JSException(ex));
                }
            }, null);
            return completion.Task;
        }
    }
}

internal sealed class JSTsfnSynchronizationContext : JSSynchronizationContext
{
    private readonly JSThreadSafeFunction _tsfn;

    public JSTsfnSynchronizationContext()
    {
        _tsfn = new JSThreadSafeFunction(
            maxQueueSize: 0,
            initialThreadCount: 1,
            asyncResourceName: (JSValue)nameof(JSSynchronizationContext));

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

internal sealed class JSDispatcherSynchronizationContext : JSSynchronizationContext
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
