using System;
using System.Threading;

namespace NodeApi
{
    /// <summary
    /// Keeps JSSynchronizationContext queue alive until the end of the 
    public class JSAsyncScope : IDisposable
    {
        private readonly SynchronizationContext? _previousSyncContext;
        private readonly JSSynchronizationContext _syncContext;

        public bool IsDisposed { get; private set; }

        public JSAsyncScope() : this(JSSynchronizationContext.Current
            ?? throw new InvalidOperationException("JSSynchronizationContext is not found in current thread."))
        {
        }

        public JSAsyncScope(JSSynchronizationContext synchronizationContext)
        {
            _previousSyncContext = SynchronizationContext.Current;
            _syncContext = synchronizationContext;
            SynchronizationContext.SetSynchronizationContext(_syncContext);
            _syncContext.OpenAsyncScope();
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                JSSynchronizationContext? syncContext = JSSynchronizationContext.Current;
                if (syncContext is null)
                {
                    throw new InvalidOperationException("JSSynchronizationContext is not found in current thread.");
                }

                if (_syncContext != syncContext)
                {
                    throw new InvalidOperationException("Mismatched JSSynchronizationContext.");
                }

                SynchronizationContext.SetSynchronizationContext(_previousSyncContext);
                syncContext.CloseAsyncScope();
            }
        }
    }
}
