// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma warning disable IDE0060 // Unused parameters
#pragma warning disable IDE0301 // Collection initialization can be simplified

using System;
using System.Collections.Generic;
using System.Numerics;

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

    public static string[] StringArray { get; set; } = Array.Empty<string>();

    public static byte[] ByteArray { get; set; } = new[] { (byte)0, (byte)1, (byte)2 };

    public static int[] Int32Array { get; set; } = new int[] { 0, 1, 2 };

    public static Memory<byte> ByteMemory { get; set; } = new Memory<byte>(ByteArray);

    public static Memory<int> Int32Memory { get; set; } = new Memory<int>(Int32Array);

    public static IEnumerable<int> Enumerable { get; set; } = Int32Array;

    public static ICollection<int> Collection { get; set; } = new List<int>(Int32Array);

    public static IReadOnlyCollection<int> ReadOnlyCollection { get; set; } = Int32Array;

    public static IList<int> List { get; set; } = new List<int>();

    public static IReadOnlyList<int> ReadOnlyList { get; set; } = new List<int>().AsReadOnly();

    public static ISet<int> Set { get; set; } = new HashSet<int>();

#if !NETFRAMEWORK
    public static IReadOnlySet<int> ReadOnlySet { get; set; } = new HashSet<int>();
#endif

    public static IDictionary<int, string> Dictionary { get; set; } = new Dictionary<int, string>();

    public static IDictionary<string, IList<ClassObject>> ObjectListDictionary { get; set; }
        = new Dictionary<string, IList<ClassObject>>();

    public static Memory<int> Slice(Memory<int> array, int start, int length)
        => array.Slice(start, length);

    public static TestEnum TestEnum { get; set; }

    public static DateTime Date { get; set; } = new DateTime(2023, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    public static TimeSpan Time { get; set; } = new TimeSpan(1, 12, 30, 45);

    public static KeyValuePair<string, int> Pair { get; set; }
        = new KeyValuePair<string, int>("pair", 1);

    public static Tuple<string, int> Tuple { get; set; }
        = new Tuple<string, int>("tuple", 2);

    public static (string Key, int Value) ValueTuple { get; set; } = (Key: "valueTuple", Value: 3);

    public static Guid Guid { get; set; } = Guid.Parse("01234567-89AB-CDEF-FEDC-BA9876543210");

    public static BigInteger BigInt { get; set; } = BigInteger.Parse("1234567890123456789012345");
}

/// <summary>
/// Tests marshalling struct objects (passed by value).
/// </summary>
[JSExport]
public struct StructObject
{
    public string? Value { get; set; }

    public override readonly bool Equals(object? obj)
    {
        return obj is StructObject structObject && Value == structObject.Value;
    }

    public override readonly int GetHashCode()
    {
        return Value?.GetHashCode() ?? 0;
    }

    public static string? StaticValue { get; set; }

    public readonly StructObject ThisObject() => this;

    public static bool operator ==(StructObject left, StructObject right) => left.Equals(right);

    public static bool operator !=(StructObject left, StructObject right) => !(left == right);
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

    public override bool Equals(object? obj)
    {
        return obj is ClassObject classObject && Value == classObject.Value;
    }

    public override int GetHashCode()
    {
        return Value?.GetHashCode() ?? 0;
    }

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

    public static void WithGenericList(IList<StructObject> list)
    {
        // This just ensures the TS generator can handle generic parameter types.
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

// Ensure module generation handles circular references between a base class and derived class.
public class BaseClass
{
    protected BaseClass(int x) { }

    public DerivedClass? Derived { get; set; }
}

[JSExport]
public class DerivedClass : BaseClass
{
    public DerivedClass(int x) : base(x) { }
}

[JSExport]
public class ClassWithPrivateConstructor
{
    private ClassWithPrivateConstructor(string value)
    {
        Value = value;
    }

    public static ClassWithPrivateConstructor CreateInstance(string value)
    {
        return new ClassWithPrivateConstructor(value);
    }

    public string Value { get; }
}

// Ensure module generation handles implementing an interface with a custom type argument.
[JSExport]
public class CollectionOfClassObjects : IEnumerable<ClassObject>
{
    public IEnumerator<ClassObject> GetEnumerator() => throw new NotImplementedException();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => throw new NotImplementedException();
}
