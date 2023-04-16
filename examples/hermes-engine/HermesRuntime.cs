// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Interop;
using static Hermes.Example.HermesApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Hermes.Example;

public sealed class HermesRuntime : IDisposable
{
    private object _runtimeMutex = new();
    private hermes_runtime _runtime;
    private JSDispatcherQueue _dispatcherQueue;
    private JSValueScope _rootScope;
    private TaskCompletionSource? _onRunFinish;
    private readonly TaskCompletionSource _onClose = new();
    private readonly Dictionary<int, ImmediateTask> _immediateTasks = new();
    private readonly Dictionary<int, TimerTask> _timerTasks = new();
    private int _nextTaskId;
    private bool _shouldClose;

    public bool IsDisposed { get; private set; }

    private HermesRuntime(JSDispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;
        HermesApi.Load("hermes.dll");
        using HermesConfig tempConfig = new();
        hermes_create_runtime((hermes_config)tempConfig, out _runtime).ThrowIfFailed();
        _rootScope = new JSValueScope(JSValueScopeType.Root, (napi_env)this);
        CreatePolyfills();
    }

    public static Task<HermesRuntime> Create(JSDispatcherQueue dispatcherQueue)
    {
        var taskPromise = new TaskCompletionSource<HermesRuntime>();
        dispatcherQueue.TryEnqueue(() =>
        {
            taskPromise.TrySetResult(new HermesRuntime(dispatcherQueue));
        });
        return taskPromise.Task;
    }

    public Task RunAsync(Action action)
    {
        Task? result = null;
        lock (_runtimeMutex)
        {
            VerifyElseThrow(_onRunFinish == null, "Previous run is not finished");
            _onRunFinish = new TaskCompletionSource();
            result = _onRunFinish.Task;
        }

        _dispatcherQueue.TryEnqueue(() =>
        {
            action();
            TryFinishRun();
        });

        return result;
    }

