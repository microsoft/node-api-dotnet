// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// A Node.js worker thread, as documented at https://nodejs.org/api/worker_threads.html.
/// </summary>
/// <remarks>
/// Static members of this class enable a worker thread to access its current context, while
/// instance members enable a parent (or main) thread to manage a specific child worker thread.
/// </remarks>
public class NodeWorker
{
    private readonly JSReference _workerRef;

    // Note the Import() function caches a reference to the imported module.
    private static JSValue WorkerModule => JSRuntimeContext.Current.Import("node:worker_threads");

    /// <summary>
    /// Creates a new instance of <see cref="NodeWorker" /> that runs a Node.js script in a
    /// separate worker thread.
    /// </summary>
    /// <param name="workerScript">Path to the script file to run in the worker. Or if
    /// <see cref="NodeWorker.Options.Eval"/> is true the string is a script to be directly
    /// evaluated.</param>
    /// <param name="options">Worker options.</param>
    public NodeWorker(string workerScript, NodeWorker.Options options)
    {
        if (string.IsNullOrEmpty(workerScript))
        {
            throw new ArgumentNullException(nameof(workerScript));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        JSValue workerClass = WorkerModule["Worker"];
        JSValue worker = workerClass.CallAsConstructor(workerScript, options.ToJS());
        RegisterEventHandlers(worker);
        _workerRef = new JSReference(worker);
    }

    /// <summary>
    /// Creates a new instance of <see cref="NodeWorker" /> that runs a callback delegate
    /// in a Node.js worker thread.
    /// </summary>
    /// <param name="workerCallback">Callback delegate to be invoked on the worker thread.
    /// (The callback may then invoke script and interop with the worker JS context.)</param>
    /// <param name="options">Worker options.</param>
    public NodeWorker(Action workerCallback, NodeWorker.Options options)
    {
        if (workerCallback == null)
        {
            throw new ArgumentNullException(nameof(workerCallback));
        }
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // It is not possible to pass a JS Function value to a worker. Instead, this saves the
        // callback via a GC handle, then passes the GC handle integer to the worker.
        // Do not use JSRuntimeContext.AllocGCHandle() here, because the handle will be freed from
        // another runtime context (the worker thread).
        nint callbackHandle = (nint)GCHandle.Alloc(workerCallback);
        string workerScript = @$"require('node-api-dotnet').runWorker({callbackHandle}n);";

        JSValue workerClass = WorkerModule["Worker"];
        JSValue worker = workerClass.CallAsConstructor(
            workerScript, options.ToJS(overrideEval: true));
        RegisterEventHandlers(worker);
        _workerRef = new JSReference(worker);

        // TODO: This isn't fullly implemented yet: the require('node-api-dotnet') in the worker
        // script currently fails. We'll need a way to override the require() function in the
        // worker context.
        throw new NotImplementedException();
    }

    private void RegisterEventHandlers(JSValue worker)
    {
        JSValue onMethod = worker["on"];
        onMethod.Call(worker, "online", new JSFunction(() =>
        {
            Online?.Invoke(this, EventArgs.Empty);
        }));
        onMethod.Call(worker, "message", new JSFunction((JSValue message) =>
        {
            Message?.Invoke(this, new MessageEventArgs(message));
        }));
        onMethod.Call(worker, "messageerror", new JSFunction((JSValue error) =>
        {
            MessageError?.Invoke(this, new ErrorEventArgs(new JSError(error)));
        }));
        onMethod.Call(worker, "error", new JSFunction((JSValue error) =>
        {
            Error?.Invoke(this, new ErrorEventArgs(new JSError(error)));
        }));
        onMethod.Call(worker, "exit", new JSFunction((JSValue exitCode) =>
        {
            Exit?.Invoke(this, new ExitEventArgs((int)exitCode));
        }));
    }

    /// <summary>
    /// Gets a value indicating whether the current code is running on the main JS thread;
    /// if false it is a worker thread.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">No JS scope was established for the current
    /// thread.</exception>
    public static bool IsMainThread => (bool)WorkerModule["isMainThread"];

    /// <summary>
    /// Gets an integer identifier for the current JS thread. On the corresponding worker object
    /// (if there is any), it is available as <see cref="ThreadId"/>. This value is unique for each
    /// instance inside a single process.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">No JS scope was established for the current
    /// thread.</exception>
    public static int CurrentThreadId => (int)WorkerModule["threadId"];

    /// <summary>
    /// An arbitrary JavaScript value that contains a clone of the data passed to this thread's
    /// Worker constructor.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">No JS scope was established for the current
    /// thread.</exception>
    public static JSValue CurrentWorkerData => WorkerModule["workerData"];

    /// <summary>
    /// An integer identifier for the referenced thread. Inside the worker thread, it is available
    /// as <see cref="CurrentThreadId"/>. This value is unique for each Worker
    /// instance inside a single process.
    /// </summary>
    public int ThreadId => (int)_workerRef.Run((worker) => worker["threadId"]);

    /// <summary>
    /// If <see cref="Stdin"/>: true was passed to the Worker constructor, this is a writable
    /// stream. The data written to this stream will be made available in the worker thread as
    /// <see cref="NodeProcess.Stdin"/>.
    /// </summary>
    public Stream? Stdin => _workerRef.Run((worker) =>
    {
        JSValue stream = worker["stdin"];
        return stream.IsNullOrUndefined() ? null : (NodeStream)stream;
    });

    /// <summary>
    /// A readable stream which contains data written to <see cref="NodeProcess.Stdout"/> inside
    /// the worker thread. If <see cref="Stdout"/>: true was not passed to the Worker constructor,
    /// then data is piped to the parent thread's <see cref="NodeProcess.Stdout"/> stream.
    /// </summary>
    public Stream Stdout => _workerRef.Run((worker) => (NodeStream)worker["stdout"]);

    /// <summary>
    /// A readable stream which contains data written to <see cref="NodeProcess.Stderr"/> inside
    /// the worker thread. If <see cref="Stderr"/>: true was not passed to the Worker constructor,
    /// then data is piped to the parent thread's <see cref="NodeProcess.Stderr"/> stream.
    /// </summary>
    public Stream Stderr => _workerRef.Run((worker) => (NodeStream)worker["stderr"]);

    /// <summary>
    /// Within a worker thread, returns a clone of data passed to the spawning thread's
    /// <see cref="SetEnvironmentData"/>. Every new Worker receives its own copy of the environment
    /// data automatically.
    /// </summary>
    public static JSValue GetEnvironmentData(JSValue key)
        => WorkerModule.CallMethod("getEnvironmentData", key);

    /// <summary>
    /// Sets the environment data in the current thread and all new Worker instances spawned from
    /// the current context.
    /// </summary>
    public static void SetEnvironmentData(JSValue key, JSValue value)
        => WorkerModule.CallMethod("setEnvironmentData", key, value);

    /// <summary>
    /// Opposite of <see cref="Unref"/>. Calling ref() on a previously unref()ed worker does not
    /// let the program exit if it's the only active handle left (the default behavior). If the
    /// worker is ref()ed, calling ref() again has no effect.
    /// </summary>
    public void Ref()
    {
        _workerRef.Run((worker) => worker.CallMethod("ref"));
    }

    /// <summary>
    /// Allows the thread to exit if this is the only active handle in the event system. If the
    /// worker is already unref()ed calling unref() again has no effect.
    /// </summary>
    public void Unref()
    {
        _workerRef.Run((worker) => worker.CallMethod("unref"));
    }

    /// <summary>
    /// Stops all JavaScript execution in the worker thread as soon as possible. Returns a Promise
    /// for the exit code that is fulfilled when the 'exit' event is emitted.
    /// </summary>
    public Task<int?> Terminate()
    {
        return _workerRef.Run((worker) =>
        {
            JSPromise exitPromise = (JSPromise)worker.CallMethod("terminate");
            return exitPromise.AsTask<int?>((exitCode) => (int?)exitCode ?? null);
        });
    }

    /// <summary>
    /// Send a message to the worker that is received via the
    /// <see cref="ParentPort"/> <see cref="MessagePort.Message"/> event.
    /// </summary>
    /// <param name="value"></param>
    public void PostMessage(JSValue value)
    {
        _workerRef.Run((worker) => worker.CallMethod("postMessage", value));
    }

    /// <summary>
    /// If this thread is a Worker, this is a <see cref="MessagePort"/> allowing communication with
    /// the parent thread. Messages posted via the parent port <see cref="MessagePort.PostMessage"/>
    /// are available in the parent thread via the worker's <see cref="Message"/> event, and
    /// messages sent from the parent thread using the worker's <see cref="PostMessage"/> are
    /// available in this thread via the parent port's <see cref="MessagePort.Message"> event.
    /// </summary>
    public static MessagePort? ParentPort
    {
        get
        {
            JSValue parentPort = WorkerModule["parentPort"];
            return parentPort.IsUndefined() ? null : new MessagePort(parentPort);
        }
    }

    /// <summary>
    /// Represents one end of an asynchronous, two-way communications channel. It can be used to
    /// transfer structured data, memory regions, and other MessagePorts between different Workers.
    /// </summary>
    public class MessagePort
    {
        private readonly JSReference _portRef;

        internal MessagePort(JSValue port)
        {
            RegisterEventHandlers(port);
            _portRef = new JSReference(port);
        }

        private void RegisterEventHandlers(JSValue port)
        {
            JSValue onMethod = port["on"];
            onMethod.Call(port, "message", new JSFunction((JSValue message) =>
            {
                Message?.Invoke(this, new MessageEventArgs(message));
            }));
            onMethod.Call(port, "messageerror", new JSFunction((JSValue error) =>
            {
                MessageError?.Invoke(this, new ErrorEventArgs(new JSError(error)));
            }));
            onMethod.Call(port, "close", new JSFunction((JSValue error) =>
            {
                Closed?.Invoke(this, new EventArgs());
            }));
        }

        public static (MessagePort Port1, MessagePort Port2) CreateChannel()
        {
            JSValue channel = WorkerModule["MessageChannel"].CallAsConstructor();
            return (new MessagePort(channel["port1"]), new MessagePort(channel["port2"]));
        }

        public void PostMessage(JSValue value)
        {
            _portRef.Run((port) => port.CallMethod("postMessage", value));
        }

        public event EventHandler<MessageEventArgs>? Message;
        public event EventHandler<ErrorEventArgs>? MessageError;
        public event EventHandler? Closed;

        public void Ref()
        {
            _portRef.Run((port) => port.CallMethod("ref"));
        }

        /// <summary>
        /// Allows the thread to exit if this is the only active handle in the event system. If the
        /// worker is already unref()ed calling unref() again has no effect.
        /// </summary>
        public void Unref()
        {
            _portRef.Run((port) => port.CallMethod("unref"));
        }

        /// <summary>
        /// Disables further sending of messages on either side of the connection. This method can
        /// be called when no further communication will happen over this MessagePort.
        /// The <see cref="Closed" /> event is emitted on both MessagePort instances that are part
        /// of the channel.
        /// </summary>
        public void Close()
        {
            _portRef.Run((port) => port.CallMethod("close"));
        }
    }

    /// <summary>
    /// Emitted when the worker thread has started executing JavaScript code.
    /// </summary>
    public event EventHandler? Online;

    /// <summary>
    /// Emitted when the worker thread has sent a message via its
    /// <see cref="ParentPort"/> <see cref="MessagePort.PostMessage"/> method.
    /// </summary>
    /// <remarks>
    /// All messages sent from the worker thread are emitted before the <see cref="Exit"> event is
    /// emitted on the Worker object.
    /// </remarks>
    public event EventHandler<MessageEventArgs>? Message;

    /// <summary>
    /// Emitted when deserializing a message failed.
    /// </summary>
    public event EventHandler<ErrorEventArgs>? MessageError;

    /// <summary>
    /// Emitted if the worker thread throws an uncaught exception. In that case, the worker is
    /// terminated.
    /// </summary>
    public event EventHandler<ErrorEventArgs>? Error;

    /// <summary>
    /// Emitted once the worker has stopped. If the worker exited by calling
    /// <see cref="NodeProcess.Exit"/>, the <see cref="ExitEventArgs.ExitCode"> parameter is the
    /// passed exit code. If the worker was terminated, the <see cref="ExitEventArgs.ExitCode">
    /// parameter is 1.
    /// </summary>
    public event EventHandler<ExitEventArgs>? Exit;

    public class MessageEventArgs : EventArgs
    {
        public MessageEventArgs(JSValue value) => Value = value;
        public JSValue Value { get; }
    }

    public class ErrorEventArgs : EventArgs
    {
        public ErrorEventArgs(JSError error) => Error = error;
        public JSError Error { get; }
    }

    public class ExitEventArgs : EventArgs
    {
        public ExitEventArgs(int exitCode) => ExitCode = exitCode;
        public int ExitCode { get; }
    }

    /// <summary>
    /// Options for configuring a <see cref="NodeWorker" />.
    /// </summary>
    public class Options
    {
        /// <summary>
        /// If true, interpret the first argument to the constructor as a script that is executed
        /// once the worker is online. Otherwise the first argument to the constructor must be
        /// a file path to the script.
        /// </summary>
        public bool Eval { get; init; }

        /// <summary>
        /// An optional name to be appended to the worker title for debugging/identification purposes.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// List of arguments which would be stringified and appended to
        /// <see cref="NodeProcess.Argv"/> in the worker. This is mostly similar to the
        /// <see cref="WorkerData"/> but the values are available on the global
        /// <see cref="NodeProcess.Argv"/> as if they were passed as CLI options to the script.
        /// </summary>
        public string[]? Argv { get; set; }

        /// <summary>
        /// List of node CLI options passed to the worker. V8 options and options that affect the
        /// process are not supported. If set, this is provided as process.execArgv inside the 
        /// worker. By default, options are inherited from the parent thread.
        /// </summary>
        public string[]? ExecArgv { get; set; }

        /// <summary>
        /// Any JavaScript value that is cloned and made available as
        /// <see cref="CurrentWorkerData"/>. The cloning occurs as described in the HTML structured
        /// clone algorithm, and an error is thrown if the object cannot be cloned (e.g. because it
        /// contains functions).
        /// </summary>
        public JSValue? WorkerData { get; set; }

        /// <summary>
        /// If set, specifies the initial value of <see cref="NodeProcess.Env"/> inside the
        /// Worker thread. Default: parent thread env.
        /// </summary>
        /// <remarks>
        /// Not valid if <see cref="ShareEnv"/> is true.
        /// </remarks>
        public IDictionary<string, string>? Env { get; set; }

        /// <summary>
        /// Specifies that the parent thread and the child thread should share their environment
        /// variables; in that case, changes to one thread's <see cref="NodeProcess.Env"/> object
        /// affect the other thread as well.
        /// </summary>
        /// <remarks>
        /// <see cref="Env"/> must be null if this option is set.
        /// </remarks>
        public bool ShareEnv { get; set; }

        /// <summary>
        /// If this is set to true, then <see cref="NodeWorker.Stdin"/> provides a writable stream
        /// whose contents appear as <see cref="NodeProcess.Stdin"/> inside the Worker. By default,
        /// no data is provided.
        /// </summary>
        public bool Stdin { get; set; }

        /// <summary>
        /// If this is set to true, then <see cref="NodeWorker.Stdout"/> is not automatically
        /// piped through to <see cref="NodeProcess.Stdout"/> in the parent.
        /// </summary>
        public bool Stdout { get; set; }

        /// <summary>
        /// If this is set to true, then <see cref="NodeWorker.Stderr"/> is not automatically
        /// piped through to <see cref="NodeProcess.StdErr"/> in the parent.
        /// </summary>
        public bool Stderr { get; set; }

        /// <summary>
        /// If this is set to true, then the Worker tracks raw file descriptors managed through
        /// <c>fs.open()</c> and <c>fs.close()</c>, and closes them when the Worker exits, similar
        /// to other resources like network sockets or file descriptors managed through the
        /// FileHandle API. This option is automatically inherited by all nested Workers.
        /// Default: true.
        /// </summary>
        public bool TrackUnmanagedFds { get; set; } = true;

        /// <summary>
        /// An optional set of resource limits for the new JS engine instance. Reaching these limits
        /// leads to termination of the Worker instance. These limits only affect the JS engine,
        /// and no external data, including no ArrayBuffers. Even if these limits are set, the
        /// process may still abort if it encounters a global out-of-memory situation.
        /// </summary>
        public NodeWorker.ResourceLimits? ResourceLimits { get; set; }

        internal JSObject ToJS(bool? overrideEval = null)
        {
            return new JSObject
            {
                ["eval"] = overrideEval ?? Eval,
                ["name"] = Name ?? JSValue.Undefined,
                ["argv"] = Argv != null ?
                    new JSArray(Argv.Select((a) => (JSValue)a).ToArray()) :
                    JSValue.Undefined,
                ["execArgv"] = ExecArgv != null ?
                    new JSArray(ExecArgv.Select((a) => (JSValue)a).ToArray()) :
                    JSValue.Undefined,
                ["workerData"] = WorkerData ?? JSValue.Undefined,
                ["env"] = ShareEnv ? WorkerModule["SHARE_ENV"] : Env != null ?
                    new JSObject(Env.Select(
                        kv => new KeyValuePair<JSValue, JSValue>(kv.Key, kv.Value))) :
                    JSValue.Undefined,
                ["stdin"] = Stdin,
                ["stdout"] = Stdout,
                ["stderr"] = Stderr,
                ["trackUnmanagedFds"] = TrackUnmanagedFds,
                ["resourceLimits"] = ResourceLimits?.ToJS() ?? JSValue.Undefined,
            };
        }
    }

    /// <summary>
    /// Resource limits for a <see cref="NodeWorker" />.
    /// </summary>
    public class ResourceLimits
    {
        public uint? MaxOldGenerationSizeMb { get; set; }
        public uint? MaxYoungGenerationSizeMb { get; set; }
        public uint? CodeRangeSizeMb { get; set; }
        public uint? StackSizeMb { get; set; }

        internal JSObject ToJS()
        {
            return new JSObject
            {
                ["maxOldGenerationSizeMb"] = MaxOldGenerationSizeMb ?? JSValue.Undefined,
                ["maxYoungGenerationSizeMb"] = MaxYoungGenerationSizeMb ?? JSValue.Undefined,
                ["codeRangeSizeMb"] = CodeRangeSizeMb ?? JSValue.Undefined,
                ["stackSizeMb"] = StackSizeMb ?? JSValue.Undefined,
            };
        }
    }
}
