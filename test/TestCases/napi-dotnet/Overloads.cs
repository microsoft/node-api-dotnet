// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.TestCases;

[JSExport]
public class Overloads
{
    public Overloads()
    {
    }

    public Overloads(int intValue)
    {
        IntValue = intValue;
    }

    public Overloads(string stringValue)
    {
        StringValue = stringValue;
    }

    public Overloads(int intValue, string stringValue)
    {
        IntValue = intValue;
        StringValue = stringValue;
    }

    public Overloads(ITestInterface obj)
    {
        StringValue = obj.Value;
    }

    public int? IntValue { get; private set; }

    public string? StringValue { get; private set; }

    public void SetValue(int intValue)
    {
        IntValue = intValue;
    }

    public void SetValue(string stringValue)
    {
        StringValue = stringValue;
    }

    public void SetValue(int intValue, string stringValue)
    {
        IntValue = intValue;
        StringValue = stringValue;
    }

    public void SetValue(ITestInterface obj)
    {
        StringValue = obj.Value;
    }

    // Method with overloaded name in C# is given a non-overloaded export name.
    [JSExport("setDoubleValue")]
    public void SetValue(double doubleValue)
    {
        IntValue = (int)doubleValue;
    }

    public static string NumericMethod(int value) => $"{value}: int";
    public static string NumericMethod(long value) => $"{value}: long";
    public static string NumericMethod(float value) => $"{value}: float";
    public static string NumericMethod(double value) => $"{value}: double";
    public static string NumericMethod2(string value1, int value2)
        => $"{value1}: string, {value2}: int";
    public static string NumericMethod2(string value1, double value2)
        => $"{value1}: string, {value2}: double";

    public static string NumericMethod3(byte value) => $"{value}: byte";
    public static string NumericMethod3(sbyte value) => $"{value}: sbyte";
    public static string NumericMethod3(ushort value) => $"{value}: ushort";
    public static string NumericMethod3(short value) => $"{value}: short";
    public static string NumericMethod3(uint value) => $"{value}: uint";
    public static string NumericMethod3(int value) => $"{value}: int";
    public static string NumericMethod3(ulong value) => $"{value}: ulong";
    public static string NumericMethod3(long value) => $"{value}: long";

    public static string ClassMethod(ClassObject value) => $"{value.Value}: ClassObject";
    public static string ClassMethod(BaseClass value) => $"{value.Value1}: BaseClass";
    public static string ClassMethod(SubClass value) => $"{value.Value2}: SubClass";
    public static string ClassMethod(StructObject value) => $"{value.Value}: StructObject";

    public static string InterfaceMethod(IBaseInterface value)
        => $"{value.Value1}: IBaseInterface";
    public static string InterfaceMethod(ISubInterface value)
        => $"{value.Value2}: ISubInterface";

    public static string CollectionMethod1(int[] value)
        => $"[{string.Join(", ", value)}]: int[]";
    public static string CollectionMethod1(ICollection<int> value)
        => $"[{string.Join(", ", value)}]: ICollection<int>";
    public static string CollectionMethod1(IList<int> value)
        => $"[{string.Join(", ", value)}]: IList<int>";
    public static string CollectionMethod1(ISet<int> value)
        => $"[{string.Join(", ", value)}]: ISet<int>";
    public static string CollectionMethod1(IDictionary<int, int> value)
        => $"[{string.Join(", ", value)}]: IDictionary<int, int>";

    public static string CollectionMethod2(IEnumerable<int> value)
        => $"[{string.Join(", ", value)}]: IEnumerable<int>";
    public static string CollectionMethod2(IReadOnlyCollection<int> value)
        => $"[{string.Join(", ", value)}]: IReadOnlyCollection<int>";
    public static string CollectionMethod2(IReadOnlyList<int> value)
        => $"[{string.Join(", ", value)}]: IReadOnlyList<int>";
    public static string CollectionMethod2(IReadOnlyDictionary<int, int> value)
        => $"[{string.Join(", ", value)}]: IReadOnlyDictionary<int, int>";

    public static string CollectionMethod3(IEnumerable<int> value)
        => $"[{string.Join(", ", value)}]: IEnumerable<int>";
    public static string CollectionMethod3(ICollection<int> value)
        => $"[{string.Join(", ", value)}]: ICollection<int>";

    public static Task<string> CollectionMethod4(IEnumerable<int> value)
        => Task.FromResult($"[{string.Join(", ", value)}]: IEnumerable<int>");
    public static async Task<string> CollectionMethod4(IAsyncEnumerable<int> value)
    {
        List<int> list = new();
        await foreach (var item in value)
        {
            list.Add(item);
        }
        return $"[{string.Join(", ", list)}]: IAsyncEnumerable<int>";
    }

    public static string DateTimeMethod(DateTime value) => $"{value:s}: DateTime";
    public static string DateTimeMethod(DateTimeOffset value) => $"{value:s}: DateTimeOffset";
    public static string DateTimeMethod(TimeSpan value) => $"{value}: TimeSpan";

    public static string OtherMethod(TestEnum value) => $"{value}: TestEnum";
    public static string OtherMethod(Guid value) => $"{value}: Guid";
    public static string OtherMethod(BigInteger value) => $"{value}: BigInteger";
    public static string OtherMethod(Task value) => $"Task";
    public static string OtherMethod(TestDelegate value) => $"{value("test")}: TestDelegate";

    public static string NullableNumericMethod(int? value) =>
        $"{(value == null ? "null" : value.ToString())}: int?";
    public static string NullableNumericMethod(double value) => $"{value}: double";
}
