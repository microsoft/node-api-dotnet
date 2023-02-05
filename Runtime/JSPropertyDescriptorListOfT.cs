using System;
using System.Collections.Generic;

namespace NodeApi;

// TODO: Add interceptors for C# exceptions

public abstract class JSPropertyDescriptorList<TDerived, TObject>
  where TDerived : class, IJSObjectUnwrap<TObject>
  where TObject : class
{
    public IList<JSPropertyDescriptor> Properties { get; } = new List<JSPropertyDescriptor>();

    protected JSPropertyDescriptorList() { }

    public TDerived AddProperty(
      string name,
      JSValue value,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
    {
        Properties.Add(JSPropertyDescriptor.ForValue(name, value, attributes));
        return (TDerived)(object)this;
    }

    public TDerived AddProperty(
      string name,
      JSCallback? getter,
      JSCallback? setter,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty)
    {
        Properties.Add(JSPropertyDescriptor.Accessor(name, getter, setter, attributes));
        return (TDerived)(object)this;
    }

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

    public TDerived AddProperty(
      string name,
      Func<JSValue>? getter,
      Action<JSValue>? setter,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultProperty | JSPropertyAttributes.Static)
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

    public TDerived AddMethod(
      string name,
      JSCallback callback,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
    {
        Properties.Add(JSPropertyDescriptor.Function(name, callback, attributes));
        return (TDerived)(object)this;
    }

    public TDerived AddMethod(
      string name,
      Func<TObject, Action> getMethod,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
    {
        return AddMethod(
          name,
          args =>
          {
              if (TDerived.Unwrap(args) is TObject obj)
              {
                  getMethod(obj).Invoke();
              }
              return JSValue.Undefined;
          },
          attributes);
    }

    public TDerived AddMethod(
      string name,
      Func<TObject, Action<JSCallbackArgs>> getMethod,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
    {
        return AddMethod(
          name,
          args =>
          {
              if (TDerived.Unwrap(args) is TObject obj)
              {
                  getMethod(obj).Invoke(args);
              }
              return JSValue.Undefined;
          },
          attributes);
    }

    public TDerived AddMethod(
      string name,
      Func<TObject, Action<JSValue>> getMethod,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
    {
        return AddMethod(
          name,
          args =>
          {
              if (TDerived.Unwrap(args) is TObject obj)
              {
                  getMethod(obj).Invoke(args[0]);
              }
              return JSValue.Undefined;
          },
          attributes);
    }

    public TDerived AddMethod(
      string name,
      Func<TObject, Func<JSValue>> getMethod,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
    {
        return AddMethod(
          name,
          args => (TDerived.Unwrap(args) is TObject obj) ? getMethod(obj).Invoke() : JSValue.Undefined,
          attributes);
    }

    public TDerived AddMethod(
      string name,
      Func<TObject, Func<JSCallbackArgs, JSValue>> getMethod,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod)
    {
        return AddMethod(
          name,
          args => (TDerived.Unwrap(args) is TObject obj) ? getMethod(obj).Invoke(args) : JSValue.Undefined,
          attributes);
    }

    public TDerived AddMethod(
      string name,
      Func<Action> getMethod,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod | JSPropertyAttributes.Static)
    {
        return AddMethod(
          name,
          args =>
          {
              getMethod().Invoke();
              return JSValue.Undefined;
          },
          attributes);
    }

    public TDerived AddMethod(
      string name,
      Func<Action<JSCallbackArgs>> getMethod,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod | JSPropertyAttributes.Static)
    {
        return AddMethod(
          name,
          args =>
          {
              getMethod().Invoke(args);
              return JSValue.Undefined;
          },
          attributes);
    }

    public TDerived AddMethod(
      string name,
      Func<Func<JSValue>> getMethod,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod | JSPropertyAttributes.Static)
    {
        return AddMethod(
          name,
          args => getMethod().Invoke(),
          attributes);
    }

    public TDerived AddMethod(
      string name,
      Func<Func<JSCallbackArgs, JSValue>> getMethod,
      JSPropertyAttributes attributes = JSPropertyAttributes.DefaultMethod | JSPropertyAttributes.Static)
    {
        return AddMethod(
          name,
          args => getMethod().Invoke(args),
          attributes);
    }
}
