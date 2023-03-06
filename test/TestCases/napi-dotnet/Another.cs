using System;

namespace Microsoft.JavaScript.NodeApi.TestCases;

#pragma warning disable CA1822 // Mark members as static

[JSExport]
public class Another
{
    public Another()
    {
        Console.WriteLine("Another({init})");
    }

    public static string StaticValue
    {
        get
        {
            Console.WriteLine("Another.StaticValue.get()");
            return "static";
        }
    }

    public string InstanceValue
    {
        get
        {
            Console.WriteLine("Another.InstanceValue.get()");
            return "instance";
        }
    }

    public static bool StaticMethod(bool arg1, int arg2)
    {
        Console.WriteLine($"Another.StaticMethod({arg1}, {arg2})");
        return true;
    }

    public bool InstanceMethod(string arg)
    {
        Console.WriteLine($"Another.InstanceMethod({arg})");
        return false;
    }
}
