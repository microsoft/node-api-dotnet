using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace NodeApi.TestCases;

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

    public static ClassObject? NullableClassObject { get; set; }

    public static int[] Array { get; set; } = System.Array.Empty<int>();

    public static int[]? NullableArray { get; set; }

    public static IList<int> List { get; set; } = new List<int>();

    public static IReadOnlyList<int> ReadOnlyList { get; set; } = new List<int>().AsReadOnly();

    public static IDictionary<int, string> Dictionary { get; set; } = new Dictionary<int, string>();

    public static IReadOnlyDictionary<int, string> ReadOnlyDictionary { get; set; }
        = new Dictionary<int, string>().AsReadOnly();
}

/// <summary>
/// Tests marshalling struct objects (passed by value).
/// </summary>
[JSExport]
public struct StructObject
{
    public string? Value { get; set; }

    public static string? StaticValue { get; set; }

    public StructObject ThisObject() => this;
}

/// <summary>
/// Tests marshalling class objects (passed by reference).
/// </summary>
[JSExport]
public class ClassObject
{
    public string? Value { get; set; }

    public static string? StaticValue { get; set; }

    public ClassObject ThisObject() => this;
}
