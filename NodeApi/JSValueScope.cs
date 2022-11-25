using System;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public class JSValueScope : IDisposable
{
  private napi_env _env;
  private bool _isDisposed = false;
  [ThreadStatic] private static JSValueScope? t_current = null;

  public JSValueScope? ParentScope { get; }

  public JSValueScope(napi_env env)
  {
    _env = env;
    ParentScope = t_current;
    t_current = this;
  }

  public static JSValueScope? Current { get { return t_current; } }

  public void Close() => Dispose();

  public void Dispose()
  {
    // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    Dispose(disposing: true);
    GC.SuppressFinalize(this);
  }

  public bool IsInvalid => _isDisposed;

  public static explicit operator napi_env(JSValueScope? scope)
  {
    if (scope != null)
      return scope._env;
    else
      throw new JSException("Out of scope!");
  }

  protected virtual void Dispose(bool disposing)
  {
    if (!_isDisposed)
    {
      _isDisposed = true;
    }
  }
}
