using NodeApi;

namespace NodeApiTest;

public partial class TestObject
{
    private static JSValue GetPropertyWithNapiValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.GetProperty((JSNativeApi.Interop.napi_value)key);
    }

    private static JSValue GetPropertyWithNapiWrapperValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.GetProperty(key);
    }

    private static JSValue GetPropertyWithLatin1StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.GetProperty(JSValue.CreateStringLatin1(key.GetValueStringLatin1()));
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
