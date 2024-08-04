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

    #region JSIterable

    public static string ValueAsIterable(JSValue value)
        => (JSIterable?)value is not null ? "ok" : "failed";

    public static string ValueIsIterable(JSValue value)
        => value.Is<JSIterable>() ? "ok" : "failed";

    public static string ValueCastToIterable(JSValue value)
    {
        try
        {
            JSIterable signal = (JSIterable)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSMap

    public static string ValueAsMap(JSValue value)
        => (JSMap?)value is not null ? "ok" : "failed";

    public static string ValueIsMap(JSValue value)
        => value.Is<JSMap>() ? "ok" : "failed";

    public static string ValueCastToMap(JSValue value)
    {
        try
        {
            JSMap signal = (JSMap)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSObject

    public static string ValueAsObject(JSValue value)
        => (JSObject?)value is not null ? "ok" : "failed";

    public static string ValueIsObject(JSValue value)
        => value.Is<JSObject>() ? "ok" : "failed";

    public static string ValueCastToObject(JSValue value)
    {
        try
        {
            JSObject signal = (JSObject)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSPromise

    public static string ValueAsPromise(JSValue value)
        => (JSPromise?)value is not null ? "ok" : "failed";

    public static string ValueIsPromise(JSValue value)
        => value.Is<JSPromise>() ? "ok" : "failed";

    public static string ValueCastToPromise(JSValue value)
    {
        try
        {
            JSPromise signal = (JSPromise)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSProxy

    public static string ValueAsProxy(JSValue value)
        => (JSProxy?)value is not null ? "ok" : "failed";

    public static string ValueIsProxy(JSValue value)
        => value.Is<JSProxy>() ? "ok" : "failed";

    public static string ValueCastToProxy(JSValue value)
    {
        try
        {
            JSProxy signal = (JSProxy)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSSet

    public static string ValueAsSet(JSValue value)
        => (JSSet?)value is not null ? "ok" : "failed";

    public static string ValueIsSet(JSValue value)
        => value.Is<JSSet>() ? "ok" : "failed";

    public static string ValueCastToSet(JSValue value)
    {
        try
        {
            JSSet signal = (JSSet)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSSymbol

    public static string ValueAsSymbol(JSValue value)
        => (JSSymbol?)value is not null ? "ok" : "failed";

    public static string ValueIsSymbol(JSValue value)
        => value.Is<JSSymbol>() ? "ok" : "failed";

    public static string ValueCastToSymbol(JSValue value)
    {
        try
        {
            JSSymbol signal = (JSSymbol)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<sbyte>

    public static string ValueAsTypedArrayInt8(JSValue value)
        => (JSTypedArray<sbyte>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayInt8(JSValue value)
        => value.Is<JSTypedArray<sbyte>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayInt8(JSValue value)
    {
        try
        {
            JSTypedArray<sbyte> signal = (JSTypedArray<sbyte>)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<byte>

    public static string ValueAsTypedArrayUint8(JSValue value)
        => (JSTypedArray<byte>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayUint8(JSValue value)
        => value.Is<JSTypedArray<byte>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayUint8(JSValue value)
    {
        try
        {
            JSTypedArray<byte> signal = (JSTypedArray<byte>)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<short>

    public static string ValueAsTypedArrayInt16(JSValue value)
        => (JSTypedArray<short>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayInt16(JSValue value)
        => value.Is<JSTypedArray<short>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayInt16(JSValue value)
    {
        try
        {
            JSTypedArray<short> signal = (JSTypedArray<short>)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<ushort>

    public static string ValueAsTypedArrayUint16(JSValue value)
        => (JSTypedArray<ushort>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayUint16(JSValue value)
        => value.Is<JSTypedArray<ushort>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayUint16(JSValue value)
    {
        try
        {
            JSTypedArray<ushort> signal = (JSTypedArray<ushort>)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<int>

    public static string ValueAsTypedArrayInt32(JSValue value)
        => (JSTypedArray<int>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayInt32(JSValue value)
        => value.Is<JSTypedArray<int>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayInt32(JSValue value)
    {
        try
        {
            JSTypedArray<int> signal = (JSTypedArray<int>)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<uint>

    public static string ValueAsTypedArrayUint32(JSValue value)
        => (JSTypedArray<uint>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayUint32(JSValue value)
        => value.Is<JSTypedArray<uint>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayUint32(JSValue value)
    {
        try
        {
            JSTypedArray<uint> signal = (JSTypedArray<uint>)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<long>

    public static string ValueAsTypedArrayBigInt64(JSValue value)
        => (JSTypedArray<long>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayBigInt64(JSValue value)
        => value.Is<JSTypedArray<long>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayBigInt64(JSValue value)
    {
        try
        {
            JSTypedArray<long> signal = (JSTypedArray<long>)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<ulong>

    public static string ValueAsTypedArrayBigUint64(JSValue value)
        => (JSTypedArray<ulong>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayBigUint64(JSValue value)
        => value.Is<JSTypedArray<ulong>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayBigUint64(JSValue value)
    {
        try
        {
            JSTypedArray<ulong> signal = (JSTypedArray<ulong>)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<float>

    public static string ValueAsTypedArrayFloat32(JSValue value)
        => (JSTypedArray<float>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayFloat32(JSValue value)
        => value.Is<JSTypedArray<float>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayFloat32(JSValue value)
    {
        try
        {
            JSTypedArray<float> signal = (JSTypedArray<float>)value;
            JSValue value2 = signal;
            return value.Handle == value2.Handle ? "ok" : "failed roundrip";
        }
        catch (InvalidCastException)
        {
            return "failed";
        }
    }

    #endregion

    #region JSTypedArray<double>

    public static string ValueAsTypedArrayFloat64(JSValue value)
        => (JSTypedArray<double>?)value is not null ? "ok" : "failed";

    public static string ValueIsTypedArrayFloat64(JSValue value)
        => value.Is<JSTypedArray<double>>() ? "ok" : "failed";

    public static string ValueCastToTypedArrayFloat64(JSValue value)
    {
        try
        {
            JSTypedArray<double> signal = (JSTypedArray<double>)value;
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
