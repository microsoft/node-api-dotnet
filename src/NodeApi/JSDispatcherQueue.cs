// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi;

public sealed class JSDispatcherQueueController
{
    public JSDispatcherQueue DispatcherQueue { get; } = new();

    private JSDispatcherQueueController() { }

    public static JSDispatcherQueueController CreateOnDedicatedThread()
    {
        var controller = new JSDispatcherQueueController();
        var thread = new Thread(controller.DispatcherQueue.Run);
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

public sealed class JSDispatcherQueue
{
    private readonly object _queueMutex = new();
    private List<Action> _taskQueue = new();
    private readonly List<JSDispatcherQueueTimer.Job> _timerJobs = new();
    private TaskCompletionSource<int>? _onShutdownCompleted;
    private int _threadId;
    private int _deferralCount = 1; // Set initial deferral count to 1 to defer shutdown
                                    // while we run ShutdownStarting event handlers.
    private bool _hasStoppedEnqueueing;

    [ThreadStatic]
    private static JSDispatcherQueue? s_currentQueue;

    public event EventHandler<JSDispatcherQueueShutdownStartingEventArgs>? ShutdownStarting;

    public event EventHandler? ShutdownCompleted;

    public bool HasThreadAccess => _threadId == Environment.CurrentManagedThreadId;

    public static JSDispatcherQueue? GetForCurrentThread() => s_currentQueue;

    public bool TryEnqueue(Action callback)
    {
        lock (_queueMutex)
        {
            return TryEnqueueInternal(callback);
        }
    }

    public JSDispatcherQueueTimer CreateTimer() => new(this);

    // Run the thread function.
    internal void Run()
    {
        using var currentQueueHolder = new CurrentQueueHolder(this);

        // The tasks to process outside of lock.
        List<Action> localTaskQueue = new();

        // Loop until the shutdown completion breaks out of it.
        while (true)
        {
            // Invoke tasks from the local task reader queue outside of lock.
            foreach (Action task in localTaskQueue)
            {
                task();
            }

            // All tasks are completed. Clear the queue.
            localTaskQueue.Clear();

            // Under lock see if we have more tasks, complete shutdown, or start waiting.
            lock (_queueMutex)
            {
                // See if must run timer jobs.
                // The timer jobs are sorted in reverse order so that we can remove them from
                // the end of list and avoid the list shifting.
                DateTime now = DateTime.Now;
                // The timeout that we use later to wait for new tasks.
                TimeSpan waitTimeout = Timeout.InfiniteTimeSpan;
                for (int i = _timerJobs.Count - 1; i >= 0; i--)
                {
                    JSDispatcherQueueTimer.Job timerJob = _timerJobs[i];
                    if (now >= timerJob.TickTime)
                    {
                        _timerJobs.RemoveAt(i);
                        localTaskQueue.Add(timerJob.Invoke);
                    }
                    else
                    {
                        // The wait timeout for the next timer job activation.
                        waitTimeout = timerJob.TickTime - now;
                        break;
                    }
                }

                if (localTaskQueue.Count == 0)
                {
                    // No timer jobs to run. See if tasks were enqueued. Swap the queues.
                    (localTaskQueue, _taskQueue) = (_taskQueue, localTaskQueue);
                }

                if (localTaskQueue.Count > 0)
                {
                    // We have more work to do. Start the loop from the beginning.
                    continue;
                }

                if (_hasStoppedEnqueueing)
                {
                    // Complete the shutdown: the shutdown is already started,
                    // there are no deferrals, and all work is completed.
                    break;
                }

                // Wait for more tasks to come.
                Monitor.Wait(_queueMutex, waitTimeout);
            }
        }

        // Notify about the shutdown completion.
        ShutdownCompleted?.Invoke(this, EventArgs.Empty);
        _onShutdownCompleted?.SetResult(0);
    }

    internal bool TryEnqueueInternal(Action callback)
    {
        ValidateLock();
        if (_hasStoppedEnqueueing)
        {
            return false;
        }

        _taskQueue.Add(callback);
        Monitor.PulseAll(_queueMutex);
        return true;
    }

    internal void AddTimerJob(JSDispatcherQueueTimer.Job timerJob)
    {
        ValidateNoLock();
        if (timerJob.IsCanceled) return;

        // See if we can invoke it immediately.
        if (timerJob.TickTime <= DateTime.Now)
        {
            timerJob.Invoke();
            return;
        }

        lock (_queueMutex)
        {
            // Schedule for future invocation.
            int index = _timerJobs.BinarySearch(timerJob);
            // If the index negative, then it is a bitwise complement of
            // the suggested insertion index.
            if (index < 0) index = ~index;
            _timerJobs.Insert(index, timerJob);
        }
    }

    internal void Shutdown(TaskCompletionSource<int> completion)
    {
        bool isShutdownEnqueued = TryEnqueue(() =>
        {
            if (_onShutdownCompleted != null)
            {
                throw new InvalidOperationException("The shutdown is already started.");
            }

            _onShutdownCompleted = completion;
            ShutdownStarting?.Invoke(
                this, new JSDispatcherQueueShutdownStartingEventArgs(CreateDeferral));
            DecrementDeferralCount(); // Decrement the initial _deferralCount == 1.
        });

        if (!isShutdownEnqueued)
        {
            throw new InvalidOperationException("Shutdown was already completed.");
        }
    }

    internal void InvokeUnderLock(Action action)
    {
        lock (_queueMutex)
        {
            action();
        }
    }

    internal void ValidateLock()
    {
        if (!Monitor.IsEntered(_queueMutex))
        {
            throw new InvalidOperationException("_queueMutex must be locked");
        }
    }

    internal void ValidateNoLock()
    {
        if (Monitor.IsEntered(_queueMutex))
        {
            throw new InvalidOperationException("_queueMutex must not be locked");
        }
    }

    private Deferral CreateDeferral()
    {
        IncrementDeferralCount();
        return new Deferral(DecrementDeferralCount);
    }

    private void IncrementDeferralCount()
    {
        lock (_queueMutex)
        {
            if (_deferralCount == 0)
            {
                throw new InvalidOperationException(
                    "Deferral can only be taken in the ShutdownStarting event handler.");
            }

            _deferralCount++;
        }
    }

    private void DecrementDeferralCount()
    {
        lock (_queueMutex)
        {
            if (_deferralCount == 0)
            {
                throw new InvalidOperationException(
                    "Unbalanced deferral count decrement.");
            }

            if (--_deferralCount == 0)
            {
                _hasStoppedEnqueueing = true;
            }
        }
    }

    private readonly struct CurrentQueueHolder : IDisposable
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

    private sealed class Deferral : IDisposable
    {
        private bool _isDisposed;
        private readonly Action _completionHandler;

        public Deferral(Action completionHandler)
            => _completionHandler = completionHandler;

        ~Deferral()
        {
            Dispose(false);
        }

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
}

public sealed class JSDispatcherQueueTimer
{
    private readonly JSDispatcherQueue _queue;
    private TimeSpan _interval;
    private bool _isRepeating = true;
    private Job? _currentJob;

    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            _queue.InvokeUnderLock(() =>
            {
                if (_interval == value) return;
                _interval = value;
                RestartInternal();
            });
        }
    }

    public bool IsRepeating
    {
        get => _isRepeating;
        set
        {
            _queue.InvokeUnderLock(() =>
            {
                if (_isRepeating == value) return;
                _isRepeating = value;
                RestartInternal();
            });
        }
    }

    public bool IsRunning => _currentJob != null;

    public event EventHandler? Tick;

    public JSDispatcherQueueTimer(JSDispatcherQueue queue) => _queue = queue;

    public void Start() => _queue.InvokeUnderLock(StartInternal);

    public void Stop() => _queue.InvokeUnderLock(StopInternal);

    private void StartInternal()
    {
        _queue.ValidateLock();
        if (_currentJob != null) return;
        if (Tick == null) return;

        var timerJob = new Job(this, DateTime.Now + Interval, Tick);
        // We always enqueue new timer job to the queue as a normal task.
        // This way a timer job with a zero timeout will behave the same way as a normal task.
        if (_queue.TryEnqueueInternal(() => _queue.AddTimerJob(timerJob)))
        {
            _currentJob = timerJob;
        }
    }

    private void StopInternal()
    {
        _queue.ValidateLock();
        if (_currentJob == null) return;

        _currentJob.Cancel();
        _currentJob = null;
    }

    private void RestartInternal()
    {
        if (_currentJob == null) return;
        StopInternal();
        StartInternal();
    }

    private void CompleteJob(Job job)
    {
        _queue.InvokeUnderLock(() =>
        {
            if (_currentJob == job)
            {
                _currentJob = null;
            }

            if (IsRepeating)
            {
                StartInternal();
            }
        });
    }

    internal class Job : IComparable<Job>
    {
        public JSDispatcherQueueTimer Timer { get; }
        public DateTime TickTime { get; }
        public EventHandler Tick { get; }
        public bool IsCanceled { get; private set; }

        public Job(JSDispatcherQueueTimer timer, DateTime tickTime, EventHandler tick)
        {
            Timer = timer;
            TickTime = tickTime;
            Tick = tick;
        }

        public int CompareTo(Job? other)
        {
            if (other == null) return 1;
            // Sort in descending order where the timer jobs with lower time
            // appear in the end of the list. It is to optimize deletion from the job list.
            return -Comparer<DateTime>.Default.Compare(TickTime, other.TickTime);
        }

        public void Cancel() => IsCanceled = true;

        public void Invoke()
        {
            if (IsCanceled) return;
            Tick?.Invoke(Timer, EventArgs.Empty);
            Timer.CompleteJob(this);
        }
    }
}

public sealed class JSDispatcherQueueShutdownStartingEventArgs : EventArgs
{
    private readonly Func<IDisposable> _getDeferral;

    internal JSDispatcherQueueShutdownStartingEventArgs(Func<IDisposable> getDeferral)
        => _getDeferral = getDeferral;

    public IDisposable GetDeferral() => _getDeferral();
}
