using NodeApi;

namespace NodeApiTest;

public partial class TestObject
{
    private static bool s_testValue = true;

    // Used to test void* Data() integrity
    private class UserDataHolder
    {
        public int Value { get; set; }
    }

    private static JSValue TestGetter(JSCallbackArgs args)
    {
        return JSNativeApi.GetBoolean(s_testValue);
    }

    private static JSValue TestSetter(JSCallbackArgs args)
    {
        s_testValue = (bool)args[0];
        return JSValue.Undefined;
    }

    private static JSValue TestGetterWithUserData(JSCallbackArgs args)
    {
        if (args.Data is UserDataHolder holder)
        {
            return (JSValue)holder.Value;
        }
        return JSValue.Undefined;
    }

    private static JSValue TestSetterWithUserData(JSCallbackArgs args)
    {
        if (args.Data is UserDataHolder holder)
        {
            holder.Value = (int)args[0];
        }
        return JSValue.Undefined;
    }

    private static JSValue TestFunction(JSCallbackArgs args)
    {
        return true;
    }

    private static JSValue TestFunctionWithUserData(JSCallbackArgs args)
    {
        if (args.Data is UserDataHolder holder)
        {
            return holder.Value;
        }
        return JSValue.Undefined;
    }

    private static JSValue GetPropertyNames(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        return obj.GetPropertyNames();
    }

    private static JSValue DefineProperties(JSCallbackArgs args)
    {
        JSValue obj = args[0];

        JSValue trueValue = JSValue.True;
        UserDataHolder holder = new()
        {
            Value = 1234
        };

        obj.DefineProperties(
            JSPropertyDescriptor.Accessor("readonlyAccessor", TestGetter),
            JSPropertyDescriptor.Accessor("readWriteAccessor", TestGetter, TestSetter),
            JSPropertyDescriptor.Accessor("readonlyAccessorWithUserData",
                                          TestGetterWithUserData,
                                          data: holder),
            JSPropertyDescriptor.Accessor("readWriteAccessorWithUserData",
                                          TestGetterWithUserData,
                                          TestSetterWithUserData,
                                          data: holder),
            JSPropertyDescriptor.ForValue("readonlyValue", trueValue),
            JSPropertyDescriptor.ForValue("readWriteValue", trueValue, JSPropertyAttributes.Writable),
            JSPropertyDescriptor.ForValue("enumerableValue", trueValue, JSPropertyAttributes.Enumerable),
            JSPropertyDescriptor.ForValue("configurableValue", trueValue, JSPropertyAttributes.Configurable),
            JSPropertyDescriptor.Function("function", TestFunction),
            JSPropertyDescriptor.Function("functionWithUserData",
                                          TestFunctionWithUserData,
                                          data: holder)
        );
        return JSValue.Undefined;
    }

    private static JSValue DefineValueProperty(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue name = args[1];
        JSValue value = args[2];
        obj.DefineProperties(JSPropertyDescriptor.ForValue(name, value));
        return JSValue.Undefined;
    }

    private static JSValue CreateObjectUsingMagic(JSCallbackArgs args)
    {
        JSValue obj = JSNativeApi.CreateObject();
        obj["cp_false"] = false;
        obj["cp_true"] = true;
        obj["s_true"] = true;
        obj["s_false"] = false;
        obj["0"] = 0;
        obj[(uint)42] = 120;
        obj["0.0f"] = 0.0f;
        obj["0.0"] = 0.0;
        obj["-1"] = -1;
        obj["foo2"] = "foo"u8;
        obj["foo4"] = "foo";
        obj["circular"] = obj;
        obj["circular2"] = obj;
        return obj;
    }

    private static JSValue Sum(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        long sum = 0;

        foreach ((JSValue _, JSValue value) in obj)
        {
            sum += (long)value;
        }

        return sum;
    }

    private static JSValue Increment(JSCallbackArgs args)
    {
        JSValue obj = args[0];

        foreach ((JSValue name, JSValue value) in obj)
        {
            obj[name] = (long)value + 1;
        }

        return JSValue.Undefined;
    }

    private static JSValue InstanceOf(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue constructor = args[1];
        return obj.InstanceOf(constructor);
    }

