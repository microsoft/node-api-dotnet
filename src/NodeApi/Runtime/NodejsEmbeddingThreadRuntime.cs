// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.Runtime;

using static JSRuntime;

/// <summary>
/// A Node.js runtime environment with a dedicated main execution thread.
/// </summary>
/// <remarks>
/// Multiple Node.js environments may be created (concurrently) in the same process. Each
/// environment instance has its own dedicated execution thread. Except where otherwise documented,
/// all interaction with the environment and JavaScript values associated with the environment MUST
/// be executed on the environment's thread. Use the
/// <see cref="NodejsEmbeddingThreadRuntime.SynchronizationContext" /> to switch to the thread.
/// </remarks>
public sealed class NodejsEmbeddingThreadRuntime : IDisposable
{
    private readonly JSValueScope _scope;
    private readonly Thread _thread;
    private readonly JSThreadSafeFunction? _completion;

    public static explicit operator napi_env(NodejsEmbeddingThreadRuntime environment) =>
        (napi_env)environment._scope;
    public static implicit operator JSValueScope(NodejsEmbeddingThreadRuntime environment) =>
        environment._scope;

    internal NodejsEmbeddingThreadRuntime(
        NodejsEmbeddingPlatform platform,
        string? baseDir,
        NodejsEmbeddingRuntimeSettings? settings)
    {
        JSValueScope scope = null!;
        JSSynchronizationContext syncContext = null!;
        JSThreadSafeFunction? completion = null;
        using ManualResetEvent loadedEvent = new(false);

        _thread = new(() =>
        {
            using var runtime = new NodejsEmbeddingRuntime(platform, settings);
            // The new scope instance saves itself as the thread-local JSValueScope.Current.
            using var nodeApiScope = new NodejsEmbeddingNodeApiScope(runtime);

            completion = new JSThreadSafeFunction(
                maxQueueSize: 0,
                initialThreadCount: 1,
                asyncResourceName: (JSValue)nameof(NodejsEmbeddingThreadRuntime));

            scope = JSValueScope.Current;
            syncContext = scope.RuntimeContext.SynchronizationContext;

            if (!string.IsNullOrEmpty(baseDir))
            {
                JSValue.Global.SetProperty("__dirname", baseDir!);
                InitializeModuleImportFunctions(scope.RuntimeContext, baseDir!);
            }

            loadedEvent.Set();

            // Run the JS event loop until disposal unrefs the completion thread safe function.
            try
            {
                runtime.CompleteEventLoop();
                ExitCode = 0;
            }
            catch (Exception)
            {
                ExitCode = 1;
            }

            syncContext.Dispose();
        });
        _thread.Start();

        loadedEvent.WaitOne();

        _completion = completion;
        _scope = scope;
        SynchronizationContext = syncContext;
    }

    public static JSRuntime JSRuntime => NodejsEmbedding.JSRuntime;

