// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.Examples;

[JSExport]
public static class CounterFactory
{
    public static Counter CreateCounter() => new Counter();
}

[JSExport]
public class Counter
{
    public long Count { get; private set; }

    public void Increment()
    {
        Count++;

        if (Count % 100000 == 0)
        {
            System.GC.Collect();
        }
    }

    public async Task IncrementAsync()
    {
        Count++;
        await Task.Yield();

        if (Count % 100000 == 0)
        {
            System.GC.Collect();
        }
    }
}
