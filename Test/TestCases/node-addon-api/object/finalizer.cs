using NodeApi;

namespace NodeApiTest;

public partial class TestObject
{
    private static JSValue AddFinalizer(JSCallbackArgs args)
    {
        JSValue result = JSValue.CreateObject();
        JSReference objRef = new(result);
        args[0].AddFinalizer(() =>
        {
            if (objRef.GetValue() is JSValue value)
            {
                value.SetProperty("finalizerCalled", true);
            }
            objRef.Dispose();
        });
        return result;
    }
}
