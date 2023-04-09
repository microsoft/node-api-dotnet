// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Hermes.Example;

public delegate void JSTypedEventHandler<TSender, TResult>(TSender sender, TResult args);

public sealed class DispatcherQueueShutdownStartingEventArgs : EventArgs
{
    private Func<JSDispatcherQueueDeferral> _getDeferral;

    internal DispatcherQueueShutdownStartingEventArgs(Func<JSDispatcherQueueDeferral> getDeferral)
        => _getDeferral = getDeferral;

    public JSDispatcherQueueDeferral GetDeferral() => _getDeferral();
}

public sealed class JSDispatcherQueue
{
    private readonly object _queueMutex = new();
    private List<Action?> _writerQueue = new(); // Queue to add new items
    private List<Action?> _readerQueue = new(); // Queue to read items from
    private TaskCompletionSource<int>? _onShutdownCompleted;
    private int _threadId;
    private int _deferralCount;
    private bool _isShutdownCompleted;

    [ThreadStatic]
    private static JSDispatcherQueue? s_currentQueue;

    public event JSTypedEventHandler<JSDispatcherQueue, object?>? ShutdownCompleted;
    public event JSTypedEventHandler<JSDispatcherQueue, DispatcherQueueShutdownStartingEventArgs>?
        ShutdownStarting;

    public bool HasThreadAccess => _threadId == Environment.CurrentManagedThreadId;

    public static JSDispatcherQueue? GetForCurrentThread() => s_currentQueue;

    public bool TryEnqueue(Action callback)
    {
        lock (_queueMutex)
        {
            if (_isShutdownCompleted)
            {
                return false;
            }

            _writerQueue.Add(callback);
            Monitor.PulseAll(_queueMutex);
        }

        return true;
    }

    // Run the thread function.
    internal void Run()
    {
        using var currentQueueHolder = new CurrentQueueHolder(this);

        // Loop until the shutdown completion breaks out of it.
        while (true)
        {
            // Invoke tasks from reader queue outside of lock.
            // The reader queue is only accessible from this thread.
            for (int i = 0; i < _readerQueue.Count; i++)
            {
                _readerQueue[i]?.Invoke();
                _readerQueue[i] = null;
            }

            // All tasks are completed. Clear the queue.
            _readerQueue.Clear();

            // Under lock see if we have more tasks, complete shutdown, or start waiting.
            lock (_queueMutex)
            {
                // Swap reader and writer queues.
                (_readerQueue, _writerQueue) = (_writerQueue, _readerQueue);

                if (_readerQueue.Count > 0)
                {
                    // We have more work to do. Start the loop from the beginning.
                    continue;
                }

                if (_onShutdownCompleted != null && _deferralCount == 0)
                {
                    // Complete the shutdown: the shutdown is already started,
                    // there are no deferrals, and all work is completed.
                    _isShutdownCompleted = true;
                    break;
                }

                // Wait for more work to come.
                Monitor.Wait(_queueMutex);
            }
        }

        // Notify about the shutdown completion.
        ShutdownCompleted?.Invoke(this, null);
        _onShutdownCompleted.SetResult(0);
    }

    // Create new Deferral and increment deferral count.
    internal JSDispatcherQueueDeferral CreateDeferral()
    {
        lock (_queueMutex)
        {
            _deferralCount++;
        }

        return new JSDispatcherQueueDeferral(() =>
        {
            // Decrement deferral count upon deferral completion.
            TryEnqueue(() => _deferralCount--);
        });
    }

    internal void Shutdown(TaskCompletionSource<int> completion)
    {
        // Try to start the shutdown process.
        bool isShutdownStarted = TryEnqueue(() =>
        {
            if (_onShutdownCompleted != null)
            {
                // The shutdown is already started. Subscribe to its completion.
                ShutdownCompleted += (_, _) => completion.SetResult(0);
                return;
            }

            // Start the shutdown process.
            _onShutdownCompleted = completion;
            ShutdownStarting?.Invoke(
                this, new DispatcherQueueShutdownStartingEventArgs(() => CreateDeferral()));
        });

        if (!isShutdownStarted)
        {
            // The shutdown was already completed.
            completion.SetResult(0);
        }
    }

    private struct CurrentQueueHolder : IDisposable
    {
        private readonly JSDispatcherQueue? _previousCurrentQueue;

        public CurrentQueueHolder(JSDispatcherQueue queue)
        {
            _previousCurrentQueue = s_currentQueue;
            s_currentQueue = queue;
            queue._threadId = Environment.CurrentManagedThreadId;
        }

        public void Dispose()
        {
            if (s_currentQueue != null)
            {
                s_currentQueue._threadId = default;
            }

            s_currentQueue = _previousCurrentQueue;
        }
    }
}


public class JSDispatcherQueueController
{
    public JSDispatcherQueue DispatcherQueue { get; } = new();

    public static JSDispatcherQueueController CreateOnDedicatedThread()
    {
        var controller = new JSDispatcherQueueController();
        JSDispatcherQueue queue = controller.DispatcherQueue;
        var thread = new Thread(() => queue.Run());
        thread.Start();
        return controller;
    }

    public Task ShutdownQueueAsync()
    {
        var completion = new TaskCompletionSource<int>();
        DispatcherQueue.Shutdown(completion);
        return completion.Task;
    }
}

public sealed class JSDispatcherQueueDeferral : IDisposable
{
    private bool _isDisposed;
    private Action _completionHandler;

    public JSDispatcherQueueDeferral(Action completionHandler)
        => _completionHandler = completionHandler;

    ~JSDispatcherQueueDeferral()
    {
        Dispose(false);
    }

    public void Complete() => Dispose();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool _)
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _completionHandler.Invoke();
    }
}
