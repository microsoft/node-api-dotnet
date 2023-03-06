using System;
using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Holds overload resolution information (parameter types) for one of multiple overloads
/// for a .NET method.
/// </summary>
public readonly struct JSCallbackOverload
{
    /// <summary>
    /// Specifies the number of parameters and the .NET type that each JS argument will be
    /// converted to before invoking the callback.
    /// </summary>
    public Type[] ParameterTypes { get; init; }

    /// <summary>
    /// Callback that expects JS arguments that are convertible to the specified parameter types.
    /// </summary>
    public JSCallback Callback { get; init; }

    /// <summary>
    /// Selects a callback matching the supplied arguments, and invokes the callback.
    /// </summary>
    /// <param name="args">Callback arguments including overload info in the
    /// <see cref="JSCallbackArgs.Data"/> property.</param>
    /// <returns>Result of the callback invocation.</returns>
    /// <exception cref="JSException">No overload or multiple overloads were found for the
    /// supplied arguments.</exception>
    /// <remarks>
    /// This method may be use as the <see cref="JSPropertyDescriptor.Method"/> when
    /// a list of <see cref="JSCallbackOverload" /> entries is also provided via the
    /// <see cref="JSPropertyDescriptor.Data" /> property.
    /// </remarks>
    public static JSValue ResolveAndInvoke(JSCallbackArgs args)
    {
        if (args.Data is not IReadOnlyList<JSCallbackOverload> overloads ||
            overloads.Count == 0)
        {
            throw new JSException("Missing overload resolution information.");
        }

        JSCallback callback = Resolve(args, overloads);
        return callback(args);
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
    public static JSCallback Resolve(
        JSCallbackArgs args, IReadOnlyList<JSCallbackOverload> overloads)
    {
        // If there's only one overload in the list, no resolution logic is needed.
        if (overloads.Count == 1)
        {
            return overloads[0].Callback;
        }

        // First try to match the supplied number of arguments to an overload parameter count.
        // (Avoid using IEnumerable<> queries to prevent boxing the JSCallbackOverload struct.)
        int argsCount = args.Length;
        JSCallback? matchingCallback = null;
        int matchingCallbackCount = 0;
        foreach (JSCallbackOverload overload in overloads)
        {
            if (overload.ParameterTypes.Length == argsCount)
            {
                matchingCallback = overload.Callback;
                if (++matchingCallbackCount > 1)
                {
                    break;
                }
            }
        }

        if (matchingCallbackCount == 1)
        {
            return matchingCallback!;
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

        matchingCallback = null;
        matchingCallbackCount = 0;
        foreach (JSCallbackOverload overload in overloads)
        {
            if (overload.ParameterTypes.Length == argsCount)
            {
                bool isMatch = true;
                for (int i = 0; i < argsCount; i++)
                {
                    if (!IsArgumentTypeMatch(argTypes[i], overload.ParameterTypes[i]))
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    matchingCallback = overload.Callback;
                    if (++matchingCallbackCount > 1)
                    {
                        break;
                    }
                }
            }
        }

        if (matchingCallbackCount == 1)
        {
            return matchingCallback!;
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
}
