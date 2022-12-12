using NodeApi;

namespace NodeApiTest;

public partial class TestObjectFreezeSeal
{
    private static JSValue Freeze(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        obj.Freeze();
        return true;
    }

    private static JSValue Seal(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        obj.Seal();
        return true;
    }

    public static JSValue Init()
    {
        JSValue exports = JSNativeApi.CreateObject();
        exports["freeze"] = (JSCallback)Freeze;
        exports["seal"] = (JSCallback)Seal;
        return exports;
    }
}
