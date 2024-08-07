// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Represents a JavaScript Function value.
/// </summary>
public readonly struct JSFunction : IJSValue<JSFunction>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSFunction" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSFunction" /> to convert.</param>
    public static implicit operator JSValue(JSFunction function) => function._value;

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSFunction" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSFunction" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSFunction?(JSValue value) => value.As<JSFunction>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSFunction" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSFunction" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSFunction(JSValue value) => value.CastTo<JSFunction>();

    private JSFunction(JSValue value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a new unnamed JS function with a callback and optional callback data.
    /// </summary>
    public JSFunction(JSCallback callback, object? callbackData = null)
        : this(JSValue.CreateFunction(name: null, callback, callbackData))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes no arguments and returns void.
    /// </summary>
    public JSFunction(Action callback)
        : this(JSValue.CreateFunction(name: null, (args) => { callback(); return default; }))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes one argument and returns void.
    /// </summary>
    public JSFunction(Action<JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) => { callback(args[0]); return default; }))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes two arguments and returns void.
    /// </summary>
    public JSFunction(Action<JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) =>
        {
            callback(args[0], args[1]);
            return default;
        }))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes three arguments and returns void.
    /// </summary>
    public JSFunction(Action<JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) =>
        {
            callback(args[0], args[1], args[2]);
            return default;
        }))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes four arguments and returns void.
    /// </summary>
    public JSFunction(Action<JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) =>
        {
            callback(args[0], args[1], args[2], args[3]);
            return default;
        }))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes five arguments and returns void.
    /// </summary>
    /// <param name="callback"></param>
    public JSFunction(Action<JSValue, JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) =>
        {
            callback(args[0], args[1], args[2], args[3], args[4]);
            return default;
        }))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes no arguments and returns a value.
    /// </summary>
    /// <param name="callback"></param>
    public JSFunction(Func<JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) => callback()))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes one argument and returns a value.
    /// </summary>
    public JSFunction(Func<JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) => callback(args[0])))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes two arguments and returns a value.
    /// </summary>
    public JSFunction(Func<JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) => callback(args[0], args[1])))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes three arguments and returns a value.
    /// </summary>
    public JSFunction(Func<JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) => callback(args[0], args[1], args[2])))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes four arguments and returns a value.
    /// </summary>
    public JSFunction(Func<JSValue, JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) =>
            callback(args[0], args[1], args[2], args[3])))
    {
    }

    /// <summary>
    /// Creates a new unnamed JS function takes five arguments and returns a value.
    /// </summary>
    public JSFunction(Func<JSValue, JSValue, JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name: null, (args) =>
            callback(args[0], args[1], args[2], args[3], args[4])))
    {
    }

    /// <summary>
    /// Creates a new named JS function with a callback and optional callback data.
    /// </summary>
    public JSFunction(string name, JSCallback callback, object? callbackData = null)
        : this(JSValue.CreateFunction(name, callback, callbackData))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes no arguments and returns void.
    /// </summary>
    public JSFunction(string name, Action callback)
        : this(JSValue.CreateFunction(name, (args) => { callback(); return default; }))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes one argument and returns void.
    /// </summary>
    public JSFunction(string name, Action<JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) => { callback(args[0]); return default; }))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes two arguments and returns void.
    /// </summary>
    public JSFunction(string name, Action<JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) =>
        {
            callback(args[0], args[1]);
            return default;
        }))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes three arguments and returns void.
    /// </summary>
    public JSFunction(string name, Action<JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) =>
        {
            callback(args[0], args[1], args[2]);
            return default;
        }))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes four arguments and returns void.
    /// </summary>
    public JSFunction(string name, Action<JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) =>
        {
            callback(args[0], args[1], args[2], args[3]);
            return default;
        }))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes five arguments and returns void.
    /// </summary>
    public JSFunction(string name, Action<JSValue, JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) =>
        {
            callback(args[0], args[1], args[2], args[3], args[4]);
            return default;
        }))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes no arguments and returns a value.
    /// </summary>
    public JSFunction(string name, Func<JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) => callback()))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes one argument and returns a value.
    /// </summary>
    public JSFunction(string name, Func<JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) => callback(args[0])))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes two arguments and returns a value.
    /// </summary>
    public JSFunction(string name, Func<JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) => callback(args[0], args[1])))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes three arguments and returns a value.
    /// </summary>
    public JSFunction(string name, Func<JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) => callback(args[0], args[1], args[2])))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes four arguments and returns a value.
    /// </summary>
    public JSFunction(string name, Func<JSValue, JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) => callback(args[0], args[1], args[2], args[3])))
    {
    }

    /// <summary>
    /// Creates a new named JS function takes five arguments and returns a value.
    /// </summary>
    public JSFunction(
        string name, Func<JSValue, JSValue, JSValue, JSValue, JSValue, JSValue> callback)
        : this(JSValue.CreateFunction(name, (args) =>
            callback(args[0], args[1], args[2], args[3], args[4])))
    {
    }

    #region IJSValue<JSFunction> implementation

    /// <summary>
    /// Checks if the T struct can be created from this instance`.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// `true` if the T struct can be created from this instance. Otherwise it returns `false`.
    /// </returns>
    public bool Is<T>() where T : struct, IJSValue<T> => _value.Is<T>();

    /// <summary>
    /// Tries to create a T struct from this instance.
    /// It returns `null` if the T struct cannot be created.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// Nullable value that contains T struct if it was successfully created
    /// or `null` if it was failed.
    /// </returns>
    public T? As<T>() where T : struct, IJSValue<T> => _value.As<T>();

    /// <summary>
    /// Creates a T struct from this instance without checking the enclosed handle type.
    /// It must be used only when the handle type is known to be correct.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    public T AsUnchecked<T>() where T : struct, IJSValue<T> => _value.AsUnchecked<T>();

    /// <summary>
    /// Creates a T struct from this instance.
    /// It throws `InvalidCastException` in case of failure.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be crated based on this instance.
    /// </exception>
    public T CastTo<T>() where T : struct, IJSValue<T> => _value.CastTo<T>();

    /// <summary>
    /// Determines whether a <see cref="JSFunction" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSFunction" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
