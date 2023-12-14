// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// A strong or weak reference to a JS value.
/// </summary>
/// <remarks>
/// <see cref="JSValue"/> and related JS handle structs are not valid after their
/// <see cref="JSValueScope"/> closes -- typically that is when a callback returns. Use a
/// <see cref="JSReference"/> to maintain a reference to a JS value beyond a single scope.
/// <para/>
/// A <see cref="JSReference"/> should be disposed when no longer needed; this allows the JS value
/// to be collected by the GC if it has no other references. The <see cref="JSReference"/> class
/// also has a finalizer so that the reference will be released when the C# object is GC'd. However
/// explicit disposal is still preferable when possible.
/// </remarks>
public class JSReference : IDisposable
{
    private readonly napi_ref _handle;
    private readonly napi_env _env;
    private readonly JSRuntimeContext? _context;

    public bool IsWeak { get; private set; }

    public JSReference(JSValue value, bool isWeak = false)
        : this(value.Runtime.CreateReference(
                  (napi_env)JSValueScope.Current,
                  (napi_value)value,
                  isWeak ? 0u : 1u,
                  out napi_ref handle).ThrowIfFailed(handle),
               isWeak)
    {
    }

    public JSReference(napi_ref handle, bool isWeak = false)
    {
        JSValueScope currentScope = JSValueScope.Current;

        // Thread access to the env will be checked on reference handle use.
        _env = currentScope.UncheckedEnvironmentHandle;
        _handle = handle;
        _context = currentScope.RuntimeContext;
        IsWeak = isWeak;
    }

    /// <summary>
    /// Gets the value handle, or throws an exception if access from the current thread is invalid.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">Access to the reference is not valid on
    /// the current thread.</exception>
    public napi_ref Handle
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfInvalidThreadAccess();
            return _handle;
        }
    }

    public static explicit operator napi_ref(JSReference reference)
    {
        if (reference is null) throw new ArgumentNullException(nameof(reference));
        return reference.Handle;
    }

    public static bool TryCreateReference(
        JSValue value, bool isWeak, [NotNullWhen(true)] out JSReference? result)
    {
        napi_status status = value.Runtime.CreateReference(
                  (napi_env)JSValueScope.Current,
                  (napi_value)value,
                  isWeak ? 0u : 1u,
                  out napi_ref handle);
        if (status == napi_status.napi_ok)
        {
            result = new JSReference(handle, isWeak);
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Gets the synchronization context that must be used to access the referenced value.
    /// </summary>
    /// <remarks>
    /// Use the <see cref="JSSynchronizationContext.Run(Action)" /> method to wrap code that
    /// accesses the referenced value, if there is a possibility that the current execution
    /// context is not already on the correct thread.
    /// </remarks>
    public JSSynchronizationContext? SynchronizationContext => _context?.SynchronizationContext;

    private napi_env Env
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfInvalidThreadAccess();
            return _env;
        }
    }

    public void MakeWeak()
    {
        if (!IsWeak)
        {
            JSValueScope.CurrentRuntime.UnrefReference(Env, _handle, out _).ThrowIfFailed();
            IsWeak = true;
        }
    }
    public void MakeStrong()
    {
        if (IsWeak)
        {
            JSValueScope.CurrentRuntime.RefReference(Env, _handle, out _).ThrowIfFailed();
            IsWeak = false;
        }
    }

    public JSValue? GetValue()
    {
        JSValueScope.CurrentRuntime.GetReferenceValue(Env, _handle, out napi_value result)
            .ThrowIfFailed();
        return result;
    }

    /// <summary>
    /// Runs an action with the referenced value, using the <see cref="JSSynchronizationContext" />
    /// associated with the reference to switch to the JS thread (if necessary) while operating
    /// on the value.
    /// </summary>
    public void Run(Action<JSValue> action)
    {
        void GetValueAndRunAction()
        {
            JSValue? value = GetValue();
            if (!value.HasValue)
            {
                throw new NullReferenceException("The JS reference is null.");
            }

            action(value.Value);
        }

        JSSynchronizationContext? synchronizationContext = SynchronizationContext;
        if (synchronizationContext != null)
        {
            synchronizationContext.Run(GetValueAndRunAction);
        }
        else
        {
            GetValueAndRunAction();
        }
    }

    /// <summary>
    /// Runs an action with the referenced value, using the <see cref="JSSynchronizationContext" />
    /// associated with the reference to switch to the JS thread (if necessary) while operating
    /// on the value.
    /// </summary>
    public T Run<T>(Func<JSValue, T> action)
    {
        T GetValueAndRunAction()
        {
            JSValue? value = GetValue();
            if (!value.HasValue)
            {
                throw new NullReferenceException("The JS reference is null.");
            }

            return action(value.Value);
        }

        JSSynchronizationContext? synchronizationContext = SynchronizationContext;
        if (synchronizationContext != null)
        {
            return synchronizationContext.Run(GetValueAndRunAction);
        }
        else
        {
            return GetValueAndRunAction();
        }
    }

    public bool IsDisposed { get; private set; }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(JSReference));
        }
    }

    /// <summary>
    /// Checks that the current thread is the thread that is running the JavaScript environment
    /// that this reference was created in.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">The reference cannot be accessed from the
    /// current thread.</exception>
    private void ThrowIfInvalidThreadAccess()
    {
        JSValueScope currentScope = JSValueScope.Current;
        if ((napi_env)currentScope != _env)
        {
            int threadId = Environment.CurrentManagedThreadId;
            string? threadName = Thread.CurrentThread.Name;
            string threadDescription = string.IsNullOrEmpty(threadName) ?
                $"#{threadId}" : $"#{threadId} \"{threadName}\"";
            string message = "The JS reference cannot be accessed from the current thread.\n" +
                $"Current thread: {threadDescription}. " +
                $"Consider using the synchronization context to switch to the JS thread.";
            throw new JSInvalidThreadAccessException(currentScope, message);
        }
    }

    /// <summary>
    /// Releases the reference.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            // The context may be null if the reference was created from a "no-context" scope such
            // as the native host. In that case the reference must be disposed from the JS thread.
            if (_context == null)
            {
                ThrowIfInvalidThreadAccess();
                JSValueScope.CurrentRuntime.DeleteReference(_env, _handle).ThrowIfFailed();
            }
            else
            {
                _context.SynchronizationContext.Post(
                    () => _context.Runtime.DeleteReference(
                        _env, _handle).ThrowIfFailed(), allowSync: true);
            }
        }
    }

    ~JSReference() => Dispose(disposing: false);
}
