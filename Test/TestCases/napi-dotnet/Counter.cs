using System;
using System.Threading;

namespace NodeApi.Examples;

public class Counter
{
    // TODO: Support exporting static classes without a constructor.
    public Counter(JSCallbackArgs _)
    {
        Console.WriteLine("Counter()");
    }

    private static uint s_count;

    public static JSValue Count(JSCallbackArgs _)
    {
        uint result = Interlocked.Increment(ref s_count);

        Console.WriteLine($"Counter.Count() => {result}");

        return result;
    }
}
