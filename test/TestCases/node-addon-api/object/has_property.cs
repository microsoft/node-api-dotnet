using NodeApi;

namespace NodeApiTest;

public partial class TestObject
{
    private static JSValue HasPropertyWithNapiValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasProperty((JSNativeApi.Interop.napi_value)key);
    }

    private static JSValue HasPropertyWithNapiWrapperValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasProperty(key);
    }

    private static JSValue HasPropertyWithLatin1StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasProperty(JSValue.CreateStringLatin1(key.GetValueStringLatin1()));
    }

    private static JSValue HasPropertyWithUtf8StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasProperty(key.GetValueStringUtf8());
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
