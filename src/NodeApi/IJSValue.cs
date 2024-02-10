// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

#if !NET7_0_OR_GREATER
using System.Reflection;
#endif

namespace Microsoft.JavaScript.NodeApi;

// A static interface that helps with the conversion of JSValue to a specific type.
public interface IJSValue<TSelf> : IEquatable<JSValue> where TSelf : struct, IJSValue<TSelf>
{
    public JSValue AsJSValue();

#if NET7_0_OR_GREATER
    public static abstract bool CanCreateFrom(JSValue value);

    public static abstract TSelf CreateUnchecked(JSValue value);
#endif
}

#if !NET7_0_OR_GREATER
// A static class that helps with the conversion of JSValue to a specific type.
internal static class IJSValueShim<T> where T : struct
{
    private static readonly Func<JSValue, bool> s_canBeCreatedFrom =
        (Func<JSValue, bool>)Delegate.CreateDelegate(
            typeof(Func<JSValue, bool>),
            typeof(T).GetMethod(
                "CanCreateFrom",
                BindingFlags.Static | BindingFlags.Public)!);

    private static readonly Func<JSValue, T>s_createUnchecked =
        (Func<JSValue, T>)Delegate.CreateDelegate(
            typeof(Func<JSValue, T>),
            typeof(T).GetMethod(
                "CreateUnchecked",
                BindingFlags.Static | BindingFlags.NonPublic)!);

    public static bool CanCreateFrom(JSValue value) => s_canBeCreatedFrom(value);

    public static T CreateUnchecked(JSValue value) => s_createUnchecked(value);
}
#endif
