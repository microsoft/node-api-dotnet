// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Runtime;

namespace Microsoft.JavaScript.NodeApiTest;

public partial class TestObject
{
    private static JSValue HasOwnPropertyWithNapiValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasOwnProperty((JSRuntime.napi_value)key);
    }

    private static JSValue HasOwnPropertyWithNapiWrapperValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasOwnProperty(key);
    }

    private static JSValue HasOwnPropertyWithUtf8StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasOwnProperty(JSValue.CreateStringUtf8(key.GetValueStringUtf8()));
    }

    private static JSValue HasOwnPropertyWithCSharpStyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasOwnProperty((string)key);
    }
}
