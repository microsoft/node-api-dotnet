using System.Linq;

namespace NodeApi;

public class JSClassBuilder<T>
  : JSPropertyDescriptorList<JSClassBuilder<T>, T>
  , IJSObjectUnwrap<T>
  where T : class
{
    public delegate T ConstructorDelegate(JSCallbackArgs args);

    public string ClassName { get; }

    public ConstructorDelegate? Constructor { get; }

    public JSClassBuilder(string className, ConstructorDelegate? constructor = null)
    {
        ClassName = className;
        Constructor = constructor;
    }

    static T? IJSObjectUnwrap<T>.Unwrap(JSCallbackArgs args)
    {
        return (T?)args.ThisArg.Unwrap();
    }

    public JSValue DefineClass()
    {
        if (Constructor != null)
        {
            return JSNativeApi.DefineClass(
                ClassName,
                (args) => args.ThisArg.Wrap(Constructor(args)),
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
