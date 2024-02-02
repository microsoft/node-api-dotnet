// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi.TestCases;

/// <summary>
/// Tests marshalling of various non-primitive types.
/// </summary>
[JSExport]
public static class ComplexTypes
{
    public static int? NullableInt { get; set; }

    public static string? NullableString { get; set; }

    public static StructObject StructObject { get; set; } = new() { Value = "test" };

    public static StructObject? NullableStructObject { get; set; }

    public static ClassObject ClassObject { get; set; } = new() { Value = "test" };

    public static ITestInterface InterfaceObject { get; set; } = ClassObject;

    public static ClassObject? NullableClassObject { get; set; }

    public static string[] StringArray { get; set; } = [];

    public static Memory<uint> UIntArray { get; set; }

    public static IEnumerable<int> Enumerable { get; set; } = new int[] { 0, 1, 2 };

    public static ICollection<int> Collection { get; set; } = new List<int>(new int[] { 0, 1, 2 });

    public static IReadOnlyCollection<int> ReadOnlyCollection { get; set; } = new int[] { 0, 1, 2 };

    public static IList<int> List { get; set; } = new List<int>();

    public static IReadOnlyList<int> ReadOnlyList { get; set; } = new List<int>().AsReadOnly();

    public static ISet<int> Set { get; set; } = new HashSet<int>();

#if !NETFRAMEWORK
    public static IReadOnlySet<int> ReadOnlySet { get; set; } = new HashSet<int>();
#endif

    public static IDictionary<int, string> Dictionary { get; set; } = new Dictionary<int, string>();

    public static IDictionary<string, IList<ClassObject>> ObjectListDictionary { get; set; }
        = new Dictionary<string, IList<ClassObject>>();

    public static Memory<uint> Slice(Memory<uint> array, int start, int length)
        => array.Slice(start, length);

    public static TestEnum TestEnum { get; set; }

    public static DateTime Date { get; set; } = new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    public static KeyValuePair<string, int> Pair { get; set; }
        = new KeyValuePair<string, int>("pair", 1);

    public static Tuple<string, int> Tuple { get; set; }
        = new Tuple<string, int>("tuple", 2);

    public static (string Key, int Value) ValueTuple { get; set; } = (Key: "valueTuple", Value: 3);
}

/// <summary>
/// Tests marshalling struct objects (passed by value).
/// </summary>
[JSExport]
public struct StructObject
{
    public string? Value { get; set; }

    public static string? StaticValue { get; set; }

    public readonly StructObject ThisObject() => this;
}

/// <summary>
/// Tests marshalling class objects via interfaces.
/// </summary>
[JSExport]
public interface ITestInterface
{
    string? Value { get; set; }

    string AppendValue(string append);

    void AppendAndGetPreviousValue(ref string value, out string? previousValue);

#if !AOT
    string AppendGenericValue<T>(T value);
#endif
}

/// <summary>
/// Tests marshalling class objects (passed by reference).
/// </summary>
[JSExport]
public class ClassObject : ITestInterface
{
    public string? Value { get; set; }

    public string AppendValue(string append)
    {
        Value = (Value ?? "") + append;
        return Value!;
    }

    public void AppendAndGetPreviousValue(ref string value, out string? previousValue)
    {
        previousValue = Value;
        Value = (Value ?? "") + value;
        value = Value;
    }

    public bool TryGetValue(out string? value)
    {
        value = Value;
        return value != null;
    }

    public static string? StaticValue { get; set; }

    public ClassObject ThisObject() => this;

    public static ITestInterface Create(string value)
    {
        return new ClassObject { Value = value };
    }

#if !AOT
    public string AppendGenericValue<T>(T value)
    {
        Value = (Value ?? "") + value?.ToString();
        return Value!;
    }

    public static void CallGenericMethod(ITestInterface obj, int value)
    {
        obj.AppendGenericValue<int>(value);
    }
#endif

    public class NestedClass
    {
        public NestedClass(string value)
        {
            Value = value;
        }

        public string Value { get; set; }
    }
}

[JSExport]
public enum TestEnum
{
    Zero,
    One,
    Two,
}
