using System.Collections.Generic;
using System.Linq;

namespace NodeApi;

public class JSStructBuilder<T> where T : struct
{
    public IList<JSPropertyDescriptor> Properties { get; } = new List<JSPropertyDescriptor>();

    public JSContext Context { get; }

    public string StructName { get; }

    public JSStructBuilder(JSContext context, string structName)
    {
        Context = context;
        StructName = structName;
    }

    public JSStructBuilder<T> AddProperty(string name, bool isStatic = false)
    {
        Properties.Add(JSPropertyDescriptor.ForValue(
            name,
            JSValue.Undefined,
            JSPropertyAttributes.DefaultProperty | (isStatic ? JSPropertyAttributes.Static : 0)));
        return this;
    }

    public JSStructBuilder<T> AddMethod(
        string name,
        JSCallback callback,
        bool isStatic = false)
    {
        Properties.Add(JSPropertyDescriptor.Function(
            name,
            callback,
            JSPropertyAttributes.DefaultProperty | (isStatic ? JSPropertyAttributes.Static : 0)));
        return this;
    }

    public JSValue DefineStruct()
    {
        // TODO: Generate a constructor callback that initializes properties on the JS object
        // to converted default values? Otherwise they will be initially undefined.

        // Note this does not use Wrap() because structs are passed by value.
        return Context.RegisterStruct<T>(JSNativeApi.DefineClass(
            StructName,
            (args) => args.ThisArg,
            Properties.ToArray()));
    }
}
