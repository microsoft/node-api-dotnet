using System;
using System.Linq;

namespace NodeApi;

public class JSClassBuilder<T>
  : JSPropertyDescriptorList<JSClassBuilder<T>, T>
  , IJSObjectUnwrap<T>
  where T : class
{
    public string ClassName { get; }

    private readonly Func<T>? _constructor;
    private readonly Func<JSCallbackArgs, T>? _constructorWithArgs;

    public JSClassBuilder(string className, Func<T>? constructor = null)
    {
        ClassName = className;
        _constructor = constructor;
    }

    public JSClassBuilder(string className, Func<JSCallbackArgs, T> constructor)
    {
        ClassName = className;
        _constructorWithArgs = constructor;
    }

    static T? IJSObjectUnwrap<T>.Unwrap(JSCallbackArgs args)
    {
        return (T?)args.ThisArg.Unwrap();
    }

    public JSValue DefineClass()
    {
        if (_constructor != null)
        {
            return ObjectMap.RegisterClass<T>(JSNativeApi.DefineClass(
                ClassName,
                (args) =>
                {
                    T instance;
                    if (args.Length == 1 && args[0].IsExternal())
                    {
                        // Constructing a JS instance to wrap a pre-existing C# instance.
                        instance = (T)args[0].GetValueExternal();
                    }
                    else
                    {
                        instance = _constructor();
                    }

                    return ObjectMap.InitializeObjectWrapper(args.ThisArg, instance);
                },
                Properties.ToArray()));
        }
        else if (_constructorWithArgs != null)
        {
            return ObjectMap.RegisterClass<T>(JSNativeApi.DefineClass(
                ClassName,
                (args) =>
                {
                    T instance;
                    if (args.Length == 1 && args[0].IsExternal())
                    {
                        // Constructing a JS instance to wrap a pre-existing C# instance.
                        instance = (T)args[0].GetValueExternal();
                    }
                    else
                    {
                        instance = _constructorWithArgs(args);
                    }

                    return ObjectMap.InitializeObjectWrapper(args.ThisArg, instance);
                },
                Properties.ToArray()));
        }
        else
        {
            // Static class (no constructor).
            var obj = JSValue.CreateObject();
            obj.DefineProperties(Properties.ToArray());
            return obj;
        }
    }
}
