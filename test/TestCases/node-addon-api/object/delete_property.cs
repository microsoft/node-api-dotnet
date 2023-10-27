// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;

namespace Microsoft.JavaScript.NodeApiTest;

public partial class TestObject
{
    private static JSValue DeletePropertyWithNapiValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.DeleteProperty((JSRuntime.napi_value)key);
    }

    private static JSValue DeletePropertyWithNapiWrapperValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.DeleteProperty(key);
    }

    private static JSValue DeletePropertyWithUtf8StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.DeleteProperty(key.GetValueStringUtf8());
    }

    private static JSValue DeletePropertyWithCSharpStyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.DeleteProperty((string)key);
    }

    private static JSValue DeletePropertyWithUInt32(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.DeleteProperty((uint)key);
    }
}
