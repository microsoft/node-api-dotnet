// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

#if !NET7_0_OR_GREATER
using System.Reflection;
#endif

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// A base interface for a struct that represents a JavaScript value type or a built-in
/// object type. It provides functionality for converting between the struct
/// and <see cref="JSValue"/>.
/// </summary>
/// <typeparam name="TSelf">The derived struct type.</typeparam>
public interface IJSValue<TSelf> : IEquatable<JSValue> where TSelf : struct, IJSValue<TSelf>
{
    /// <summary>
    /// Converts the derived struct `TSelf` to a <see cref="JSValue"/> with
    /// the same `napi_value` handle.
    /// </summary>
    /// <returns>
    /// <see cref="JSValue"/> with the same `napi_value` handle as the derived struct.
    /// </returns>
    JSValue AsJSValue();

#if NET7_0_OR_GREATER
    /// <summary>
    /// Checks id the derived struct `TSelf` can be created from a <see cref="JSValue"/>.
    /// </summary>
    static abstract bool CanCreateFrom(JSValue value);

    /// <summary>
    /// Creates a new instance of the derived struct `TSelf` from a <see cref="JSValue"/> without
    /// checking the enclosed handle type.
    /// </summary>
    static abstract TSelf CreateUnchecked(JSValue value);
#endif
}

#if !NET7_0_OR_GREATER
/// <summary>
/// Implements IJSValue interface static functions for the previous .Net versions.
/// </summary>
/// <typeparam name="T"></typeparam>
internal static class IJSValueShim<T> where T : struct, IJSValue<T>
{
    /// <summary>
    /// A static field to keep a reference to the CanCreateFrom public method.
    /// </summary>
    private static readonly Func<JSValue, bool> s_canBeCreatedFrom =
        (Func<JSValue, bool>)Delegate.CreateDelegate(
            typeof(Func<JSValue, bool>),
            typeof(T).GetMethod(
                "CanCreateFrom",
                BindingFlags.Static | BindingFlags.Public)!);

    /// <summary>
    /// A static field to keep a reference to the CreateUnchecked private method.
    /// </summary>
    private static readonly Func<JSValue, T>s_createUnchecked =
        (Func<JSValue, T>)Delegate.CreateDelegate(
            typeof(Func<JSValue, T>),
            typeof(T).GetMethod(
                "CreateUnchecked",
                BindingFlags.Static | BindingFlags.NonPublic)!);

    /// <summary>
    /// Invokes `T.CanCreateFrom` static public method.
    /// </summary>
    public static bool CanCreateFrom(JSValue value) => s_canBeCreatedFrom(value);

    /// <summary>
    /// Invokes `T.CreateUnchecked` static private method.
    /// </summary>
    public static T CreateUnchecked(JSValue value) => s_createUnchecked(value);
}
#endif
