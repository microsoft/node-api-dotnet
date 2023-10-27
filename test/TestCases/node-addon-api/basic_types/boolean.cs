// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;

namespace Microsoft.JavaScript.NodeApiTest;

public class TestBasicTypesBoolean : TestHelper, ITestObject
{
    private static JSValue CreateBoolean(JSCallbackArgs args)
        => JSValue.GetBoolean((bool)args[0]);

    private static JSValue CreateBooleanFromPrimitive(JSCallbackArgs args)
        => (bool)args[0];

    public JSObject Init() => new()
    {
        Method(CreateBoolean),
        Method(CreateBooleanFromPrimitive),
    };
}