    public static JSValue Init()
    {
        JSValue exports = JSNativeApi.CreateObject();

        exports["GetPropertyNames"] = (JSCallback)GetPropertyNames;
        exports["defineProperties"] = (JSCallback)DefineProperties;
        exports["defineValueProperty"] = (JSCallback)DefineValueProperty;

        exports["getPropertyWithNapiValue"] = (JSCallback)GetPropertyWithNapiValue;
        exports["getPropertyWithNapiWrapperValue"] = (JSCallback)GetPropertyWithNapiWrapperValue;
        exports["getPropertyWithLatin1StyleString"] = (JSCallback)GetPropertyWithLatin1StyleString;
        exports["getPropertyWithUtf8StyleString"] = (JSCallback)GetPropertyWithUtf8StyleString;
        exports["getPropertyWithCSharpStyleString"] = (JSCallback)GetPropertyWithCSharpStyleString;
        exports["getPropertyWithUInt32"] = (JSCallback)GetPropertyWithUInt32;

        exports["setPropertyWithNapiValue"] = (JSCallback)SetPropertyWithNapiValue;
        exports["setPropertyWithNapiWrapperValue"] = (JSCallback)SetPropertyWithNapiWrapperValue;
        exports["setPropertyWithLatin1StyleString"] = (JSCallback)SetPropertyWithLatin1StyleString;
        exports["setPropertyWithUtf8StyleString"] = (JSCallback)SetPropertyWithUtf8StyleString;
        exports["setPropertyWithCSharpStyleString"] = (JSCallback)SetPropertyWithCSharpStyleString;
        exports["setPropertyWithUInt32"] = (JSCallback)SetPropertyWithUInt32;

        exports["deletePropertyWithNapiValue"] = (JSCallback)DeletePropertyWithNapiValue;
        exports["deletePropertyWithNapiWrapperValue"] = (JSCallback)DeletePropertyWithNapiWrapperValue;
        exports["deletePropertyWithLatin1StyleString"] = (JSCallback)DeletePropertyWithLatin1StyleString;
        exports["deletePropertyWithUtf8StyleString"] = (JSCallback)DeletePropertyWithUtf8StyleString;
        exports["deletePropertyWithCSharpStyleString"] = (JSCallback)DeletePropertyWithCSharpStyleString;
        exports["deletePropertyWithUInt32"] = (JSCallback)DeletePropertyWithUInt32;

        exports["hasOwnPropertyWithNapiValue"] = (JSCallback)HasOwnPropertyWithNapiValue;
        exports["hasOwnPropertyWithNapiWrapperValue"] = (JSCallback)HasOwnPropertyWithNapiWrapperValue;
        exports["hasOwnPropertyWithLatin1StyleString"] = (JSCallback)HasOwnPropertyWithLatin1StyleString;
        exports["hasOwnPropertyWithUtf8StyleString"] = (JSCallback)HasOwnPropertyWithUtf8StyleString;
        exports["hasOwnPropertyWithCSharpStyleString"] = (JSCallback)HasOwnPropertyWithCSharpStyleString;

        exports["hasPropertyWithNapiValue"] = (JSCallback)HasPropertyWithNapiValue;
        exports["hasPropertyWithNapiWrapperValue"] = (JSCallback)HasPropertyWithNapiWrapperValue;
        exports["hasPropertyWithLatin1StyleString"] = (JSCallback)HasPropertyWithLatin1StyleString;
        exports["hasPropertyWithUtf8StyleString"] = (JSCallback)HasPropertyWithUtf8StyleString;
        exports["hasPropertyWithCSharpStyleString"] = (JSCallback)HasPropertyWithCSharpStyleString;
        exports["hasPropertyWithUInt32"] = (JSCallback)HasPropertyWithUInt32;

        exports["createObjectUsingMagic"] = (JSCallback)CreateObjectUsingMagic;
        exports["sum"] = (JSCallback)Sum;
        exports["increment"] = (JSCallback)Increment;

        exports["addFinalizer"] = (JSCallback)AddFinalizer;

        exports["instanceOf"] = (JSCallback)InstanceOf;

        exports["subscriptGetWithLatin1StyleString"] = (JSCallback)SubscriptGetWithLatin1StyleString;
        exports["subscriptGetWithUtf8StyleString"] = (JSCallback)SubscriptGetWithUtf8StyleString;
        exports["subscriptGetWithCSharpStyleString"] = (JSCallback)SubscriptGetWithCSharpStyleString;
        exports["subscriptGetAtIndex"] = (JSCallback)SubscriptGetAtIndex;
        exports["subscriptSetWithLatin1StyleString"] = (JSCallback)SubscriptSetWithLatin1StyleString;
        exports["subscriptSetWithUtf8StyleString"] = (JSCallback)SubscriptSetWithUtf8StyleString;
        exports["subscriptSetWithCSharpStyleString"] = (JSCallback)SubscriptSetWithCSharpStyleString;
        exports["subscriptSetAtIndex"] = (JSCallback)SubscriptSetAtIndex;

        return exports;
    }
}
