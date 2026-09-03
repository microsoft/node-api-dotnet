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
        return (T?)JSValueScope.Current.Module;
    }

    /// <summary>
    /// Exports the built properties to the module exports object.
    /// </summary>
    /// <param name="module">An object that represents the module instance and is
    /// used as the 'this' argument for any non-static methods on the module.</param>
    /// <param name="exports">Object to be returned from the module initializer.</param>
    /// <returns>The module exports.</returns>
    public JSValue ExportModule(T module, JSObject exports)
    {
        // Write through the holder the descriptors captured, so callbacks bound before the module
        // instance existed observe it.
        JSValueScope.Current.ModuleHolder!.Value = module;

        // Honor JSModuleAttribute's IDisposable contract. Modules loaded into one host share a
        // context, and the module-less path passes the context as the module, so append real
        // instances (not the type-keyed annotation, which would collide on typeof(IDisposable));
        // the context disposes itself via its own teardown.
        JSRuntimeContext context = JSValueScope.Current.RuntimeContext;
        if (module is IDisposable disposable && !ReferenceEquals(module, context))
        {
            context.AddModuleDisposable(disposable);
        }

        exports.DefineProperties(Properties.ToArray());
        return exports;
    }
}
