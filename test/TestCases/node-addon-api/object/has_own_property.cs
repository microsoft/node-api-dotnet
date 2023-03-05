using NodeApi;

namespace NodeApiTest;

public partial class TestObject
{
    private static JSValue HasOwnPropertyWithNapiValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasOwnProperty((JSNativeApi.Interop.napi_value)key);
    }

    private static JSValue HasOwnPropertyWithNapiWrapperValue(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasOwnProperty(key);
    }

    private static JSValue HasOwnPropertyWithLatin1StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasOwnProperty(JSValue.CreateStringLatin1(key.GetValueStringLatin1()));
    }

    private static JSValue HasOwnPropertyWithUtf8StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasOwnProperty(key.GetValueStringUtf8());
    }

    private static JSValue HasOwnPropertyWithCSharpStyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue key = args[1];
        return obj.HasOwnProperty((string)key);
    }
}
