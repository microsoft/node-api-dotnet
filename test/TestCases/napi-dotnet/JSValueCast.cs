// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma warning disable IDE0060 // Unused parameters
#pragma warning disable IDE0301 // Collection initialization can be simplified

using System;
using System.Collections.Generic;
using System.Numerics;
using Interop = Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi.TestCases;

/// <summary>
/// Tests type casting between JSValue and IJSValue derived types.
/// </summary>
[JSExport]
public static class JSValueCast
{
    #region JSAbortSignal

    public static string ValueAsAbortSignal(JSValue value)
        => (Interop.JSAbortSignal?)value is not null ? "ok" : "failed";

    public static string ValueIsAbortSignal(JSValue value)
        => value.Is<Interop.JSAbortSignal>() ? "ok" : "failed";

    public static string ValueCastToAbortSignal(JSValue value)
    {
        try
        {
            Interop.JSAbortSignal signal = (Interop.JSAbortSignal)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSArray

    public static string ValueAsArray(JSValue value)
        => (JSArray?)value is not null ? "ok" : "failed";

    public static string ValueIsArray(JSValue value)
        => value.Is<JSArray>() ? "ok" : "failed";

    public static string ValueCastToArray(JSValue value)
    {
        try
        {
            JSArray signal = (JSArray)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSAsyncIterable

    public static JSValue GetAsyncIterator(JSValue value)
        => JSValue.Global["Symbol"]["asyncIterator"];

    public static string ValueAsAsyncIterable(JSValue value)
        => (JSAsyncIterable?)value is not null ? "ok" : "failed";

    public static string ValueIsAsyncIterable(JSValue value)
        => value.Is<JSAsyncIterable>() ? "ok" : "failed";

    public static string ValueCastToAsyncIterable(JSValue value)
    {
        try
        {
            JSAsyncIterable signal = (JSAsyncIterable)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSBigInt

    public static string ValueAsBigInt(JSValue value)
        => (JSBigInt?)value is not null ? "ok" : "failed";

    public static string ValueIsBigInt(JSValue value)
        => value.Is<JSBigInt>() ? "ok" : "failed";

    public static string ValueCastToBigInt(JSValue value)
    {
        try
        {
            JSBigInt signal = (JSBigInt)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSDate

    public static string ValueAsDate(JSValue value)
        => (JSDate?)value is not null ? "ok" : "failed";

    public static string ValueIsDate(JSValue value)
        => value.Is<JSDate>() ? "ok" : "failed";

    public static string ValueCastToDate(JSValue value)
    {
        try
        {
            JSDate signal = (JSDate)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSFunction

    public static string ValueAsFunction(JSValue value)
        => (JSFunction?)value is not null ? "ok" : "failed";

    public static string ValueIsFunction(JSValue value)
        => value.Is<JSFunction>() ? "ok" : "failed";

    public static string ValueCastToFunction(JSValue value)
    {
        try
        {
            JSFunction signal = (JSFunction)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion
}
