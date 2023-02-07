using System;
using System.Linq;

namespace NodeApi;

/// <summary>
/// Builds JS module exports.
/// </summary>
/// <typeparam name="T">Either <see cref="JSContext" /> or a custom module class that
/// wraps a <see cref="JSContext"/> instance.</typeparam>
public class JSModuleBuilder<T>
  : JSPropertyDescriptorList<JSModuleBuilder<T>, T>
  , IJSObjectUnwrap<T>
  where T : class
{
    static T? IJSObjectUnwrap<T>.Unwrap(JSCallbackArgs _)
    {
        return (T?)JSNativeApi.GetInstanceData();
    }

    /// <summary>
    /// Exports the built properties to the module exports object.
    /// </summary>
    /// <param name="context">An object that represents the module instance and is
    /// used as the 'this' argument for any non-static methods on the module. If the object
    /// implements <see cref="IDisposable"/> then it is also registered for disposal when
    /// the module is unloaded.</param>
    /// <param name="exports">Object to be returned from the module initializer.</param>
    /// <returns>The module exports.</returns>
    public JSValue ExportModule(T context, JSObject exports)
    {
        JSNativeApi.SetInstanceData(context);
        exports.DefineProperties(Properties.ToArray());
        return exports;
    }
}
