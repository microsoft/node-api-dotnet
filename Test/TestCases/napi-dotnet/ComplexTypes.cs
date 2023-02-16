using System;
using System.Collections.Generic;

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

    public static string[] StringArray { get; set; } = Array.Empty<string>();

    public static Memory<uint> UIntArray { get; set; }

    public static IEnumerable<int> Enumerable { get; set; } = new int[] { 0, 1, 2 };

    public static ICollection<int> Collection { get; set; } = new List<int>(new int[] { 0, 1, 2 });

    public static IReadOnlyCollection<int> ReadOnlyCollection { get; set; } = new int[] { 0, 1, 2 };

    public static IList<int> List { get; set; } = new List<int>();

    public static IReadOnlyList<int> ReadOnlyList { get; set; } = new List<int>().AsReadOnly();

    public static ISet<int> Set { get; set; } = new HashSet<int>();

    public static IReadOnlySet<int> ReadOnlySet { get; set; } = new HashSet<int>();

    public static IDictionary<int, string> Dictionary { get; set; } = new Dictionary<int, string>();

    public static IReadOnlyDictionary<int, string> ReadOnlyDictionary { get; set; }
        = new Dictionary<int, string>().AsReadOnly();

    public static IDictionary<string, IList<ClassObject>> ObjectListDictionary { get; set; }
        = new Dictionary<string, IList<ClassObject>>();

    public static Memory<uint> Slice(Memory<uint> array, int start, int length) => array.Slice(start, length);
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
