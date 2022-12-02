using System.Linq;

namespace NodeApi;

public class JSModuleBuilder<T>
  : JSPropertyDescriptorList<JSModuleBuilder<T>, T>
  , IJSObjectUnwrap<T>
  where T : class
{
  static T? IJSObjectUnwrap<T>.Unwrap(JSCallbackArgs _)
  {
    return (T?)JSNativeApi.GetInstanceData();
  }

  public JSValue ExportModule(JSValue exports, T obj)
  {
    JSNativeApi.SetInstanceData(obj);
    exports.DefineProperties(Properties.ToArray());
    return exports;
  }
}
