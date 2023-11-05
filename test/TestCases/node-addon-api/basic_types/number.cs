// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;

namespace Microsoft.JavaScript.NodeApiTest;

#pragma warning disable IDE0060 // Unused parameter 'args'

public class TestBasicTypesNumber : TestHelper, ITestObject
{
    private static JSValue ToInt32(JSCallbackArgs args) => (int)args[0];
    private static JSValue ToUInt32(JSCallbackArgs args) => (uint)args[0];
    private static JSValue ToInt64(JSCallbackArgs args) => (long)args[0];
    private static JSValue ToFloat(JSCallbackArgs args) => (float)args[0];
    private static JSValue ToDouble(JSCallbackArgs args) => (double)args[0];
    private static JSValue MinFloat(JSCallbackArgs args) => float.Epsilon;
    private static JSValue MaxFloat(JSCallbackArgs args) => float.MaxValue;
    private static JSValue MinDouble(JSCallbackArgs args) => double.Epsilon;
    private static JSValue MaxDouble(JSCallbackArgs args) => double.MaxValue;
    private static JSValue OperatorInt32(JSCallbackArgs args) => (int)args[0] == args[0].GetValueInt32();
    private static JSValue OperatorUInt32(JSCallbackArgs args) => (uint)args[0] == args[0].GetValueUInt32();
    private static JSValue OperatorInt64(JSCallbackArgs args) => (long)args[0] == args[0].GetValueInt64();
    private static JSValue OperatorFloat(JSCallbackArgs args) => (float)args[0] == (float)args[0].GetValueDouble();
    private static JSValue OperatorDouble(JSCallbackArgs args) => (double)args[0] == args[0].GetValueDouble();

    public JSObject Init() => new()
    {
        Method(ToInt32),
        Method(ToUInt32),
        Method(ToInt64),
        Method(ToFloat),
        Method(ToDouble),
        Method(MinFloat),
        Method(MaxFloat),
        Method(MinDouble),
        Method(MaxDouble),
        Method(OperatorInt32),
        Method(OperatorUInt32),
        Method(OperatorInt64),
        Method(OperatorFloat),
        Method(OperatorDouble),
    };
}
