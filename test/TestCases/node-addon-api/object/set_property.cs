// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;

namespace Microsoft.JavaScript.NodeApiTest;

public partial class TestObject
{
    private static JSValue SetPropertyWithNapiValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        JSValue value = args[2];
        obj.SetProperty((JSRuntime.napi_value)key, value);
        return JSValue.Undefined;
    }

    private static JSValue SetPropertyWithNapiWrapperValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        JSValue value = args[2];
        obj.SetProperty(key, value);
        return JSValue.Undefined;
    }

    private static JSValue SetPropertyWithUtf8StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        JSValue value = args[2];
        obj.SetProperty(key.GetValueStringUtf8(), value);
        return JSValue.Undefined;
    }

    private static JSValue SetPropertyWithCSharpStyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        JSValue value = args[2];
        obj.SetProperty((string)key, value);
        return JSValue.Undefined;
    }

    private static JSValue SetPropertyWithUInt32(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        JSValue value = args[2];
        obj.SetProperty((uint)key, value);
        return JSValue.Undefined;
    }
}
