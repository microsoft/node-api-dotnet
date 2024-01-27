// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Helps preventing Node.JS process from exiting while we execute C# async functions.
/// </summary>
/// <remarks>
/// Async function uses <c>await</c> keyword that splits it up into several sequentially executed tasks.
/// Each task can run in its own task scheduler.
/// For running tasks in Node.JS main loop we use <see cref="JSSynchronizationContext"/>.
/// In the following code we start and end async function execution in JSSynchornizationContext
/// if we start running it in the main loop thread:
///
/// <code>
/// public async void RunAsyncTask(JSDeferred deferred, int id)
/// {
///     // Capture current SynchornizationContext and run work in a background thread.
///     var data = await RetriveDataById(id);
///
///     // After exiting await we use captured SynchornizationContext to run remaining code in that context.
///     deferred.Resolve((JSValue)data.FullName);
/// }
/// </code>
///
/// The work in the background thread may take some time and Node.JS process can finish because it completed
/// all current tasks. It is not aware about us running important code in the background thread.
/// We must say to Node.JS that we plan to do some important work in its main loop after we finish
/// the background task. The <see cref="JSSynchronizationContext.OpenAsyncScope"/> and
/// <see cref="JSSynchronizationContext.CloseAsyncScope"/> can be used to do that:
///
/// <code>
/// public async void RunAsyncTask(JSDeferred deferred, int id)
/// {
///     // Ask Node.JS to keep process alive because we need its main loop.
///     JSSynchronizationContext.Current.OpenAsyncScope();
///
///     var data = await RetriveDataById(id);
///     deferred.Resolve((JSValue)data.FullName);
///
///     // Tell Node.JS that we finished using its main loop and Node.JS process can exit
///     // after completing current callback.
///     JSSynchronizationContext.Current.CloseAsyncScope();
/// }
/// </code>
///
/// Note that these two functions must be called in the main loop thread.
///
/// The <c>JSAsyncScope</c> is a convenience struct that calls these two functions for us
/// automatically in it constructor and <c>Dispose</c> method. We can rewrite the code above as:
///
/// <code>
/// public async void RunAsyncTask(JSDeferred deferred, int id)
/// {
///     // We must use 'using' keyword to call 'Dispose' in the end.
///     using var asyncScope = new JSAsyncScope();
///
///     var data = await RetriveDataById(id);
///     deferred.Resolve((JSValue)data.FullName);
/// }
/// </code>
///
/// </remarks>
public struct JSAsyncScope : IDisposable
{
    private readonly JSSynchronizationContext _syncContext;

    public bool IsDisposed { get; private set; } = false;

    public JSAsyncScope()
    {
        _syncContext = JSSynchronizationContext.Current
            ?? throw new InvalidOperationException("JSSynchronizationContext is not found in current thread.");
        _syncContext.OpenAsyncScope();
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        if (_syncContext != JSSynchronizationContext.Current)
        {
            throw new InvalidOperationException("Mismatched JSSynchronizationContext.");
        }

        _syncContext.CloseAsyncScope();
    }
}
