using System;
using System.Runtime.CompilerServices;
using Microsoft.JavaScript.NodeApi;

namespace Microsoft.JavaScript.NodeApiTest;

#pragma warning disable IDE0060 // Unused parameter 'args'

public partial class TestObject : TestHelper, ITestObject
{
    private static bool s_testValue = true;

    // Used to test void* Data() integrity
    private class UserDataHolder
    {
        public int Value { get; set; }
    }

    private static JSValue TestGetter(JSCallbackArgs args)
    {
        return JSValue.GetBoolean(s_testValue);
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
        JSValue obj = JSValue.CreateObject();
        obj["cp_false"] = false;
        obj["cp_true"] = true;
        obj["s_true"] = true;
        obj["s_false"] = false;
        obj["0"] = 0;
        obj[(uint)42] = 120;
        obj["0.0f"] = 0.0f;
        obj["0.0"] = 0.0;
        obj["-1"] = -1;
        obj["foo2"] = new ReadOnlySpan<byte>(new byte[] { (byte)'f', (byte)'o', (byte)'o' });
        obj["foo4"] = "foo";
        obj["circular"] = obj;
        obj["circular2"] = obj;
        return obj;
    }

    private static JSValue Sum(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        long sum = 0;

        foreach ((JSValue _, JSValue value) in obj.Properties)
        {
            sum += (long)value;
        }

        return sum;
    }

    private static JSValue Increment(JSCallbackArgs args)
    {
        JSValue obj = args[0];

        foreach ((JSValue name, JSValue value) in obj.Properties)
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

    public JSObject Init() => new()
    {
        Method(GetPropertyNames, nameof(GetPropertyNames)),
        Method(DefineProperties, nameof(DefineProperties)),
        Method(DefineValueProperty, nameof(DefineValueProperty)),

        Method(GetPropertyWithNapiValue, nameof(GetPropertyWithNapiValue)),
        Method(GetPropertyWithNapiWrapperValue, nameof(GetPropertyWithNapiWrapperValue)),
        Method(GetPropertyWithLatin1StyleString, nameof(GetPropertyWithLatin1StyleString)),
        Method(GetPropertyWithUtf8StyleString, nameof(GetPropertyWithUtf8StyleString)),
        Method(GetPropertyWithCSharpStyleString, nameof(GetPropertyWithCSharpStyleString)),
        Method(GetPropertyWithUInt32, nameof(GetPropertyWithUInt32)),

        Method(SetPropertyWithNapiValue, nameof(SetPropertyWithNapiValue)),
        Method(SetPropertyWithNapiWrapperValue, nameof(SetPropertyWithNapiWrapperValue)),
        Method(SetPropertyWithLatin1StyleString, nameof(SetPropertyWithLatin1StyleString)),
        Method(SetPropertyWithUtf8StyleString, nameof(SetPropertyWithUtf8StyleString)),
        Method(SetPropertyWithCSharpStyleString, nameof(SetPropertyWithCSharpStyleString)),
        Method(SetPropertyWithUInt32, nameof(SetPropertyWithUInt32)),

        Method(DeletePropertyWithNapiValue, nameof(DeletePropertyWithNapiValue)),
        Method(DeletePropertyWithNapiWrapperValue, nameof(DeletePropertyWithNapiWrapperValue)),
        Method(DeletePropertyWithLatin1StyleString, nameof(DeletePropertyWithLatin1StyleString)),
        Method(DeletePropertyWithUtf8StyleString, nameof(DeletePropertyWithUtf8StyleString)),
        Method(DeletePropertyWithCSharpStyleString, nameof(DeletePropertyWithCSharpStyleString)),
        Method(DeletePropertyWithUInt32, nameof(DeletePropertyWithUInt32)),

        Method(HasOwnPropertyWithNapiValue, nameof(HasOwnPropertyWithNapiValue)),
        Method(HasOwnPropertyWithNapiWrapperValue, nameof(HasOwnPropertyWithNapiWrapperValue)),
        Method(HasOwnPropertyWithLatin1StyleString, nameof(HasOwnPropertyWithLatin1StyleString)),
        Method(HasOwnPropertyWithUtf8StyleString, nameof(HasOwnPropertyWithUtf8StyleString)),
        Method(HasOwnPropertyWithCSharpStyleString, nameof(HasOwnPropertyWithCSharpStyleString)),

        Method(HasPropertyWithNapiValue, nameof(HasPropertyWithNapiValue)),
        Method(HasPropertyWithNapiWrapperValue, nameof(HasPropertyWithNapiWrapperValue)),
        Method(HasPropertyWithLatin1StyleString, nameof(HasPropertyWithLatin1StyleString)),
        Method(HasPropertyWithUtf8StyleString, nameof(HasPropertyWithUtf8StyleString)),
        Method(HasPropertyWithCSharpStyleString, nameof(HasPropertyWithCSharpStyleString)),
        Method(HasPropertyWithUInt32, nameof(HasPropertyWithUInt32)),

        Method(CreateObjectUsingMagic, nameof(CreateObjectUsingMagic)),
        Method(Sum, nameof(Sum)),
        Method(Increment, nameof(Increment)),

        Method(AddFinalizer, nameof(AddFinalizer)),

        Method(InstanceOf, nameof(InstanceOf)),

        Method(SubscriptGetWithLatin1StyleString, nameof(SubscriptGetWithLatin1StyleString)),
        Method(SubscriptGetWithUtf8StyleString, nameof(SubscriptGetWithUtf8StyleString)),
        Method(SubscriptGetWithCSharpStyleString, nameof(SubscriptGetWithCSharpStyleString)),
        Method(SubscriptGetAtIndex, nameof(SubscriptGetAtIndex)),
        Method(SubscriptSetWithLatin1StyleString, nameof(SubscriptSetWithLatin1StyleString)),
        Method(SubscriptSetWithUtf8StyleString, nameof(SubscriptSetWithUtf8StyleString)),
        Method(SubscriptSetWithCSharpStyleString, nameof(SubscriptSetWithCSharpStyleString)),
        Method(SubscriptSetAtIndex, nameof(SubscriptSetAtIndex)),
    };
}
