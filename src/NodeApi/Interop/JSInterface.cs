// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Base class for a dynamically generated interface adapter that enables a JS object to implement
/// a .NET interface. Holds a reference to the underlying JS value.
/// </summary>
public abstract class JSInterface
{
    private readonly JSReference _jsReference;

    protected JSInterface(JSValue value)
    {
        _jsReference = new JSReference(value, isWeak: false);
    }

    public static JSValue? GetJSValue(object obj)
        => (obj as JSInterface)?.Value;

    /// <summary>
    /// Gets the underlying JS value. (The property name is prefixed with `__` to avoid
    /// possible conflicts with interface
    /// </summary>
    protected internal JSValue Value => _jsReference.GetValue()!.Value;
}
