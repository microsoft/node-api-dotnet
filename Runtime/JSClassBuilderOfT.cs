using System;
using System.Linq;

namespace NodeApi;

public class JSClassBuilder<T>
  : JSPropertyDescriptorList<JSClassBuilder<T>, T>
  , IJSObjectUnwrap<T>
  where T : class
{
    public string ClassName { get; }

    private Func<T>? _constructor;
    private Func<JSCallbackArgs, T>? _constructorWithArgs;

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
            return JSNativeApi.DefineClass(
                ClassName,
                (args) => args.ThisArg.Wrap(_constructor()),
                Properties.ToArray());
        }
        else if (_constructorWithArgs != null)
        {
            return JSNativeApi.DefineClass(
                ClassName,
                (args) => args.ThisArg.Wrap(_constructorWithArgs(args)),
                Properties.ToArray());
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
