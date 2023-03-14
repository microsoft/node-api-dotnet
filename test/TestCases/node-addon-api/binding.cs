// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.JavaScript.NodeApi;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApiTest;

[JSModule]
public class Binding
{
    private readonly Dictionary<Type, JSReference> _testObjects = new();

    public JSValue BasicTypesArray => GetOrCreate<TestBasicTypesArray>();
    public JSValue BasicTypesBoolean => GetOrCreate<TestBasicTypesBoolean>();
    public JSValue BasicTypesNumber => GetOrCreate<TestBasicTypesNumber>();
    public JSValue BasicTypesValue => GetOrCreate<TestBasicTypesValue>();
    public JSValue Object => GetOrCreate<TestObject>();
    public JSValue ObjectFreezeSeal => GetOrCreate<TestObjectFreezeSeal>();

    private JSValue GetOrCreate<T>() where T : class, ITestObject, new()
    {
        if (_testObjects.TryGetValue(typeof(T), out JSReference? testRef))
        {
            return testRef.GetValue() ?? JSValue.Undefined;
        }

        JSValue obj = new T().Init();
        _testObjects.Add(typeof(T), new JSReference(obj));
        return obj;
    }
}

public interface ITestObject
{
    abstract JSObject Init();
}

public abstract class TestHelper
{
    public static KeyValuePair<JSValue, JSValue> Method(JSCallback callback, string callbackName)
    {
        string name = callbackName ?? string.Empty;
        name = name.Substring(name.IndexOf('.') + 1);
        return new KeyValuePair<JSValue, JSValue>(ToCamelCase(name), callback);
    }

    public static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        char[] chars = value.ToCharArray();
        chars[0] = char.ToLower(chars[0]);
        return new string(chars);
    }
}
