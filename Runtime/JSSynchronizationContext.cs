using System;
using System.Threading;

namespace NodeApi;

public class JSSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly JSThreadSafeFunction _tsfn;
    private readonly SynchronizationContext? _previousSyncContext;

    public bool IsDisposed { get; private set; }

    public JSSynchronizationContext()
    {
        _tsfn = JSThreadSafeFunction.New(
            maxQueueSize: 0,
            initialThreadCount: 1,
            asyncResourceName: (JSValue)"SynchronizationContext");

        _previousSyncContext = Current;
        SetSynchronizationContext(this);
    }

    ~JSSynchronizationContext()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public override void Post(SendOrPostCallback callback, object? state)
    {
        if (!IsDisposed)
        {
            _tsfn.NonBlockingCall(() => callback(state));
        }
    }

    public override void Send(SendOrPostCallback callback, object? state)
    {
        if (this == Current)
        {
            callback(state);
        }
        else
        {
            Post(callback, state);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            if (disposing)
            {
                SetSynchronizationContext(_previousSyncContext);
            }

            _tsfn.Release();
        }
    }
}
