// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.TestCases;

#pragma warning disable CA1822 // Mark members as static

// Note these types do not have [JSExport] attributes: static binding does not support generics.

public interface IGenericInterface<T>
{
    T Value { get; set; }
}

public class GenericClass<T> : IGenericInterface<T>
{
    public GenericClass(T value) { Value = value; }
    public T Value { get; set; }
    public T GetValue(T value) => value;
}

public class GenericClassWithConstraint<T> where T : struct
{
    public GenericClassWithConstraint(T value) { Value = value; }
    public T Value { get; set; }
    public T GetValue(T value) => value;
}

public struct GenericStruct<T>
{
    public GenericStruct(T value) { Value = value; }
    public T Value { get; set; }
    public readonly T GetValue(T value) => value;
}

public static class StaticClassWithGenericMethods
{
    public static T GetValue<T>(T value) => value;
}

public class NonstaticClassWithGenericMethods
{
    public T GetValue<T>(T value) => value;
}
