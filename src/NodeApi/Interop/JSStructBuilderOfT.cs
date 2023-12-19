// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.JavaScript.NodeApi.Interop;

public class JSStructBuilder<T> where T : struct
{
    public IList<JSPropertyDescriptor> Properties { get; } = new List<JSPropertyDescriptor>();

    public string StructName { get; }

    public JSStructBuilder(string structName)
    {
        StructName = structName;
    }

    /// <summary>
    /// Adds a property with an initial value of undefined.
    /// </summary>
    public JSStructBuilder<T> AddProperty(
        string name,
        JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
    {
        Properties.Add(JSPropertyDescriptor.ForValue(name, JSValue.Undefined, attributes));
        return this;
    }

    /// <summary>
    /// Adds a property with a specified initial value.
    /// </summary>
    public JSStructBuilder<T> AddProperty(
        string name,
        JSValue value,
        JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
    {
        Properties.Add(JSPropertyDescriptor.ForValue(name, value, attributes));
        return this;
    }

    /// <summary>
    /// Adds a property with getter and/or setter callbacks.
    /// </summary>
    public JSStructBuilder<T> AddProperty(
        string name,
        JSCallback? getter,
        JSCallback? setter,
        JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty,
        object? data = null)
    {
        Properties.Add(JSPropertyDescriptor.Accessor(name, getter, setter, attributes, data));
        return this;
    }

    /// <summary>
    /// Adds a property with getter and/or setter callbacks.
    /// </summary>
    public JSStructBuilder<T> AddProperty(
        string name,
        Func<JSValue>? getter,
        Action<JSValue>? setter,
        JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
    {
        return AddProperty(
            name,
            getter == null ? null : args => getter(),
            setter == null ? null : args =>
            {
                setter(args[0]);
                return JSValue.Undefined;
            },
            attributes);
    }

    /// <summary>
    /// Adds a method with a callback.
    /// </summary>
    public JSStructBuilder<T> AddMethod(
        string name,
        JSCallback callback,
        JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod,
        object? data = null)
    {
        Properties.Add(JSPropertyDescriptor.Function(
            name,
            callback,
            attributes,
            data));
        return this;
    }

    public JSValue DefineStruct()
    {
        // TODO: Generate a constructor callback that initializes properties on the JS object
        // to converted default values? Otherwise they will be initially undefined.

        AddTypeToString();

        // Note this does not use Wrap() because structs are passed by value.
        JSValue classObject = JSValue.DefineClass(
            StructName,
            new JSCallbackDescriptor(StructName, (args) => args.ThisArg),
            Properties.ToArray());

        // The class object wraps the Type, so it can be easily converted when passed
        // to APIs that require a Type.
        classObject.Wrap(typeof(T));

        return JSRuntimeContext.Current.RegisterStruct<T>(classObject);
    }


    /// <summary>
    /// Adds a JS `toString()` method on the object that represents the type in JavaScript.
    /// The method returns the full name of the .NET type.
    /// </summary>
    private void AddTypeToString()
    {
        // Return early if there is already a static `toString()` method defined.
        foreach (JSPropertyDescriptor property in Properties)
        {
            if (property.Attributes.HasFlag(JSPropertyAttributes.Static) &&
                property.Name == "toString")
            {
                return;
            }
        }

        AddMethod(
            "toString",
            (_) => typeof(T).FormatName(),
            JSPropertyAttributes.Static | JSPropertyAttributes.DefaultMethod);
    }
}