    private static void InitializeModuleImportFunctions(
        JSRuntimeContext runtimeContext,
        string baseDir)
    {
        // The require function is available as a global in the embedding context.
        JSFunction originalRequire = (JSFunction)JSValue.Global["require"];
        JSReference originalRequireRef = new(originalRequire);
        JSFunction envRequire = new("require", (modulePath) =>
        {
            Debug.WriteLine($"require('{(string)modulePath}')");
            JSValue require = originalRequireRef.GetValue();
            JSValue resolvedPath = ResolveModulePath(require, modulePath, baseDir);
            return require.Call(thisArg: default, resolvedPath);
        });

        // Also set up a callback for require.resolve(), in case it is used by imported modules.
        JSValue requireObject = (JSValue)envRequire;
        requireObject["resolve"] = new JSFunction("resolve", (modulePath) =>
        {
            JSValue require = originalRequireRef.GetValue();
            return ResolveModulePath(require, modulePath, baseDir);
        });

        JSValue.Global.SetProperty("require", envRequire);
        runtimeContext.RequireFunction = envRequire;

        // The import keyword is not a function and is only available through use of an
        // external helper module.
#if NETFRAMEWORK || NETSTANDARD
        string assemblyLocation = new Uri(typeof(NodejsEmbeddingThreadRuntime).Assembly.CodeBase).LocalPath;
#else
#pragma warning disable IL3000 // Assembly.Location returns an empty string for assemblies embedded in a single-file app
        string assemblyLocation = typeof(NodejsEmbeddingThreadRuntime).Assembly.Location;
#pragma warning restore IL3000
#endif
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            string importAdapterModulePath = Path.Combine(
                Path.GetDirectoryName(assemblyLocation)!, "import.cjs");
            if (File.Exists(importAdapterModulePath))
            {
                JSFunction originalImport = (JSFunction)originalRequire.CallAsStatic(
                    importAdapterModulePath);
                JSReference originalImportRef = new(originalImport);
                JSFunction envImport = new("import", (modulePath) =>
                {
                    JSValue require = originalRequireRef.GetValue();
                    JSValue resolvedPath = ResolveModulePath(require, modulePath, baseDir);
                    JSValue moduleUri = "file://" + (string)resolvedPath;
                    JSValue import = originalImportRef.GetValue();
                    return import.Call(thisArg: default, moduleUri);
                });

                JSValue.Global.SetProperty("import", envImport);
                runtimeContext.ImportFunction = envImport;
            }
        }
    }

    /// <summary>
    /// Use the require.resolve() function with an explicit base directory to resolve both
    /// CommonJS and ES modules.
    /// </summary>
    /// <param name="require">Require function.</param>
    /// <param name="modulePath">Module name or path that was supplied to the require or import
    /// function.</param>
    /// <param name="baseDir">Base directory for the module resolution.</param>
    /// <returns>Resolved module path.</returns>
    /// <exception cref="JSException">Thrown if the module could not be resolved.</exception>
    private static JSValue ResolveModulePath(
        JSValue require,
        JSValue modulePath,
        string baseDir)
    {
        // Pass the base directory to require.resolve() via the options object.
        JSObject options = new();
        options["paths"] = new JSArray(new[] { (JSValue)baseDir! });
        return require.CallMethod("resolve", modulePath, options);
    }

    /// <summary>
    /// Gets a synchronization context that enables switching to the Node.js environment's thread
    /// and returning to the thread after an `await`.
    /// </summary>
    /// <remarks>
    /// Except where otherwise documented, all interaction with the environment and JavaScript
    /// values associated with the environment MUST be executed on the environment's thread.
    /// The "Post" and "Run" methods of this class use the synchronization context to switch to
    /// the JS thread.
    /// </remarks>
    /// <seealso cref="Post(Action, bool) "/>
    /// <seealso cref="Post(Func{Task}, bool)"/>
    /// <seealso cref="Run(Action)"/>
    /// <seealso cref="Run{T}(Func{T})"/>
    /// <seealso cref="RunAsync(Func{Task})"/>
    /// <seealso cref="RunAsync{T}(Func{Task{T}})"/>
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

        // Unreffing the completion should complete the Node.js event loop
        // if it has nothing else to do.
        if (_completion != null)
        {
            // The Unref must be called in the JS thread.
            _completion.BlockingCall(() => _completion.Unref());
        }
        _thread.Join();

        Debug.WriteLine($"Node.js environment exited with code: {ExitCode}");
    }

    public Uri StartInspector(int? port = null, string? host = null, bool? wait = null)
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(NodejsEmbeddingThreadRuntime));

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
        JSValue listener = _unhandledPromiseRejectionListener!.GetValue();
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

    /// <summary>
    /// Runs an action on the JS thread, without waiting for completion.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <param name="allowSync">True to allow the action to run immediately if the current
    /// synchronization context is this one. By default the action will always be scheduled
    /// for later execution.
    /// </param>
    public void Post(Action action, bool allowSync = false)
        => SynchronizationContext.Post(action, allowSync);

    /// <summary>
    /// Runs an asynchronous action on the JS thread, without waiting for completion.
    /// </summary>
    /// <param name="asyncAction">The action to run.</param>
    /// <param name="allowSync">True to allow the action to run immediately if the current
    /// synchronization context is this one. By default the action will always be scheduled
    /// for later execution.
    /// </param>
    public void Post(Func<Task> asyncAction, bool allowSync = false)
        => SynchronizationContext.Post(asyncAction, allowSync);

    /// <summary>
    /// Runs an action on the JS thread, and waits for completion.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <exception cref="JSException">Any exception thrown by the action is wrapped in a
    /// JS exception. The original exception is available via the
    /// <see cref="Exception.InnerException" /> property.</exception>
    public void Run(Action action) => SynchronizationContext.Run(action);

    /// <summary>
    /// Runs an action on the JS thread, and waits for the return value.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <exception cref="JSException">Any exception thrown by the action is wrapped in a
    /// JS exception. The original exception is available via the
    /// <see cref="Exception.InnerException" /> property.</exception>
    public T Run<T>(Func<T> action) => SynchronizationContext.Run<T>(action);

    /// <summary>
    /// Runs an action on the JS thread, and asynchronously waits for completion.
    /// </summary>
    /// <param name="asyncAction">The action to run.</param>
    public Task RunAsync(Func<Task> asyncAction) => SynchronizationContext.RunAsync(asyncAction);

    /// <summary>
    /// Runs an action on the JS thread, and asynchronously waits for the return value.
    /// </summary>
    /// <param name="asyncAction">The action to run.</param>
    public Task<T> RunAsync<T>(Func<Task<T>> asyncAction)
        => SynchronizationContext.RunAsync<T>(asyncAction);

    /// <summary>
    /// Imports a module or module property from JavaScript.
    /// </summary>
    /// <param name="module">Name of the module being imported, or null to import a
    /// global property. This is equivalent to the value provided to <c>import</c> or
    /// <c>require()</c> in JavaScript. Required if <paramref name="property"/> is null.</param>
    /// <param name="property">Name of a property on the module (or global), or null to import
    /// the module object. Required if <paramref name="module"/> is null.</param>
    /// <param name="esModule">True if importing an ES module. The default is false. Note when
    /// importing an ES module the returned value will be a JS Promise object that resolves to the
    /// imported value.</param>
    /// <returns>The imported value.</returns>
    /// <exception cref="ArgumentNullException">Both <paramref name="module" /> and
    /// <paramref name="property" /> are null.</exception>
    /// <exception cref="InvalidOperationException">The
    /// <see cref="JSRuntimeContext.RequireFunction"/> property was not initialized.</exception>
    public JSValue Import(string? module, string? property = null, bool esModule = false)
        => _scope.RuntimeContext.Import(module, property, esModule);

    public Task<JSValue> ImportAsync(string? module, string? property = null, bool esModule = false)
        => _scope.RuntimeContext.ImportAsync(module, property, esModule);

    /// <summary>
    /// Runs garbage collection in the JS environment.
    /// </summary>
    /// <exception cref="InvalidOperationException">The Node.js platform was not initialized
    /// with the --expose-gc option.</exception>
    public void GC()
    {
        bool result = SynchronizationContext.Run(() =>
        {
            JSValue gcFunction = JSValue.Global["gc"];
            if (gcFunction.TypeOf() != JSValueType.Function)
            {
                return false;
            }

            gcFunction.Call();
            return true;
        });

        if (!result)
        {
            throw new InvalidOperationException("The global gc() function was not found. " +
                "Make sure the Node.js platform was initialized with the `--enable-gc` option.");
        }
    }
}
