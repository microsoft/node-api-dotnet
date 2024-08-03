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
    [JSExport("testAbortSignalAs")]
    public static string TestAbortSignalAs(JSValue value)
    {
        return (Interop.JSAbortSignal?)value is not null ? "ok" : "fail";
    }

    [JSExport("testAbortSignalIs")]
    public static string TestAbortSignalIs(JSValue value)
    {
        return value.Is<Interop.JSAbortSignal>() ? "ok" : "fail";
    }

    [JSExport("testAbortSignalCast")]
    public static string TestAbortSignalCast(JSValue value)
    {
        try
        {
            Interop.JSAbortSignal signal = (Interop.JSAbortSignal)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "fail roundrip";
        }
        catch (InvalidCastException)
        {
            return "fail";
        }
    }
}
