// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Descriptor for a callback not associated with an object property, for example a constructor
/// callback or standalone function callback. Enables passing a data object via the callback
/// args data.
/// </summary>
public readonly struct JSCallbackDescriptor
{
    /// <summary>
    /// Saves the module context under which the callback was defined, so that multiple .NET
    /// modules in the same process can register callbacks for module-level functions.
    /// </summary>
    internal JSModuleContext? ModuleContext { get; }

    /// <summary>
    /// Gets the callback that handles invocations from JavaScript.
    /// </summary>
    public JSCallback Callback { get; }

    /// <summary>
    /// Gets the optional data object that will be passed to the callback via
    /// <see cref="JSCallbackArgs.Data" />.
    /// </summary>
    public object? Data { get; }

    public JSCallbackDescriptor(JSCallback callback, object? data = null)
        : this(callback, data, JSValueScope.Current.ModuleContext)
    {
    }

    internal JSCallbackDescriptor(JSCallback callback, object? data, JSModuleContext? moduleContext)
    {
        ModuleContext = moduleContext;
        Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        Data = data;
    }

    public static implicit operator JSCallbackDescriptor(JSCallback callback) => new(callback);
}