    public Task CloseAsync()
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            _shouldClose = true;
            TryClose();
        });
        return _onClose.Task;
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        VerifyElseThrow(JSDispatcherQueue.GetForCurrentThread() == _dispatcherQueue);
        IsDisposed = true;
        _rootScope.Dispose();
        hermes_delete_runtime(_runtime).ThrowIfFailed();
    }

    public static explicit operator hermes_runtime(HermesRuntime value) => value._runtime;

    public static explicit operator napi_env(HermesRuntime value)
    {
        VerifyElseThrow(JSDispatcherQueue.GetForCurrentThread() == value._dispatcherQueue);
        return hermes_get_node_api_env((hermes_runtime)value, out napi_env env).ThrowIfFailed(env);
    }

    public void CreatePolyfills()
    {
        VerifyElseThrow(JSDispatcherQueue.GetForCurrentThread() == _dispatcherQueue);
        using var scope = new JSValueScope();

        // Add global
        JSValue global = JSValue.Global;
        global["global"] = global;

        global["setImmediate"] = (JSCallback)(args =>
        {
            JSValue immediateCallback = args[0];
            VerifyElseThrow(immediateCallback.TypeOf() == JSValueType.Function,
                "Wrong type of args[0]. Expects a function.");

            int taskId = AddImmediateTask(immediateCallback);
            return taskId;
        });

        global["clearImmediate"] = (JSCallback)(args =>
        {
            RemoveImmediateTask((int)args[0]);
            return default;
        });

        global["setTimeout"] = (JSCallback)(args =>
        {
            JSValue timeoutCallback = args[0];
            VerifyElseThrow(timeoutCallback.TypeOf() == JSValueType.Function,
                "Wrong type of args[0]. Expects a function.");

            int taskId = AddTimerTask(timeoutCallback, (int)args[1]);
            return taskId;
        });

        global["clearTimeout"] = (JSCallback)(args =>
        {
            RemoveTimerTask((int)args[0]);
            return default;
        });

        var console = new JSObject();
        console["log"] = (JSCallback)(args =>
        {
            Console.WriteLine((string)args[0]);
            return default;
        });
        global["console"] = console;
    }

    private int AddImmediateTask(JSValue callback)
    {
        VerifyElseThrow(JSDispatcherQueue.GetForCurrentThread() == _dispatcherQueue);
        int taskId = ++_nextTaskId;
        var task = new ImmediateTask(this, callback, () => RemoveImmediateTask(taskId));
        if (_dispatcherQueue.TryEnqueue(task.Run))
        {
            _immediateTasks[taskId] = task;
            return taskId;
        }

        return 0;
    }

    private int AddTimerTask(JSValue callback, int timeout)
    {
        VerifyElseThrow(JSDispatcherQueue.GetForCurrentThread() == _dispatcherQueue);
        int taskId = ++_nextTaskId;
        JSDispatcherQueueTimer timer = _dispatcherQueue.CreateTimer();
        var task = new TimerTask(this, timer, callback, () => RemoveTimerTask(taskId));
        _timerTasks[taskId] = task;
        timer.IsRepeating = false;
        timer.Interval = new TimeSpan(0, 0, 0, 0, timeout);
        timer.Tick += task.OnTimerTick;
        timer.Start();
        return taskId;
    }

    private void RemoveImmediateTask(int taskId)
    {
        VerifyElseThrow(JSDispatcherQueue.GetForCurrentThread() == _dispatcherQueue);
        if (_immediateTasks.TryGetValue(taskId, out ImmediateTask? task))
        {
            _immediateTasks.Remove(taskId);
            task.Dispose();
        }

        TryFinishRun();
    }

    private void RemoveTimerTask(int taskId)
    {
        VerifyElseThrow(JSDispatcherQueue.GetForCurrentThread() == _dispatcherQueue);
        if (_timerTasks.TryGetValue(taskId, out TimerTask? task))
        {
            _timerTasks.Remove(taskId);
            task.Dispose();
        }

        TryFinishRun();
    }

    private void TryFinishRun()
    {
        VerifyElseThrow(JSDispatcherQueue.GetForCurrentThread() == _dispatcherQueue);
        if (_immediateTasks.Count == 0 && _timerTasks.Count == 0)
        {
            TaskCompletionSource? onRunFinish = null;
            lock (_runtimeMutex)
            {
                onRunFinish = _onRunFinish;
                _onRunFinish = null;
            }
            onRunFinish?.TrySetResult();
            TryClose();
        }
    }

    private void TryClose()
    {
        VerifyElseThrow(JSDispatcherQueue.GetForCurrentThread() == _dispatcherQueue);
        if (_shouldClose && _immediateTasks.Count == 0 && _timerTasks.Count == 0)
        {
            Dispose();
            _onClose.TrySetResult();
        }
    }

    private static void VerifyElseThrow(bool condition, string? message = null)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private class ImmediateTask : IDisposable
    {
        private HermesRuntime _runtime;
        private JSReference _callback;
        private Action _onFinalize;

        public bool IsDisposed { get; private set; }

        public ImmediateTask(HermesRuntime runtime, JSValue callback, Action onFinalize)
        {
            _runtime = runtime;
            _callback = new JSReference(callback);
            _onFinalize = onFinalize;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _callback.Dispose();
            _onFinalize();
        }

        public void Run()
        {
            if (_callback.IsDisposed) return;
            using var asyncScope = new JSAsyncScope();
            JSValue? callback = _callback.GetValue();
            callback?.Call();
            Dispose();
        }
    }

    private class TimerTask
    {
        private HermesRuntime _runtime;
        private JSDispatcherQueueTimer _timer;
        private JSReference _callback;
        private Action _onFinalize;

        public bool IsDisposed { get; private set; }

        public TimerTask(
            HermesRuntime runtime,
            JSDispatcherQueueTimer timer,
            JSValue callback,
            Action onFinalize)
        {
            _runtime = runtime;
            _timer = timer;
            _callback = new JSReference(callback);
            _onFinalize = onFinalize;
        }

        public void Dispose()
        {
            if (IsDisposed) return;
            IsDisposed = true;
            _callback.Dispose();
            _timer.Stop();
            _onFinalize();
        }

        public void OnTimerTick(object? _, EventArgs e)
        {
            if (IsDisposed) return;
            using var asyncScope = new JSAsyncScope();
            JSValue? callback = _callback.GetValue();
            callback?.Call();
            Dispose();
        }
    }
}
