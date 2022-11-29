using System.Linq;

namespace NodeApi;

public class JSClassBuilder<T>
  : JSPropertyDescriptorList<JSClassBuilder<T>, T>
  , IJSObjectUnwrap<T>
  where T : class
{
  public delegate T ConstructorDelegate(JSCallbackArgs args);

  public string ClassName { get; }

  public ConstructorDelegate Constructor { get; }

  public JSClassBuilder(string className, ConstructorDelegate constructor)
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
    return JSNativeApi.DefineClass(ClassName, args => args.ThisArg.Wrap(Constructor(args)), Properties.ToArray());
  }
}
