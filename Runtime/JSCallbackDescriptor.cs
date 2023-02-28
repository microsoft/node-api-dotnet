using System;

namespace NodeApi;

/// <summary>
/// Descriptor for a callback not associated with an object property, for example a constructor
/// callback or standalone function callback. Enables passing a data object via the callback
/// args data.
/// </summary>
public readonly struct JSCallbackDescriptor
{
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
    {
        Callback = callback;
        Data = data;
    }

    public static implicit operator JSCallbackDescriptor(JSCallback callback)
        => new JSCallbackDescriptor(callback);
}
