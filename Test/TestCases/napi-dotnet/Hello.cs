using System;

namespace NodeApi.TestCases;

public static class Hello
{
    [JSExport("hello")]
    public static string Test(string greeter)
    {
        Console.WriteLine($"Hello(\"{greeter}\")");
        return $"Hello {greeter}!";
    }
}
