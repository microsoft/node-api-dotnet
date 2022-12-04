using System;

namespace NodeApi.Examples;

public class Another
{
    public Another(JSCallbackArgs _)
    {
        Console.WriteLine("Another()");
    }

    public static JSValue StaticValue
    {
        get
        {
            Console.WriteLine("Another.StaticValue.get()");
            return "static";
        }
    }

    public JSValue InstanceValue
    {
        get
        {
            Console.WriteLine("Another.InstanceValue.get()");
            return "instance";
        }
    }

    public static JSValue StaticMethod(JSCallbackArgs _)
    {
        Console.WriteLine("Another.StaticMethod()");
        return true;
    }

    public JSValue InstanceMethod(JSCallbackArgs _)
    {
        Console.WriteLine("Another.InstanceMethod()");
        return false;
    }
}
