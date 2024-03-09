// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;

namespace Microsoft.JavaScript.NodeApiTest;

public partial class TestObject
{
    private static JSValue HasPropertyWithNapiValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasProperty((JSRuntime.napi_value)key);
    }

    private static JSValue HasPropertyWithNapiWrapperValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasProperty(key);
    }

    private static JSValue HasPropertyWithUtf8StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasProperty(JSValue.CreateStringUtf8(key.GetValueStringUtf8()));
    }

    private static JSValue HasPropertyWithCSharpStyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasProperty((string)key);
    }

    private static JSValue HasPropertyWithUInt32(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasProperty((uint)key);
    }
}
