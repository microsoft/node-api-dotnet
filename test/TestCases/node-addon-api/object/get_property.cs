// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;

namespace Microsoft.JavaScript.NodeApiTest;

public partial class TestObject
{
    private static JSValue GetPropertyWithNapiValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.GetProperty((JSRuntime.napi_value)key);
    }

    private static JSValue GetPropertyWithNapiWrapperValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.GetProperty(key);
    }

    private static JSValue GetPropertyWithUtf8StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.GetProperty(key.GetValueStringUtf8());
    }

    private static JSValue GetPropertyWithCSharpStyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.GetProperty((string)key);
    }

    private static JSValue GetPropertyWithUInt32(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.GetProperty((uint)key);
    }
}
