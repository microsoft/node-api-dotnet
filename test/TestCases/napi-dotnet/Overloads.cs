// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

    // Method with overloaded name in C# is given a non-overloaded export name.
    [JSExport("setDoubleValue")]
    public void SetValue(double doubleValue)
    {
        IntValue = (int)doubleValue;
    }
}
