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
    /// Checks if the T struct can be created from this instance`.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// `true` if the T struct can be created from this instance. Otherwise it returns `false`.
    /// </returns>
    bool Is<T>() where T : struct, IJSValue<T>;

    /// <summary>
    /// Tries to create a T struct from this instance.
    /// It returns `null` if the T struct cannot be created.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// Nullable value that contains T struct if it was successfully created
    /// or `null` if it was failed.
    /// </returns>
    T? As<T>() where T : struct, IJSValue<T>;

    /// <summary>
    /// Creates a T struct from this instance without checking the enclosed handle type.
    /// It must be used only when the handle type is known to be correct.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    T AsUnchecked<T>() where T : struct, IJSValue<T>;

    /// <summary>
    /// Creates a T struct from this instance.
    /// It throws `InvalidCastException` in case of failure.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be crated based on this instance.
    /// </exception>
    T CastTo<T>() where T : struct, IJSValue<T>;

#if NET7_0_OR_GREATER
    /// <summary>
    /// Checks if the derived struct `TSelf` can be created from a <see cref="JSValue"/>.
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
    /// A static field to keep a reference to the CanCreateFrom private method.
    /// </summary>
    private static readonly Func<JSValue, bool> s_canCreateFrom =
        (Func<JSValue, bool>)Delegate.CreateDelegate(
            typeof(Func<JSValue, bool>),
            typeof(T).GetMethod(
                nameof(CanCreateFrom),
                BindingFlags.Static | BindingFlags.NonPublic)!);

    /// <summary>
    /// A static field to keep a reference to the CreateUnchecked private method.
    /// </summary>
    private static readonly Func<JSValue, T> s_createUnchecked =
        (Func<JSValue, T>)Delegate.CreateDelegate(
            typeof(Func<JSValue, T>),
            typeof(T).GetMethod(
                nameof(CreateUnchecked),
                BindingFlags.Static | BindingFlags.NonPublic)!);

    /// <summary>
    /// Invokes `T.CanCreateFrom` static public method.
    /// </summary>
    public static bool CanCreateFrom(JSValue value) => s_canCreateFrom(value);

    /// <summary>
    /// Invokes `T.CreateUnchecked` static private method.
    /// </summary>
    public static T CreateUnchecked(JSValue value) => s_createUnchecked(value);
}
#endif
