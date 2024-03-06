// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma warning disable IDE0230 // Use UTF-8 string literals

using System;
using System.Collections.Generic;
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
        obj.DefineProperties(new JSPropertyDescriptor(name, null, null, null, value));
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
        obj["foo2"] = new ReadOnlySpan<byte>(new[] { (byte)'f', (byte)'o', (byte)'o' });
        obj["foo4"] = "foo";
        obj["circular"] = obj;
        obj["circular2"] = obj;
        return obj;
    }

    private static JSValue Sum(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        long sum = 0;

        foreach (KeyValuePair<JSValue, JSValue> pair in obj.Properties)
        {
            sum += (long)pair.Value;
        }

        return sum;
    }

    private static JSValue Increment(JSCallbackArgs args)
    {
        JSValue obj = args[0];

        foreach (KeyValuePair<JSValue, JSValue> pair in obj.Properties)
        {
            obj[pair.Key] = (long)pair.Value + 1;
        }

        return JSValue.Undefined;
    }

    private static JSValue InstanceOf(JSCallbackArgs args)
    {
        JSValue obj = args[0];
        JSValue constructor = args[1];
        return obj.InstanceOf(constructor);
    }

    public JSObject Init() => new(
        Method(GetPropertyNames),
        Method(DefineProperties),
        Method(DefineValueProperty),

        Method(GetPropertyWithNapiValue),
        Method(GetPropertyWithNapiWrapperValue),
        Method(GetPropertyWithUtf8StyleString),
        Method(GetPropertyWithCSharpStyleString),
        Method(GetPropertyWithUInt32),

        Method(SetPropertyWithNapiValue),
        Method(SetPropertyWithNapiWrapperValue),
        Method(SetPropertyWithUtf8StyleString),
        Method(SetPropertyWithCSharpStyleString),
        Method(SetPropertyWithUInt32),

        Method(DeletePropertyWithNapiValue),
        Method(DeletePropertyWithNapiWrapperValue),
        Method(DeletePropertyWithUtf8StyleString),
        Method(DeletePropertyWithCSharpStyleString),
        Method(DeletePropertyWithUInt32),

        Method(HasOwnPropertyWithNapiValue),
        Method(HasOwnPropertyWithNapiWrapperValue),
        Method(HasOwnPropertyWithUtf8StyleString),
        Method(HasOwnPropertyWithCSharpStyleString),

        Method(HasPropertyWithNapiValue),
        Method(HasPropertyWithNapiWrapperValue),
        Method(HasPropertyWithUtf8StyleString),
        Method(HasPropertyWithCSharpStyleString),
        Method(HasPropertyWithUInt32),

        Method(CreateObjectUsingMagic),
        Method(Sum),
        Method(Increment),

        Method(AddFinalizer),

        Method(InstanceOf),

        Method(SubscriptGetWithUtf8StyleString),
        Method(SubscriptGetWithCSharpStyleString),
        Method(SubscriptGetAtIndex),
        Method(SubscriptSetWithUtf8StyleString),
        Method(SubscriptSetWithCSharpStyleString),
        Method(SubscriptSetAtIndex));
}
