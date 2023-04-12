// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.Engines;

/// <summary>
/// A Node.js runtime environment with a dedicated main execution thread.
/// </summary>
/// <remarks>
/// Multiple Node.js environments may be created (concurrently) in the same process. Each
/// environment instance has its own dedicated execution thread. Except where otherwise documented,
/// all interaction with the environment and JavaScript values associated with the environment MUST
/// be executed on the environment's thread. Use the
/// <see cref="NodejsEnvironment.SynchronizationContext" /> to switch to the thread.
/// </remarks>
public sealed class NodejsEnvironment : IDisposable
{
    private readonly JSValueScope _scope;
    private readonly Thread _thread;
    private readonly TaskCompletionSource<bool> _completion = new();

    public static explicit operator napi_env(NodejsEnvironment environment) =>
        (napi_env)environment._scope;
    public static implicit operator JSValueScope(NodejsEnvironment environment) =>
        environment._scope;

    internal NodejsEnvironment(NodejsPlatform platform, string? mainScript)
    {
        JSValueScope scope = null!;
        JSSynchronizationContext syncContext = null!;
        using ManualResetEvent loadedEvent = new(false);

        _thread = new(() =>
        {
            napi_env env = JSNativeApi.CreateEnvironment(
                (napi_platform)platform, (error) => Console.WriteLine(error), mainScript);

            // The new scope instance saves itself as the thread-local JSValueScope.Current.
            scope = new JSValueScope(JSValueScopeType.Root, env);

            syncContext = JSSynchronizationContext.Create();
            System.Threading.SynchronizationContext.SetSynchronizationContext(syncContext);

            loadedEvent.Set();

            // Run the JS engine until disposal completes the completion source.
            JSNativeApi.AwaitPromise(_completion.Task.AsPromise());

            syncContext.Dispose();
            ExitCode = JSNativeApi.DestroyEnvironment(env);
        });
        _thread.Start();

        loadedEvent.WaitOne();

        _scope = scope;
        SynchronizationContext = syncContext;
    }

    /// <summary>
    /// Gets a synchronization context that enables switching to the Node.js environment's thread
    /// and returning to the thread after an `await`.
    /// </summary>
    /// <remarks>
    /// Except where otherwise documented, all interaction with the environment and JavaScript
    /// values associated with the environment MUST be executed on the environment's thread.
    /// </remarks>
    /// <seealso cref="JSSynchronizationContext.Post(Action, bool) "/>
    /// <seealso cref="JSSynchronizationContext.Post(Func{Task}, bool)"/>
    /// <seealso cref="JSSynchronizationContext.Run(Action)"/>
    /// <seealso cref="JSSynchronizationContext.Run{T}(Func{T})"/>
    /// <seealso cref="JSSynchronizationContext.RunAsync(Func{Task})"/>
    /// <seealso cref="JSSynchronizationContext.RunAsync{T}(Func{Task{T}})"/>
    public JSSynchronizationContext SynchronizationContext { get; }

    /// <summary>
    /// Gets the exit code returned by the environment after it is disposed, or null if the
    /// environment is not disposed.
    /// </summary>
    public int? ExitCode { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the Node.js environment is disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Disposes the Node.js environment, causing its main thread to exit.
    /// </summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        // Setting the completion causes `AwaitPromise()` to return so the thread exits.
        _completion.TrySetResult(true);
        _thread.Join();

        Debug.WriteLine($"Node.js environment exited with code: {ExitCode}");
    }

    public Uri StartInspector(int? port = null, string? host = null, bool? wait = null)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEnvironment));

        return SynchronizationContext.Run(() =>
        {
            JSValue inspector = JSValue.Global["require"].Call(JSValue.Undefined, "node:inspector");
            inspector.CallMethod(
                "open",
                port != null ? (JSValue)port : JSValue.Undefined,
                host != null ? (JSValue)host : JSValue.Undefined,
                wait ?? false);
            return new Uri((string)inspector.CallMethod("url"));
        });
    }

    public void StopInspector()
    {
        if (IsDisposed) return;

        SynchronizationContext.Run(() =>
        {
            JSValue inspector = JSValue.Global["require"].Call(JSValue.Undefined, "node:inspector");
            inspector.CallMethod("close");
        });
    }

    /// <summary>
    /// Event args for an unhandled promise rejection in a Node.js environment.
    /// </summary>
    /// <seealso cref="UnhandledPromiseRejection" />
    public class UnhandledPromiseRejectionEventArgs : EventArgs
    {
        public JSValue Error { get; set; }
    }

    private EventHandler<UnhandledPromiseRejectionEventArgs>? _unhandledPromiseRejection;
    private JSReference? _unhandledPromiseRejectionListener;

    /// <summary>
    /// Event raised when there is an unhandled promise rejection in the Node.js environment.
    /// </summary>
    /// <remarks>
    /// Event-handlers may be added or removed from any thread, however all events are raised
    /// on the environment thread.
    /// </remarks>
    public event EventHandler<UnhandledPromiseRejectionEventArgs>? UnhandledPromiseRejection
    {
        add
        {
            if (IsDisposed) return;

            if (_unhandledPromiseRejection == null)
            {
                SynchronizationContext.Run(AddUnhandledPromiseRejectionListener);
            }

            _unhandledPromiseRejection += value;
        }
        remove
        {
            if (IsDisposed) return;

            _unhandledPromiseRejection -= value;

            if (_unhandledPromiseRejection == null)
            {
                SynchronizationContext.Run(RemoveUnhandledPromiseRejectionListener);
            }
        }
    }

    private void AddUnhandledPromiseRejectionListener()
    {
        JSValue listener = JSValue.CreateFunction(
            "unhandledRejection", OnUnhandledPromiseRejection);
        _unhandledPromiseRejectionListener = new JSReference(listener);
        JSValue.Global["process"].CallMethod("on", "unhandledRejection", listener);
    }

    private void RemoveUnhandledPromiseRejectionListener()
    {
        JSValue listener = _unhandledPromiseRejectionListener!.GetValue()!.Value;
        JSValue.Global["process"].CallMethod("off", "unhandledRejection", listener);
        _unhandledPromiseRejectionListener.Dispose();
        _unhandledPromiseRejectionListener = null;
    }

    private JSValue OnUnhandledPromiseRejection(JSCallbackArgs args)
    {
        _unhandledPromiseRejection?.Invoke(this, new UnhandledPromiseRejectionEventArgs
        {
            Error = args[0],
        });
        return JSValue.Undefined;
    }
}
