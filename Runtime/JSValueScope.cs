using System;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public class JSValueScope : IDisposable
{
    private napi_env _env;
    [ThreadStatic] private static JSValueScope? s_current;

    public JSValueScope? ParentScope { get; }

    public JSValueScope(napi_env env)
    {
        _env = env;
        ParentScope = s_current;
        s_current = this;
    }

    public static JSValueScope? Current => s_current;

    public void Close() => Dispose();

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    public bool IsDisposed { get; private set; } = false;

    public static explicit operator napi_env(JSValueScope? scope)
    {
        if (scope == null)
        {
            throw new JSException("Out of scope!");
        }
        return scope._env;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            IsDisposed = true;
            s_current = ParentScope;
        }
    }
}
