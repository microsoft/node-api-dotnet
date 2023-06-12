// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

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
        : this(napi_create_reference(
                  (napi_env)JSValueScope.Current,
                  (napi_value)value,
                  isWeak ? 0u : 1u,
                  out napi_ref handle).ThrowIfFailed(handle),
               isWeak)
    {
    }

    public JSReference(napi_ref handle, bool isWeak = false)
    {
        _handle = handle;
        _env = (napi_env)JSValueScope.Current;
        _context = JSRuntimeContext.Current;
        IsWeak = isWeak;
    }

    public static bool TryCreateReference(
        JSValue value, bool isWeak, [NotNullWhen(true)] out JSReference? result)
    {
        napi_status status = napi_create_reference(
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

    public void MakeWeak()
    {
        ThrowIfDisposed();
        if (!IsWeak)
        {
            napi_reference_unref(_env, _handle, default).ThrowIfFailed();
            IsWeak = true;
        }
    }
    public void MakeStrong()
    {
        ThrowIfDisposed();
        if (IsWeak)
        {
            napi_reference_ref(_env, _handle, default).ThrowIfFailed();
            IsWeak = true;
        }
    }

    public JSValue? GetValue()
    {
        ThrowIfDisposed();
        napi_get_reference_value(_env, _handle, out napi_value result).ThrowIfFailed();
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

    public static explicit operator napi_ref(JSReference value) => value._handle;

    public bool IsDisposed { get; private set; }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(JSReference));
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
            napi_ref handle = _handle; // To capture in lambda

            // The context may be null if the reference was created from a "no-context" scope such
            // as the native host. In that case the reference must be disposed from the JS thread.
            if (SynchronizationContext == null)
            {
                napi_delete_reference(_env, handle).ThrowIfFailed();
            }
            else
            {
                SynchronizationContext.Post(
                    () => napi_delete_reference(_env, handle).ThrowIfFailed(), allowSync: true);
            }
        }
    }

    ~JSReference() => Dispose(disposing: false);
}
