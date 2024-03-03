// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if !NET7_0_OR_GREATER
using System;
using System.Reflection;
#endif

namespace Microsoft.JavaScript.NodeApi;

#if NET7_0_OR_GREATER
// A static interface that helps with the conversion of JSValue to a specific type.
public interface IJSValue<TSelf> where TSelf : struct, IJSValue<TSelf>
{
    public static abstract bool CanBeConvertedFrom(JSValue value);

    public static abstract TSelf CreateUnchecked(JSValue value);
}
#else
// A static class that helps with the conversion of JSValue to a specific type.
public static class IJSValueShim<T> where T : struct
{
    private static readonly Func<JSValue, bool> s_canBeConvertedFrom =
        (Func<JSValue, bool>)Delegate.CreateDelegate(
            typeof(Func<JSValue, bool>),
            typeof(T).GetMethod(
                nameof(JSObject.CanBeConvertedFrom),
                BindingFlags.Static | BindingFlags.Public)!);

    private static readonly Func<JSValue, T>s_createUnchecked =
        (Func<JSValue, T>)Delegate.CreateDelegate(
            typeof(Func<JSValue, T>),
            typeof(T).GetMethod(
                nameof(JSObject.CreateUnchecked),
                BindingFlags.Static | BindingFlags.Public)!);

    public static bool CanBeConvertedFrom(JSValue value) => s_canBeConvertedFrom(value);

    public static T CreateUnchecked(JSValue value) => s_createUnchecked(value);
}
#endif
