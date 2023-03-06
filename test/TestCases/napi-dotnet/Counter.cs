using System;
using System.Threading;

namespace NodeApi.TestCases;

/// <summary>
/// Enables testing static state.
/// </summary>
[JSExport]
public static class Counter
{
    private static uint s_count;

    public static uint Count()
    {
        uint result = Interlocked.Increment(ref s_count);

        Console.WriteLine($"Counter.Count() => {result}");

        return result;
    }
}