#if NET7_0_OR_GREATER
    static bool IJSValue<JSFunction>.CanCreateFrom(JSValue value)
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static bool CanCreateFrom(JSValue value)
#pragma warning restore IDE0051
#endif
        => value.IsFunction();

    /// <summary>
    /// Creates a new instance of <see cref="JSFunction" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSFunction" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSFunction" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSFunction IJSValue<JSFunction>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSFunction CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    #endregion

    /// <summary>
    /// Gets the name of the function, or an empty string if the function is unnamed.
    /// </summary>
    public string Name => (string)_value["name"];

    /// <summary>
    /// Calls the function with the specified `this` value.
    /// </summary>
    public JSValue Apply(JSValue thisArg)
    {
        return _value.CallMethod("apply", thisArg);
    }

    /// <summary>
    /// Calls the function with the specified `this` value and arguments.
    /// </summary>
    public JSValue Apply(JSValue thisArg, JSArray args)
    {
        return _value.CallMethod("apply", thisArg, args);
    }

    /// <summary>
    /// Calls the function with the specified `this` value and arguments.
    /// </summary>
    public JSValue Apply(JSValue thisArg, JSValue[] args)
    {
        return _value.CallMethod("apply", thisArg, new JSArray(args));
    }

    /// <summary>
    /// Creates a new function that when called has the specified `this` value, and
    /// optionally the specified sequence of arguments preceding any provided when the
    /// new function is called.
    /// </summary>
    public JSFunction Bind(JSValue thisArg, params JSValue[] args)
    {
        if (args.Length == 0)
        {
            return (JSFunction)_value.CallMethod("bind", thisArg);
        }
        else
        {
            JSValue[] thisAndArgs = new JSValue[1 + args.Length];
            thisAndArgs[0] = thisArg;
            args.CopyTo(thisAndArgs, 1);
            return (JSFunction)_value.CallMethod("bind", thisAndArgs);
        }
    }

    public JSValue Call(JSValue thisArg) => _value.Call(thisArg);

    public JSValue Call(JSValue thisArg, JSValue arg0) => _value.Call(thisArg, arg0);

    public JSValue Call(JSValue thisArg, JSValue arg0, JSValue arg1)
        => _value.Call(thisArg, arg0, arg1);

    public JSValue Call(JSValue thisArg, JSValue arg0, JSValue arg1, JSValue arg2)
        => _value.Call(thisArg, arg0, arg1, arg2);

    public JSValue Call(JSValue thisArg, params JSValue[] args) => _value.Call(thisArg, args);

    public JSValue CallAsConstructor(params JSValue[] args) => _value.CallAsConstructor(args);

    public JSValue CallAsStatic() => _value.Call(thisArg: default);

    public JSValue CallAsStatic(JSValue arg0) => _value.Call(thisArg: default, arg0);

    public JSValue CallAsStatic(JSValue arg0, JSValue arg1)
        => _value.Call(thisArg: default, arg0, arg1);

    public JSValue CallAsStatic(JSValue arg0, JSValue arg1, JSValue arg2)
        => _value.Call(thisArg: default, arg0, arg1, arg2);

    public JSValue CallAsStatic(params JSValue[] args) => _value.Call(thisArg: default, args);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSFunction a, JSFunction b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSFunction a, JSFunction b) => !a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public bool Equals(JSValue other) => _value.StrictEquals(other);

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is JSValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        throw new NotSupportedException(
            "Hashing JS values is not supported. Use JSSet or JSMap instead.");
    }
}
