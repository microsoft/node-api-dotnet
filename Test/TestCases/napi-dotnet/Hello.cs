using System;

namespace NodeApi.TestCases;

public static class Hello
{
    [JSExport("hello")]
    public static JSValue Test(JSCallbackArgs args)
    {
        Console.WriteLine($"Hello(\"{(string)args[0]}\")");
        return $"Hello {(string)args[0]}!";
    }
}
