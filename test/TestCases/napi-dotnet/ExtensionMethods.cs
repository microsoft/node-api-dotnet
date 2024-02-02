// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi.TestCases;

public class SubclassOfGenericClass : GenericClass<string>
{
    public SubclassOfGenericClass(string value) : base(value) {}

    public static IGenericInterface<string> Create(string value)
        => new SubclassOfGenericClass(value);
}

public static class TestClassExtensions
{
    public static string GetValueOrDefault(this ClassObject obj, string defaultValue)
        => obj.Value ?? defaultValue;

    public static T GetGenericValueOrDefault<T>(this GenericClass<T> obj, T defaultValue)
        => obj.Value ?? defaultValue;

    public static string GetGenericStringValueOrDefault(this GenericClass<string> obj, string defaultValue)
        => obj.Value ?? defaultValue;
}

public static class TestInterfaceExtensions
{
    public static int? ToInteger(this ITestInterface obj)
        => Int32.TryParse(obj.Value, out int value) ? (int?)value : null;

    public static int? GenericToInteger<T>(this IGenericInterface<T> obj)
        => Int32.TryParse(obj.Value?.ToString(), out int value) ? (int?)value : null;

    public static int? GenericStringToInteger(this IGenericInterface<string> obj)
        => Int32.TryParse(obj.Value, out int value) ? (int?)value : null;
}
