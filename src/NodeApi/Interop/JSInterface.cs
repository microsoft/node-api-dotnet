// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Base class for a dynamically generated interface adapter that enables a JS object to implement
/// a .NET interface. Holds a reference to the underlying JS value.
/// </summary>
public abstract class JSInterface
{
    protected JSInterface(JSValue value)
    {
        ValueReference = new JSReference(value, isWeak: false);
    }

    /// <summary>
    /// Gets the JS value for a .NET object, if the object is a proxy to a JS object that
    /// implements a .NET interface.
    /// </summary>
    public static JSValue? GetJSValue(object obj) => (obj as JSInterface)?.Value;

    /// <summary>
    /// Gets a reference to the underlying JS value.
    /// </summary>
    protected JSReference ValueReference { get; }

    /// <summary>
    /// Gets the underlying JS value.
    /// </summary>
    protected JSValue Value => ValueReference.GetValue();

    /// <summary>
    /// Dynamically invokes an interface method JS adapter delegate after obtaining the JS `this`
    /// value from the reference. Automatically switches to the JS thread if needed.
    /// </summary>
    /// <param name="interfaceMethod">Interface method JS adapter delegate.</param>
    /// <param name="args">Array of method arguments starting at index 1. Index 0 is reserved
    /// for the JS `this` value.</param>
    /// <remarks>
    /// This method is used by the dynamically-emitted interface marshalling code.
    /// </remarks>
    protected object? DynamicInvoke(Delegate interfaceMethod, object?[] args)
    {
        return ValueReference.Run((value) =>
        {
            args[0] = value;
            return interfaceMethod.DynamicInvoke(args);
        });
    }

    /// <summary>
    /// Dynamically invokes an interface method JS adapter delegate after obtaining the JS `this`
    /// value from the reference. Automatically switches to the JS thread if needed.
    /// </summary>
    /// <param name="interfaceMethod">Interface method to invoke.</param>
    /// <param name="delegateProvider">Callback function that returns a JS adapter delegate
    /// for the interface method. The callback runs on the JS thread.</param>
    /// <param name="args">Array of method arguments starting at index 1. Index 0 is reserved
    /// for the JS `this` value.</param>
    /// <remarks>
    /// This method is used by the dynamically-emitted interface marshalling code.
    /// </remarks>
    protected object? DynamicInvoke(
        MethodInfo interfaceMethod,
        Func<MethodInfo, Delegate> delegateProvider,
        object?[] args)
    {
        return ValueReference.Run((value) =>
        {
            args[0] = value;
            Delegate interfaceMethodDelegate = delegateProvider(interfaceMethod);
            return interfaceMethodDelegate.DynamicInvoke(args);
        });
    }
}
