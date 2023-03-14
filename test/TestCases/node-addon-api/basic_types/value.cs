// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;

namespace Microsoft.JavaScript.NodeApiTest;

public class TestBasicTypesValue : TestHelper, ITestObject
{
    private static JSValue IsUndefined(JSCallbackArgs args) => args[0].IsUndefined();
    private static JSValue IsNull(JSCallbackArgs args) => args[0].IsNull();
    private static JSValue IsBoolean(JSCallbackArgs args) => args[0].IsBoolean();
    private static JSValue IsNumber(JSCallbackArgs args) => args[0].IsNumber();
    private static JSValue IsString(JSCallbackArgs args) => args[0].IsString();
    private static JSValue IsSymbol(JSCallbackArgs args) => args[0].IsSymbol();
    private static JSValue IsArray(JSCallbackArgs args) => args[0].IsArray();
    private static JSValue IsArrayBuffer(JSCallbackArgs args) => args[0].IsArrayBuffer();
    private static JSValue IsTypedArray(JSCallbackArgs args) => args[0].IsTypedArray();
    private static JSValue IsObject(JSCallbackArgs args) => args[0].IsObject();
    private static JSValue IsFunction(JSCallbackArgs args) => args[0].IsFunction();
    private static JSValue IsPromise(JSCallbackArgs args) => args[0].IsPromise();
    private static JSValue IsDataView(JSCallbackArgs args) => args[0].IsDataView();
    private static JSValue IsExternal(JSCallbackArgs args) => args[0].IsExternal();
    private static JSValue ToBoolean(JSCallbackArgs args) => args[0].CoerceToBoolean();
    private static JSValue ToNumber(JSCallbackArgs args) => args[0].CoerceToNumber();
    private static JSValue ToString(JSCallbackArgs args) => args[0].CoerceToString();
    private static JSValue ToObject(JSCallbackArgs args) => args[0].CoerceToObject();

    private static JSValue StrictlyEquals(JSCallbackArgs args) => args[0].Equals(args[1]);

    // Helper methods
    private static JSValue CreateDefaultValue(JSCallbackArgs _) => default;
    private static JSValue CreateEmptyValue(JSCallbackArgs _) => new();
    private static JSValue CreateNonEmptyValue(JSCallbackArgs _) => "non_empty_val";
    private static JSValue CreateExternal(JSCallbackArgs _) => JSValue.CreateExternal(1);


    public JSObject Init() => new()
    {
        Method(IsUndefined, nameof(IsUndefined)),
        Method(IsNull, nameof(IsNull)),
        Method(IsBoolean, nameof(IsBoolean)),
        Method(IsNumber, nameof(IsNumber)),
        Method(IsString, nameof(IsString)),
        Method(IsSymbol, nameof(IsSymbol)),
        Method(IsArray, nameof(IsArray)),
        Method(IsArrayBuffer, nameof(IsArrayBuffer)),
        Method(IsTypedArray, nameof(IsTypedArray)),
        Method(IsObject, nameof(IsObject)),
        Method(IsFunction, nameof(IsFunction)),
        Method(IsPromise, nameof(IsPromise)),
        Method(IsDataView, nameof(IsDataView)),
        Method(IsExternal, nameof(IsExternal)),
        Method(ToBoolean, nameof(ToBoolean)),
        Method(ToNumber, nameof(ToNumber)),
        Method(ToString, nameof(ToString)),
        Method(ToObject, nameof(ToObject)),

        Method(StrictlyEquals, nameof(StrictlyEquals)),

        Method(CreateDefaultValue, nameof(CreateDefaultValue)),
        Method(CreateEmptyValue, nameof(CreateEmptyValue)),
        Method(CreateNonEmptyValue, nameof(CreateNonEmptyValue)),
        Method(CreateExternal, nameof(CreateExternal)),
    };
}
