// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Examples;

/// <summary>
/// Example class that will be dynamically instantiated in JavaScript.
/// </summary>
public class Class1
{
    /// <summary>
    /// Creates a new instance of the <see cref="Class1"/> class.
    /// </summary>
    public Class1()
    {
    }

    /// <summary>
    /// Gets a greeting message.
    /// </summary>
    public string Hello(string greeter)
    {
        System.Console.WriteLine($"Hello {greeter}!");
        return $"Hello {greeter}!";
    }
}
