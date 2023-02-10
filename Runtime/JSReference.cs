using System;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public class JSReference : IDisposable
{
    private readonly napi_env _env;
    private readonly napi_ref _handle;

    public bool IsWeak { get; private set; }

    public JSReference(JSValue value, bool isWeak = false)
    {
        _env = (napi_env)value.Scope;
        napi_create_reference(_env, (napi_value)value, isWeak ? 0u : 1u, out _handle).ThrowIfFailed();
        IsWeak = isWeak;
    }

    public JSReference(napi_ref handle, bool isWeak = false)
    {
        _env = (napi_env)JSSimpleValueScope.Current;
        _handle = handle;
        IsWeak = isWeak;
    }

    public void MakeWeak()
    {
        if (!IsWeak)
        {
            napi_reference_unref(_env, _handle, nint.Zero).ThrowIfFailed();
            IsWeak = true;
        }
    }
    public void MakeStrong()
    {
        if (IsWeak)
        {
            napi_reference_ref(_env, _handle, nint.Zero).ThrowIfFailed();
            IsWeak = true;
        }
    }

    public JSValue? GetValue()
    {
        napi_get_reference_value(_env, _handle, out napi_value result).ThrowIfFailed();
        return result;
    }

    public bool IsDisposed { get; private set; } = false;

    public static explicit operator napi_ref(JSReference value) => value._handle;

    public void Delete() => Dispose();

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
            if (disposing)
            {
                napi_delete_reference(_env, _handle).ThrowIfFailed();
            }
            IsDisposed = true;
        }
    }
}
