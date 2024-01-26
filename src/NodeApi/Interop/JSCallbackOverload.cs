// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Holds overload resolution information (parameter types) for one of multiple overloads
/// for a .NET method.
/// </summary>
public readonly struct JSCallbackOverload
{
    public JSCallbackOverload(Type[] parameterTypes, JSCallback callback)
        : this(parameterTypes, null, callback)
    {
    }

    public JSCallbackOverload(Type[] parameterTypes, object?[]? defaultValues, JSCallback callback)
    {
        ParameterTypes = parameterTypes;
        DefaultValues = defaultValues;
        Callback = callback;
    }

    /// <summary>
    /// Specifies the number of parameters and the .NET type that each JS argument will be
    /// converted to before invoking the callback.
    /// </summary>
    public Type[] ParameterTypes { get; }

    /// <summary>
    /// Array of default parameter values for this overload, or null if none of the parameters
    /// have default values.
    /// </summary>
    /// <remarks>
    /// Defaults are always at the end of the list of parameters, so if a method has 2 default
    /// parameters then this array length would be 2. Therfore, the indexes in this array are
    /// offset from the <see cref="ParameterTypes" /> array by the number of non-default parameters.
    /// </remarks>
    public object?[]? DefaultValues { get; }

    /// <summary>
    /// Callback that expects JS arguments that are convertible to the specified parameter types.
    /// </summary>
    public JSCallback Callback { get; }

    /// <summary>
    /// Creates a callback descriptor for a set of method overloads.
    /// </summary>
    /// <param name="name">The method name.</param>
    /// <param name="overloads">Array of objects each having parameter information for one
    /// overload.</param>
    /// <returns>Callback descriptor that can be used for marshalling the method call.</returns>
    public static JSCallbackDescriptor CreateDescriptor(
        string? name,
        JSCallbackOverload[] overloads)
    {
        return new JSCallbackDescriptor(name, ResolveAndInvoke, overloads);
    }

    /// <summary>
    /// Creates a callback descriptor for a set of method overloads, where the overloads are
    /// not loaded until the first call.
    /// </summary>
    /// <param name="name">The method name.</param>
    /// <param name="overloads">Function that returns an array of objects each having parameter
    /// information for one overload. The function is called only once, on the first invocation
    /// of the callback.</param>
    /// <returns>Callback descriptor that can be used for marshalling the method call.</returns>
    public static JSCallbackDescriptor CreateDescriptor(
        string? name,
        Func<JSCallbackOverload[]> deferredOverloads)
    {
        // Use Lazy<T> to ensure the deferral callback is only invoked once,
        // because it can involve expensive compilation of marshalling expressions.
        return new JSCallbackDescriptor(
            name,
            ResolveAndInvokeDeferred,
            new Lazy<JSCallbackOverload[]>(
                deferredOverloads, LazyThreadSafetyMode.ExecutionAndPublication));
    }

    /// <summary>
    /// Selects an overload matching the supplied arguments, and invokes the overload callback.
    /// </summary>
    /// <param name="args">Callback arguments including overload info in the
    /// <see cref="JSCallbackArgs.Data"/> property.</param>
    /// <returns>Result of the callback invocation.</returns>
    /// <exception cref="JSException">No overload or multiple overloads were found for the
    /// supplied arguments.</exception>
    private static JSValue ResolveAndInvoke(JSCallbackArgs args)
    {
        if (args.Data is not IReadOnlyList<JSCallbackOverload> overloads ||
            overloads.Count == 0)
        {
            throw new JSException("Missing overload resolution information.");
        }

        JSCallbackOverload overload = Resolve(args, overloads);
        return Invoke(overload, args);
    }

    /// <summary>
    /// Invokes a deferral callback to get the available overloads, then selects an overload
    /// matching the supplied arguments, and invokes the overload callback.
    /// </summary>
    /// <param name="args">Callback arguments including deferred overload callback in the
    /// <see cref="JSCallbackArgs.Data"/> property.</param>
    /// <returns>Result of the callback invocation.</returns>
    /// <exception cref="JSException">No overload or multiple overloads were found for the
    /// supplied arguments.</exception>
    private static JSValue ResolveAndInvokeDeferred(JSCallbackArgs args)
    {
        if (args.Data is not Lazy<JSCallbackOverload[]> deferredOverloads)
        {
            throw new JSException("Missing deferred overload resolution callback.");
        }

        JSCallbackOverload[] overloads = deferredOverloads.Value;
        JSCallbackOverload overload = Resolve(args, overloads);
        return Invoke(overload, args);
    }

    /// <summary>
    /// Selects a callback by finding the best match of the supplied arguments to method parameter
    /// counts and types.
    /// </summary>
    /// <param name="args">Callback arguments that will be matched against overload
    /// parameter counts and types.</param>
    /// <param name="overloads">List of overloads to be matched.</param>
    /// <returns>Callback for the resolved overload.</returns>
    /// <exception cref="JSException">No overload or multiple overloads were found for the
    /// supplied arguments.</exception>
    public static JSCallbackOverload Resolve(
        JSCallbackArgs args, IReadOnlyList<JSCallbackOverload> overloads)
    {
        // If there's only one overload in the list, no resolution logic is needed.
        if (overloads.Count == 1)
        {
            return overloads[0];
        }

        // First try to match the supplied number of arguments to an overload parameter count.
        // (Avoid using IEnumerable<> queries to prevent boxing the JSCallbackOverload struct.)
        int argsCount = args.Length;
        JSCallbackOverload? matchingOverload = null;
        int matchingCallbackCount = 0;
        foreach (JSCallbackOverload overload in overloads)
        {
            if ((overload.DefaultValues != null &&
                argsCount >= overload.ParameterTypes.Length - overload.DefaultValues.Length &&
                argsCount <= overload.ParameterTypes.Length) ||
                overload.ParameterTypes.Length == argsCount)
            {
                matchingOverload = overload;
                if (++matchingCallbackCount > 1)
                {
                    break;
                }
            }
        }

        if (matchingCallbackCount == 1)
        {
            return matchingOverload!.Value;
        }
        else if (matchingCallbackCount == 0)
        {
            throw new JSException(
                $"No overload was found for the supplied number of arguments ({argsCount}).");
        }

        // Multiple matches were found for the supplied number of arguments.
        // Get the JS value type of each arg and try to match them .NET types.
        Span<JSValueType> argTypes = stackalloc JSValueType[argsCount];
        for (int i = 0; i < argsCount; i++)
        {
            argTypes[i] = args[i].TypeOf();
        }

        matchingOverload = null;
        matchingCallbackCount = 0;
        foreach (JSCallbackOverload overload in overloads)
        {
            if ((overload.DefaultValues != null &&
                argsCount >= overload.ParameterTypes.Length - overload.DefaultValues.Length &&
                argsCount <= overload.ParameterTypes.Length) ||
                overload.ParameterTypes.Length == argsCount)
            {
                bool isMatch = true;
                for (int i = 0; i < argsCount; i++)
                {
                    Type parameterType = overload.ParameterTypes[i];
                    isMatch = parameterType.IsArray ?
                        argTypes[i] == JSValueType.Object && args[i].IsArray() :
                        IsArgumentTypeMatch(argTypes[i], overload.ParameterTypes[i]);
                    if (!isMatch)
                    {
                        break;
                    }
                }

                if (isMatch)
                {
                    matchingOverload = overload;
                    if (++matchingCallbackCount > 1)
                    {
                        break;
                    }
                }
            }
        }

        if (matchingCallbackCount == 1)
        {
            return matchingOverload!.Value;
        }

        string argTypesList = string.Join(", ", argTypes.ToArray());
        if (matchingCallbackCount == 0)
        {
            throw new JSException(
                $"No overload was found for the supplied argument types ({argTypesList}).");
        }
        else
        {
            // TODO: Try to match types more precisely, potentially using some additional type
            // metadata supplied with JS arguments.

            throw new JSException(
                $"Multiple overloads were found for the supplied argument types ({argTypesList}).");
        }
    }

    private static bool IsArgumentTypeMatch(JSValueType argumentType, Type parameterType)
    {
        static bool IsNullable(Type type) => !type.IsValueType ||
            (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>));

        return argumentType switch
        {
            JSValueType.Boolean => parameterType == typeof(bool),
            JSValueType.Number => parameterType.IsPrimitive && parameterType != typeof(bool),
            JSValueType.String => parameterType == typeof(string),
            JSValueType.Null => IsNullable(parameterType),
            JSValueType.Undefined => IsNullable(parameterType),
            JSValueType.Object => !parameterType.IsPrimitive && parameterType != typeof(string),
            JSValueType.Function => parameterType.BaseType == typeof(Delegate),
            JSValueType.BigInt => parameterType == typeof(System.Numerics.BigInteger),
            _ => false,
        };
    }

    private static JSValue GetDefaultArg(Type parameterType, object? defaultValue)
    {
        if (defaultValue == null)
        {
            // JS undefined will convert to null for reference types or default for value types.
            return default;
        }
        else if (parameterType == typeof(string))
        {
            return (JSValue)(string)defaultValue!;
        }
        else if (parameterType == typeof(bool))
        {
            return (JSValue)(bool)defaultValue!;
        }
        else if (parameterType == typeof(sbyte))
        {
            return (JSValue)(sbyte)defaultValue!;
        }
        else if (parameterType == typeof(byte))
        {
            return (JSValue)(byte)defaultValue!;
        }
        else if (parameterType == typeof(short))
        {
            return (JSValue)(short)defaultValue!;
        }
        else if (parameterType == typeof(ushort))
        {
            return (JSValue)(ushort)defaultValue!;
        }
        else if (parameterType == typeof(int))
        {
            return (JSValue)(int)defaultValue!;
        }
        else if (parameterType == typeof(uint))
        {
            return (JSValue)(uint)defaultValue!;
        }
        else if (parameterType == typeof(long))
        {
            return (JSValue)(long)defaultValue!;
        }
        else if (parameterType == typeof(ulong))
        {
            return (JSValue)(ulong)defaultValue!;
        }
        else if (parameterType == typeof(float))
        {
            return (JSValue)(float)defaultValue!;
        }
        else if (parameterType == typeof(double))
        {
            return (JSValue)(double)defaultValue!;
        }
        else
        {
            throw new NotSupportedException(
                "Default parameter type not supported: " + parameterType);
        }
    }

    private static JSValue Invoke(JSCallbackOverload overload, JSCallbackArgs args)
    {
        if (overload.DefaultValues != null && args.Length < overload.ParameterTypes.Length)
        {
            int count = args.Length;
            int countWithDefaults = overload.ParameterTypes.Length;
            int countRequired = countWithDefaults - overload.DefaultValues.Length;
            Span<napi_value> argsWithDefaults = stackalloc napi_value[countWithDefaults];
            for (int i = 0; i < count; i++)
            {
                argsWithDefaults[i] = (napi_value)args[i];
            }
            for (int i = count; i < countWithDefaults; i++)
            {
                argsWithDefaults[i] = (napi_value)GetDefaultArg(
                    overload.ParameterTypes[i], overload.DefaultValues[i - countRequired]);
            }

            return overload.Callback(new JSCallbackArgs(
                args.Scope, (napi_value)args.ThisArg, argsWithDefaults, args.Data));
        }
        else
        {
            return overload.Callback(args);
        }
    }
}
