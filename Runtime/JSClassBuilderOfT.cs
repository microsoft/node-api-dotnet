using System;
using System.Linq;

namespace NodeApi;

public class JSClassBuilder<T>
  : JSPropertyDescriptorList<JSClassBuilder<T>, T>
  , IJSObjectUnwrap<T>
  where T : class
{
    public JSContext Context { get; }

    public string ClassName { get; }

    public delegate T Constructor();
    public delegate T ConstructorWithArgs(JSCallbackArgs args);

    private readonly Constructor? _constructor;
    private readonly ConstructorWithArgs? _constructorWithArgs;

    public JSClassBuilder(JSContext context, string className, Constructor? constructor = null)
    {
        Context = context;
        ClassName = className;
        _constructor = constructor;
    }

    public JSClassBuilder(JSContext context, string className, ConstructorWithArgs constructor)
    {
        Context = context;
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
            return Context.RegisterClass<T>(JSNativeApi.DefineClass(
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

                    return Context.InitializeObjectWrapper(args.ThisArg, instance);
                },
                Properties.ToArray()));
        }
        else if (_constructorWithArgs != null)
        {
            return Context.RegisterClass<T>(JSNativeApi.DefineClass(
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

                    return Context.InitializeObjectWrapper(args.ThisArg, instance);
                },
                Properties.ToArray()));
        }
        else
        {
            throw new InvalidOperationException("A constructor is required.");
        }
    }

    public JSValue DefineStaticClass()
    {
        foreach (JSPropertyDescriptor property in Properties)
        {
            if (!property.Attributes.HasFlag(JSPropertyAttributes.Static))
            {
                throw new InvalidOperationException("Static class properties must be static.");
            }
        }

        JSValue obj = JSValue.CreateObject();
        obj.DefineProperties(Properties.ToArray());
        Context.RegisterStaticClass(ClassName, obj);
        return obj;
    }

    public JSValue DefineEnum()
    {
        foreach (JSPropertyDescriptor property in Properties)
        {
            if (!property.Attributes.HasFlag(JSPropertyAttributes.Static))
            {
                throw new InvalidOperationException("Enum properties must be static.");
            }
            if (property.Value?.IsNumber() != true)
            {
                throw new InvalidOperationException("Enum property values must be numbers.");
            }
        }

        JSValue obj = JSValue.CreateObject();
        obj.DefineProperties(Properties.ToArray());

        // Create the reverse mapping from numeric value to string value.
        foreach (JSPropertyDescriptor property in Properties)
        {
            obj[property.Value!.Value] = property.Name;
        }

        return obj;
    }
}
