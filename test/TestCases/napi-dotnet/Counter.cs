// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;

namespace Microsoft.JavaScript.NodeApi.TestCases;

/// <summary>
/// Enables testing static state.
/// </summary>
[JSExport]
public static class Counter
{
    private static int s_count;

    public static int Count()
    {
        int result = Interlocked.Increment(ref s_count);

        Console.WriteLine($"Counter.Count() => {result}");

        return result;
    }
}
