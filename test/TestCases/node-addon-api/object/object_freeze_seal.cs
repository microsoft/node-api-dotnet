using Microsoft.JavaScript.NodeApi;

namespace Microsoft.JavaScript.NodeApiTest;

public partial class TestObjectFreezeSeal : TestHelper, ITestObject
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

    public static JSObject Init() => new()
    {
        Method(Freeze),
        Method(Seal),
    };
}
