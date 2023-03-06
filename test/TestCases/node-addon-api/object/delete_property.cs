using System;
using NodeApi;

namespace NodeApiTest;

public partial class TestObject
{
    private static JSValue DeletePropertyWithNapiValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.DeleteProperty((JSNativeApi.Interop.napi_value)key);
    }

    private static JSValue DeletePropertyWithNapiWrapperValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.DeleteProperty(key);
    }

    private static JSValue DeletePropertyWithLatin1StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.DeleteProperty(JSValue.CreateStringLatin1(key.GetValueStringLatin1()));
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
