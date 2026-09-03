// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Descriptor for a callback not associated with an object property, for example a constructor
/// callback or standalone function callback. Enables passing a data object via the callback
/// args data.
/// </summary>
[DebuggerDisplay("{Name,nq}()")]
public readonly struct JSCallbackDescriptor
{
    /// <summary>
    /// Saves the module instance holder under which the callback was defined, so that multiple .NET
    /// modules in the same process can register callbacks for module-level functions.
    /// </summary>
    internal StrongBox<object?>? ModuleHolder { get; }

    /// <summary>
    /// Gets the name of the callback, for debugging purposes.
    /// </summary>
    public string? Name { get; }

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
        : this(null, callback, data, JSValueScope.Current.ModuleHolder)
    {
    }

    public JSCallbackDescriptor(string? name, JSCallback callback, object? data = null)
        : this(name, callback, data, JSValueScope.Current.ModuleHolder)
    {
    }

    internal JSCallbackDescriptor(JSCallback callback, object? data, StrongBox<object?>? moduleHolder)
        : this(null, callback, data, moduleHolder)
    {
    }

    internal JSCallbackDescriptor(
        string? name, JSCallback callback, object? data, StrongBox<object?>? moduleHolder)
    {
        Name = name;
        Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        Data = data;
        ModuleHolder = moduleHolder;
    }

    public static implicit operator JSCallbackDescriptor(JSCallback callback) => new(callback);
}
