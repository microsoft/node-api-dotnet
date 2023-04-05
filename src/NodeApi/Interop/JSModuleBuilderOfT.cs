// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Builds JS module exports.
/// </summary>
/// <typeparam name="T">Either <see cref="JSRuntimeContext" /> or a custom module class that
/// wraps a <see cref="JSRuntimeContext"/> instance.</typeparam>
public class JSModuleBuilder<T> : JSPropertyDescriptorList<JSModuleBuilder<T>, T> where T : class
{
    public JSModuleBuilder() : base(Unwrap)
    {
    }

    private static new T? Unwrap(JSCallbackArgs _)
    {
        return (T?)JSModuleContext.Current.Module;
    }

    /// <summary>
    /// Exports the built properties to the module exports object.
    /// </summary>
    /// <param name="module">An object that represents the module instance and is
    /// used as the 'this' argument for any non-static methods on the module. If the object
    /// implements <see cref="IDisposable"/> then it is also registered for disposal when
    /// the module is unloaded.</param>
    /// <param name="exports">Object to be returned from the module initializer.</param>
    /// <returns>The module exports.</returns>
    public JSValue ExportModule(T module, JSObject exports)
    {
        JSModuleContext.Current.Module = module;
        exports.DefineProperties(Properties.ToArray());
        return exports;
    }
}
