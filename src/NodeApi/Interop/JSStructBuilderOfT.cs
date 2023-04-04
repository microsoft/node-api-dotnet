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

        // Note this does not use Wrap() because structs are passed by value.
        return JSRuntimeContext.Current.RegisterStruct<T>(JSNativeApi.DefineClass(
            StructName,
            new JSCallbackDescriptor((args) => args.ThisArg),
            Properties.ToArray()));
    }
}
