using System;
using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi.Interop;

public abstract class JSPropertyDescriptorList<TDerived, TObject>
  where TDerived : class, IJSObjectUnwrap<TObject>
  where TObject : class
{
    public IList<JSPropertyDescriptor> Properties { get; } = new List<JSPropertyDescriptor>();

    protected JSPropertyDescriptorList() { }

    /// <summary>
    /// Adds a property with an initial value of undefined.
    /// </summary>
    public TDerived AddProperty(
        string name,
        JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
    {
        Properties.Add(JSPropertyDescriptor.ForValue(name, JSValue.Undefined, attributes));
        return (TDerived)(object)this;
    }

    /// <summary>
    /// Adds a property with a specified initial value.
    /// </summary>
    public TDerived AddProperty(
      string name,
      JSValue value,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
    {
        Properties.Add(JSPropertyDescriptor.ForValue(name, value, attributes));
        return (TDerived)(object)this;
    }

    /// <summary>
    /// Adds a property with getter and/or setter callbacks.
    /// </summary>
    public TDerived AddProperty(
      string name,
      JSCallback? getter,
      JSCallback? setter,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty,
      object? data = null)
    {
        Properties.Add(JSPropertyDescriptor.Accessor(name, getter, setter, attributes, data));
        return (TDerived)(object)this;
    }

    /// <summary>
    /// Adds a property with getter and/or setter callbacks.
    /// </summary>
    public TDerived AddProperty(
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
    /// Adds a property with getter and/or setter callbacks obtained from the wrapped object.
    /// </summary>
    public TDerived AddProperty(
      string name,
      Func<TObject, JSValue>? getter,
      Action<TObject, JSValue>? setter,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
    {
        return AddProperty(
          name,
          getter == null ? null : args =>
          {
              return (TDerived.Unwrap(args) is TObject obj) ? getter(obj) : JSValue.Undefined;
          },
          setter == null ? null : args =>
          {
              if (TDerived.Unwrap(args) is TObject obj)
              {
                  setter(obj, args[0]);
              }
              return JSValue.Undefined;
          },
          attributes);
    }

    /// <summary>
    /// Adds a method with a void no-args callback.
    /// </summary>
    public TDerived AddMethod(
      string name,
      Action callback,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
    {
        return AddMethod(
          name,
          args =>
          {
              callback.Invoke();
              return JSValue.Undefined;
          },
          attributes);
    }

    /// <summary>
    /// Adds a method with a void callback.
    /// </summary>
    public TDerived AddMethod(
      string name,
      JSActionCallback callback,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod,
      object? data = null)
    {
        return AddMethod(
          name,
          args =>
          {
              callback.Invoke(args);
              return JSValue.Undefined;
          },
          attributes,
          data);
    }

    /// <summary>
    /// Adds a method with a callback.
    /// </summary>
    public TDerived AddMethod(
      string name,
      JSCallback callback,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod,
      object? data = null)
    {
        Properties.Add(JSPropertyDescriptor.Function(name, callback, attributes, data));
        return (TDerived)(object)this;
    }

    /// <summary>
    /// Adds a method with a void no-args callback obtained from the wrapped object.
    /// </summary>
    public TDerived AddMethod(
      string name,
      Func<TObject, Action> getCallback,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
    {
        return AddMethod(
          name,
          args =>
          {
              if (TDerived.Unwrap(args) is TObject obj)
              {
                  getCallback(obj).Invoke();
              }
              return JSValue.Undefined;
          },
          attributes);
    }

    /// <summary>
    /// Adds a method with a void callback obtained from the wrapped object.
    /// </summary>
    public TDerived AddMethod(
      string name,
      Func<TObject, JSActionCallback> getCallback,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod,
      object? data = null)
    {
        return AddMethod(
          name,
          args =>
          {
              if (TDerived.Unwrap(args) is TObject obj)
              {
                  getCallback(obj).Invoke(args);
              }
              return JSValue.Undefined;
          },
          attributes,
          data);
    }

    /// <summary>
    /// Adds a method with a callback obtained from the wrapped object.
    /// </summary>
    public TDerived AddMethod(
      string name,
      Func<TObject, JSCallback> getCallback,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod,
      object? data = null)
    {
        return AddMethod(
          name,
          args => (TDerived.Unwrap(args) is TObject obj) ?
            getCallback(obj).Invoke(args) : JSValue.Undefined,
          attributes,
          data);
    }
}
