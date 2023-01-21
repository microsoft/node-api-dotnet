using NodeApi;

namespace NodeApiTest;

public partial class TestObject
{
    private static JSValue SubscriptGetWithLatin1StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        byte[] key = args[1].GetValueStringLatin1();
        return obj[JSValue.CreateStringLatin1(key)];
    }

    private static JSValue SubscriptGetWithUtf8StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        byte[] key = args[1].GetValueStringUtf8();
        return obj[key];
    }

    private static JSValue SubscriptGetWithCSharpStyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        string key = (string)args[1];
        return obj[key];
    }

    private static JSValue SubscriptGetAtIndex(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        uint index = (uint)args[1];
        return obj[index];
    }

    private static JSValue SubscriptSetWithLatin1StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        byte[] key = args[1].GetValueStringLatin1();
        JSValue value = args[2];
        obj[JSValue.CreateStringLatin1(key)] = value;
        return JSValue.Undefined;
    }

    private static JSValue SubscriptSetWithUtf8StyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        byte[] key = args[1].GetValueStringUtf8();
        JSValue value = args[2];
        obj[key] = value;
        return JSValue.Undefined;
    }

    private static JSValue SubscriptSetWithCSharpStyleString(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        string key = (string)args[1];
        JSValue value = args[2];
        obj[key] = value;
        return JSValue.Undefined;
    }

    private static JSValue SubscriptSetAtIndex(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        uint index = (uint)args[1];
        JSValue value = args[2];
        obj[index] = value;
        return JSValue.Undefined;
    }
}
