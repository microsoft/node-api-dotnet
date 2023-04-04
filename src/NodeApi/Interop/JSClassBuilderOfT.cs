// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;

namespace Microsoft.JavaScript.NodeApi.Interop;

public class JSClassBuilder<T> : JSPropertyDescriptorList<JSClassBuilder<T>, T> where T : class
{
    private readonly JSCallbackDescriptor? _constructorDescriptor;

    public string ClassName { get; }

    public delegate T Constructor();
    public delegate T ConstructorWithArgs(JSCallbackArgs args);

    public JSClassBuilder(string className) : base(Unwrap)
    {
        ClassName = className;
    }

    public JSClassBuilder(string className, Constructor constructorCallback)
        : this(className, new JSCallbackDescriptor(
            (args) => JSValue.CreateExternal(constructorCallback())))
    {
    }

    public JSClassBuilder(string className, ConstructorWithArgs constructorCallback)
        : this(className, new JSCallbackDescriptor(
            (args) => JSValue.CreateExternal(constructorCallback(args))))
    {
    }

    public JSClassBuilder(string className, JSCallback constructorCallback)
        : this(className, new JSCallbackDescriptor(constructorCallback))
    {
    }

    public JSClassBuilder(string className, JSCallbackDescriptor constructorDescriptor)
        : base(Unwrap)
    {
        ClassName = className;
        _constructorDescriptor = constructorDescriptor;
    }

    private static new T? Unwrap(JSCallbackArgs args)
    {
        return (T?)args.ThisArg.Unwrap();
    }

    /// <summary>
    /// Creates a class definition for the built class.
    /// </summary>
    /// <param name="baseClass">Optional base class for the defined class. If a base class is
    /// specified, the constructor callback must also invoke the base class constructor.</param>
    /// <returns>The class object (constructor).</returns>
    /// <exception cref="InvalidOperationException">A constructor was not provided.</exception>
    public JSValue DefineClass(JSValue? baseClass = null)
    {
        if (_constructorDescriptor == null)
        {
            throw new InvalidOperationException("A class constructor is required.");
        }

        JSRuntimeContext context = JSRuntimeContext.Current;
        JSValue classObject;
        if (typeof(Stream).IsAssignableFrom(typeof(T)))
        {
            JSPropertyDescriptor[] staticProperties = Properties
                .Where((p) => p.Attributes.HasFlag(JSPropertyAttributes.Static))
                .ToArray();
            if (staticProperties.Length < Properties.Count)
            {
                throw new InvalidOperationException(
                    "Stream classes may not have instance properties.");
            }

            baseClass ??= context.Import("node:stream", "Duplex");
            classObject = NodeStream.DefineStreamClass(
                ClassName,
                _constructorDescriptor.Value,
                staticProperties);
        }
        else
        {
            classObject = JSNativeApi.DefineClass(
                ClassName,
                new JSCallbackDescriptor(
                    (args) =>
                    {
                        JSValue instance;
                        if (args.Length == 1 && args[0].IsExternal())
                        {
                            // Constructing a JS instance to wrap a pre-existing C# instance.
                            instance = args[0];
                        }
                        else
                        {
                            instance = _constructorDescriptor.Value.Callback(args);
                        }

                        return JSRuntimeContext.Current.InitializeObjectWrapper(args.ThisArg, instance);
                    },
                    _constructorDescriptor.Value.Data),
                Properties.ToArray());
        }

        if (baseClass != null && !baseClass.Value.IsUndefined())
        {
            JSValue setPrototypeFunction =
                JSRuntimeContext.Current.Import(null, "Object").GetProperty("setPrototypeOf");
            setPrototypeFunction.Call(
                thisArg: JSValue.Undefined,
                classObject.GetProperty("prototype"),
                baseClass.Value.GetProperty("prototype"));
        }

        return context.RegisterClass<T>(classObject);
    }

    /// <summary>
    /// Creates a JS object that represents a static class. The object that represents the
    /// class has (static) properties, but is not a constructor function so it cannot be
    /// instantiated.
    /// </summary>
    public JSValue DefineStaticClass()
    {
        if (_constructorDescriptor != null)
        {
            throw new InvalidOperationException("A static class may not have a constructor.");
        }

        foreach (JSPropertyDescriptor property in Properties)
        {
            if (!property.Attributes.HasFlag(JSPropertyAttributes.Static))
            {
                throw new InvalidOperationException("Static class properties must be static.");
            }
        }

        JSValue obj = JSValue.CreateObject();
        obj.DefineProperties(Properties.ToArray());
        JSRuntimeContext.Current.RegisterStaticClass(ClassName, obj);
        return obj;
    }

    /// <summary>
    /// Defines a JS class that represents a .NET interface.
    /// </summary>
    /// <remarks>
    /// A JS class defined this way may not be constructed directly from JS. An instance of the
    /// class may be constructed when passing a .NET object (that implements the interface) to JS
    /// via <see cref="JSRuntimeContext.GetOrCreateObjectWrapper()" />.
    /// </remarks>
    public JSValue DefineInterface()
    {
        if (_constructorDescriptor != null)
        {
            throw new InvalidOperationException("An interface may not have a constructor.");
        }

        foreach (JSPropertyDescriptor property in Properties)
        {
            if (property.Attributes.HasFlag(JSPropertyAttributes.Static))
            {
                throw new InvalidOperationException("Interface properties must not be static.");
            }
        }

        return JSRuntimeContext.Current.RegisterClass<T>(JSNativeApi.DefineClass(
            ClassName,
            new JSCallbackDescriptor((args) =>
            {
                if (args.Length != 1 || !args[0].IsExternal())
                {
                    throw new InvalidOperationException("Cannot instantiate an interface.");
                }

                // Constructing a JS instance to wrap a C# instance that implements the interface.
                JSValue instance = args[0];
                return JSRuntimeContext.Current.InitializeObjectWrapper(args.ThisArg, instance);
            }),
            Properties.ToArray()));
    }

    /// <summary>
    /// Creates a JS Object for a TypeScript-style enumeration. The object has readonly integer
    /// properties along with a reverse number-to-string mapping.
    /// </summary>
    public JSValue DefineEnum()
    {
        if (_constructorDescriptor != null)
        {
            throw new InvalidOperationException("An enum may not have a constructor.");
        }

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
