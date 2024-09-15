// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    /// <param name="deferredOverloads">Function that returns an array of objects each having
    /// parameter information for one overload. The function is called only once, on the first
    /// invocation of the callback.</param>
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

        JSCallbackOverload overload = Resolve(overloads, args);
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
        JSCallbackOverload overload = Resolve(overloads, args);
        return Invoke(overload, args);
    }

    /// <summary>
    /// Selects one of multiple callback overloads by finding the best match of the supplied
    /// arguments to method parameter counts and types.
    /// </summary>
    /// <param name="overloads">List of overloads to be matched.</param>
    /// <param name="args">Callback arguments that will be matched against overload
    /// parameter counts and types.</param>
    /// <returns>Callback for the resolved overload.</returns>
    /// <exception cref="JSException">No overload or multiple overloads were found for the
    /// supplied arguments.</exception>
    public static JSCallbackOverload Resolve(
        IReadOnlyList<JSCallbackOverload> overloads, JSCallbackArgs args)
    {
        // If there's only one overload in the list, no resolution logic is needed.
        if (overloads.Count == 1)
        {
            return overloads[0];
        }

        int argsCount = args.Length;

        // This array tracks which overloads are still considered a match after each resolve step.
        Span<bool> isMatch = stackalloc bool[overloads.Count];

        // First try to match the supplied number of arguments to an overload parameter count.
        JSCallbackOverload? matchingOverload = ResolveByArgumentCount(
            overloads, argsCount, ref isMatch);
        if (matchingOverload != null)
        {
            return matchingOverload!.Value;
        }

        // Multiple matches were found for the supplied number of arguments.
        // Next get the JS value type of each arg and try resolve by matching them .NET types.
        Span<JSValueType> argValueTypes = stackalloc JSValueType[argsCount];
        for (int i = 0; i < argsCount; i++)
        {
            argValueTypes[i] = args[i].TypeOf();
        }

        matchingOverload = ResolveByArgumentJSValueTypes(
            overloads, args, ref argValueTypes, ref isMatch);
        if (matchingOverload != null)
        {
            return matchingOverload!.Value;
        }

        // Multiple matches were found for the supplied argument JS value types.
        // Next try to resolve an overload by finding the best match of numeric types.
        matchingOverload = ResolveByArgumentNumericTypes(
            overloads, args, ref argValueTypes, ref isMatch);
        if (matchingOverload != null)
        {
            return matchingOverload!.Value;
        }

        // Matching numeric types still did not resolve to a single overload.
        // Next try to resolve an overload by finding the best match of object types.
        // This will either resolve a single overload or throw an exception.
        return ResolveByArgumentObjectTypes(overloads, args, ref argValueTypes, ref isMatch);
    }

    private static JSCallbackOverload? ResolveByArgumentCount(
        IReadOnlyList<JSCallbackOverload> overloads,
        int argsCount,
        ref Span<bool> isMatch)
    {
        JSCallbackOverload? matchingOverload = null;
        int matchCount = 0;

        for (int overloadIndex = 0; overloadIndex < overloads.Count; overloadIndex++)
        {
            JSCallbackOverload overload = overloads[overloadIndex];
            int requiredArgsCount = overload.ParameterTypes.Length -
                (overload.DefaultValues?.Length ?? 0);
            int requiredAndOptionalArgsCount = overload.ParameterTypes.Length;

            if (argsCount >= requiredArgsCount && argsCount <= requiredAndOptionalArgsCount)
            {
                isMatch[overloadIndex] = true;
                matchingOverload = overload;
                matchCount++;
            }
        }

        if (matchCount == 0)
        {
            throw new JSException(new JSError(
                $"No overload was found for the supplied number of arguments ({argsCount}).",
                JSErrorType.TypeError));
        }

        return matchCount == 1 ? matchingOverload : null;
    }

    private static JSCallbackOverload? ResolveByArgumentJSValueTypes(
        IReadOnlyList<JSCallbackOverload> overloads,
        JSCallbackArgs args,
        ref Span<JSValueType> argValueTypes,
        ref Span<bool> isMatch)
    {
        JSCallbackOverload? matchingOverload = null;
        int matchCount = 0;

        for (int overloadIndex = 0; overloadIndex < overloads.Count; overloadIndex++)
        {
            JSCallbackOverload overload = overloads[overloadIndex];

            if (!isMatch[overloadIndex])
            {
                // Skip overloads already unmatched by argument count.
                continue;
            }

            bool isMatchByArgTypes = true;
            for (int argIndex = 0; argIndex < argValueTypes.Length; argIndex++)
            {
                Type parameterType = overload.ParameterTypes[argIndex];
                isMatchByArgTypes = parameterType.IsArray ?
                    argValueTypes[argIndex] == JSValueType.Object && args[argIndex].IsArray() :
                    IsArgumentJSValueTypeMatch(argValueTypes[argIndex], parameterType);
                if (!isMatchByArgTypes)
                {
                    break;
                }
            }

            if (isMatchByArgTypes)
            {
                matchingOverload = overload;
                matchCount++;
            }
            else
            {
                isMatch[overloadIndex] = false;
            }
        }

        if (matchCount == 0)
        {
            string argTypesList = string.Join(", ", argValueTypes.ToArray());
            throw new JSException(new JSError(
                $"No overload was found for the supplied argument types ({argTypesList}).",
                JSErrorType.TypeError));
        }

        return matchCount == 1 ? matchingOverload : null;
    }

    private static bool IsArgumentJSValueTypeMatch(JSValueType argumentType, Type parameterType)
    {
        // Note this does not consider nullable type annotations.
        bool isNullable = parameterType.IsGenericType &&
            parameterType.GetGenericTypeDefinition() == typeof(Nullable<>);
        if (isNullable)
        {
            parameterType = Nullable.GetUnderlyingType(parameterType)!;
        }

        return argumentType switch
        {
            JSValueType.Null or JSValueType.Undefined => isNullable || !parameterType.IsValueType,
            JSValueType.Boolean => parameterType == typeof(bool),
            JSValueType.Number => (parameterType.IsPrimitive && parameterType != typeof(bool)) ||
                parameterType.IsEnum || parameterType == typeof(TimeSpan),
            JSValueType.String => parameterType == typeof(string) || parameterType == typeof(Guid),
            JSValueType.Object => !parameterType.IsPrimitive && parameterType != typeof(string),
            JSValueType.Function => typeof(Delegate).IsAssignableFrom(parameterType),
            JSValueType.BigInt => parameterType == typeof(System.Numerics.BigInteger),
            _ => false,
        };
    }

    private static JSCallbackOverload? ResolveByArgumentNumericTypes(
        IReadOnlyList<JSCallbackOverload> overloads,
        JSCallbackArgs args,
        ref Span<JSValueType> argValueTypes,
        ref Span<bool> isMatch)
    {
        JSCallbackOverload? matchingOverload = null;

        JSFunction isIntegerFunction = (JSFunction)JSValue.Global["Number"]["isInteger"];
        JSFunction signFunction = (JSFunction)JSValue.Global["Math"]["sign"];

        for (int argIndex = 0; argIndex < argValueTypes.Length; argIndex++)
        {
            if (argValueTypes[argIndex] != JSValueType.Number)
            {
                // Skip arguments that are not JS numbers.
                continue;
            }

            // These properties will be evaluated (once) only if needed.
            bool? isInteger = null;
            bool? isLongInteger = null;

            // All overload parameters at this index are already confirmed to be some .NET numeric
            // type when matching by JS value type. Try to choose one overload that is the best
            // numeric type match to the supplied JS number.

            int matchCount = 0;
            Type? matchingNumericType = null;
            for (int overloadIndex = 0; overloadIndex < overloads.Count; overloadIndex++)
            {
                if (!isMatch[overloadIndex])
                {
                    // Skip overloads already unmatched by argument count or JS value types.
                    continue;
                }

                Type parameterType = overloads[overloadIndex].ParameterTypes[argIndex];
                if (parameterType.IsEnum)
                {
                    parameterType = Enum.GetUnderlyingType(parameterType);
                }
                else if (parameterType == typeof(TimeSpan))
                {
                    parameterType = typeof(long);
                }
                else if (parameterType.IsGenericType &&
                    parameterType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    parameterType = Nullable.GetUnderlyingType(parameterType)!;
                }

                int specificity = CompareNumericTypeSpecificity(matchingNumericType, parameterType);
                if (specificity == 0)
                {
                    // Multiple overloads have the same numeric type in this parameter index.
                    matchCount++;
                }
                else if (specificity == 1)
                {
                    if (IsArgumentNumericTypeMatch(
                        args[argIndex],
                        parameterType,
                        isIntegerFunction,
                        signFunction,
                        ref isInteger,
                        ref isLongInteger))
                    {
                        // Reset the match count because a more specific numeric type was found.
                        matchCount = 1;
                        matchingOverload = overloads[overloadIndex];
                        matchingNumericType = parameterType;
                    }
                    else
                    {
                        isMatch[overloadIndex] = false;
                    }
                }
            }

            if (matchingOverload == null)
            {
                // The numeric type arg could not be matched, e.g. a non-integer argument was
                // provided for an integer param, or a signed argument for an unsigned param.
                throw new JSException(new JSError(
                    "No overload was found for the supplied numeric argument " +
                    $"at position {argIndex}.",
                    JSErrorType.TypeError));
            }
            else if (matchCount == 1)
            {
                return matchingOverload;
            }
        }

        return null;
    }

    private static int CompareNumericTypeSpecificity(Type? currentType, Type newType)
    {
        if (currentType == null)
        {
            return 1;
        }
        else if (currentType == newType)
        {
            return 0;
        }

        // Integer types are more specific than floating-point types.
        // Smaller integer types are more specific than larger integer types.
        // For types of the same size, unsigned types are more specific than signed.
        bool isCurrentTypeIntegral = IsIntegralType(currentType);
        bool isNewTypeIntegral = IsIntegralType(newType);
        if (isCurrentTypeIntegral && !isNewTypeIntegral)
        {
            return -1;
        }
        else if (isCurrentTypeIntegral)
        {
            bool isCurrentTypeUnsigned = IsUnsignedIntegralType(currentType);
            bool isNewTypeUnsigned = IsUnsignedIntegralType(newType);
            if (isCurrentTypeUnsigned && !isNewTypeUnsigned)
            {
                return -1;
            }
            else if (!isCurrentTypeUnsigned && isNewTypeUnsigned)
            {
                return 1;
            }
            else
            {
                // Both types are signed or both are unsigned.
                return SizeOfNumericType(currentType).CompareTo(SizeOfNumericType(newType));
            }
        }
        else if (isNewTypeIntegral)
        {
            return 1;
        }
        else
        {
            // Both types are floating point.
            // Unlike with integer types, the larger floating-point type is considered
            // more specific, to reduce the potential for loss of precision.
            return SizeOfNumericType(newType).CompareTo(SizeOfNumericType(currentType));
        }
    }

    private static bool IsArgumentNumericTypeMatch(
        JSValue arg,
        Type parameterType,
        JSFunction isIntegerFunction,
        JSFunction signFunction,
        ref bool? isInteger,
        ref bool? isNegativeInteger)
    {
        if (IsIntegralType(parameterType))
        {
            isInteger ??= (bool)isIntegerFunction.CallAsStatic(arg);
            if (!isInteger.Value)
            {
                return false;
            }

            if (IsUnsignedIntegralType(parameterType))
            {
                isNegativeInteger ??= (int)signFunction.CallAsStatic(arg) < 0;
                if (isNegativeInteger.Value)
                {
                    return false;
                }

                ulong integerValue = (ulong)arg;
                return parameterType switch
                {
                    Type t when t == typeof(byte) => integerValue <= byte.MaxValue,
                    Type t when t == typeof(ushort) => integerValue <= ushort.MaxValue,
                    Type t when t == typeof(uint) => integerValue <= uint.MaxValue,
                    _ => true, // No range check for nuint / ulong.
                };
            }
            else
            {
                long integerValue = (long)arg;
                return parameterType switch
                {
                    Type t when t == typeof(sbyte) =>
                        integerValue >= sbyte.MinValue && integerValue <= sbyte.MaxValue,
                    Type t when t == typeof(short) =>
                        integerValue >= short.MinValue && integerValue <= short.MaxValue,
                    Type t when t == typeof(int) =>
                        integerValue >= int.MinValue && integerValue <= int.MaxValue,
                    _ => true, // No range check for nint / long.
                };
            }
        }

        // Any JS number value can match .NET float or double parameter type.
        return true;
    }

    private static bool IsIntegralType(Type type)
    {
        return type switch
        {
            Type t when t == typeof(sbyte) => true,
            Type t when t == typeof(byte) => true,
            Type t when t == typeof(short) => true,
            Type t when t == typeof(ushort) => true,
            Type t when t == typeof(int) => true,
            Type t when t == typeof(uint) => true,
            Type t when t == typeof(nint) => true,
            Type t when t == typeof(nuint) => true,
            Type t when t == typeof(long) => true,
            Type t when t == typeof(ulong) => true,
            _ => false,
        };
    }

    private static bool IsUnsignedIntegralType(Type type)
    {
        return type switch
        {
            Type t when t == typeof(byte) => true,
            Type t when t == typeof(ushort) => true,
            Type t when t == typeof(uint) => true,
            Type t when t == typeof(nuint) => true,
            Type t when t == typeof(ulong) => true,
            _ => false,
        };
    }

    private static int SizeOfNumericType(Type type)
    {
        return type switch
        {
            Type t when t == typeof(sbyte) => sizeof(sbyte),
            Type t when t == typeof(byte) => sizeof(byte),
            Type t when t == typeof(short) => sizeof(short),
            Type t when t == typeof(ushort) => sizeof(ushort),
            Type t when t == typeof(int) => sizeof(int),
            Type t when t == typeof(uint) => sizeof(uint),
            Type t when t == typeof(long) => sizeof(long),
            Type t when t == typeof(ulong) => sizeof(ulong),
            Type t when t == typeof(float) => sizeof(float),
            Type t when t == typeof(double) => sizeof(double),

            // The returned sizes are only used for specificity comparison purposes.
            // For nint/nuint, return a size that is between the size of int and long.
            Type t when t == typeof(nint) => sizeof(int) + sizeof(long) / 2,
            Type t when t == typeof(nuint) => sizeof(uint) + sizeof(ulong) / 2,

            _ => throw new NotSupportedException(
                "Numeric type not supported for overload resolution: " + type.Name),
        };
    }

    private static JSCallbackOverload ResolveByArgumentObjectTypes(
        IReadOnlyList<JSCallbackOverload> overloads,
        JSCallbackArgs args,
        ref Span<JSValueType> argValueTypes,
        ref Span<bool> isMatch)
    {
        JSCallbackOverload? matchingOverload = null;

        for (int argIndex = 0; argIndex < argValueTypes.Length; argIndex++)
        {
            if (argValueTypes[argIndex] != JSValueType.Object)
            {
                // Skip arguments that are not JS objects.
                continue;
            }

            int matchCount = 0;
            Type? matchedParameterType = null;

            JSValue arg = args[argIndex];
            object? obj = arg.TryUnwrap();
            Type? dotnetType = null;
            if (obj != null)
            {
                dotnetType = obj.GetType();
            }

            // The JS type will be evaluated (once) only if needed.
            JSValue? jsType = null;

            for (int overloadIndex = 0; overloadIndex < overloads.Count; overloadIndex++)
            {
                if (!isMatch[overloadIndex])
                {
                    // Skip overloads already unmatched for other reasons.
                    continue;
                }

                Type parameterType = overloads[overloadIndex].ParameterTypes[argIndex];
                if (IsArgumentObjectTypeMatch(arg, parameterType, dotnetType, ref jsType))
                {
                    int specificity = CompareObjectTypeSpecificity(
                        matchedParameterType, parameterType);
                    if (specificity == 0)
                    {
                        // Either the types are the same or neither type is assignable to the other.
                        // This can result in ambiguity in overload resolution unless the overload
                        // is disambiguated by other parameters.
                        matchCount++;
                    }
                    else if (specificity > 0)
                    {
                        // Prefer a more specific type match when selecting an overload.
                        matchCount = 1;
                        matchingOverload = overloads[overloadIndex];
                        matchedParameterType = parameterType;
                    }
                }
                else
                {
                    // The object parameter type does not match. Skip this overload when
                    // evaluating the remaining parameters.
                    isMatch[overloadIndex] = false;
                }
            }

            if (matchCount == 0)
            {
                throw new JSException(new JSError(
                    "No overload was found for the supplied object argument " +
                    $"at position {argIndex}.", JSErrorType.TypeError));
            }
            else if (matchCount == 1)
            {
                return matchingOverload!.Value;
            }
        }

        throw new JSException(new JSError(
            "Multiple overloads were found for the supplied argument types.",
            JSErrorType.TypeError));
    }

    private static int CompareObjectTypeSpecificity(Type? currentType, Type newType)
    {
        if (currentType == null)
        {
            return 1;
        }
        else if (newType == currentType)
        {
            return 0;
        }
        else if (currentType.IsArray && newType == typeof(IList<object>))
        {
            // IList<> is preferred over arrays because it supports marshal-by-reference.
            return 1;
        }
        else if (currentType == typeof(IList<object>) && newType.IsArray)
        {
            return -1;
        }
        else if (currentType.IsAssignableFrom(newType) ||
            (currentType == typeof(IEnumerable<object>) &&
                newType == typeof(IDictionary<object, object>)))
        {
            // IDictionary<> is a special case because the type matching for overload resolution
            // converts interfaces to object element types, which makes the IsAssignableFrom check
            // fail because IDictionary<object, object> does not implement IEnumerable<object>.
            return 1;
        }
        else if (newType.IsAssignableFrom(currentType))
        {
            return -1;
        }
        else
        {
            // Neither type is assignable to the other. This can result in ambiguity in
            // overload resolution unless the overload is disambiguated by other parameters.
            return 0;
        }
    }

    private static bool IsArgumentObjectTypeMatch(
        JSValue arg,
        Type parameterType,
        Type? dotnetType,
        ref JSValue? jsType
    )
    {
        if (dotnetType != null)
        {
            return parameterType.IsAssignableFrom(dotnetType);
        }
        else if (parameterType.IsValueType &&
            !parameterType.IsPrimitive && !parameterType.IsEnum) // struct type
        {
            jsType ??= arg["constructor"];

            if ((parameterType == typeof(DateTime) ||
                parameterType == typeof(DateTimeOffset)) &&
                jsType == JSValue.Global["Date"])
            {
                if (arg.HasProperty("offset"))
                {
                    return parameterType == typeof(DateTimeOffset);
                }
                else if (arg.HasProperty("kind"))
                {
                    return parameterType == typeof(DateTime);
                }
                else
                {
                    return true;
                }
            }
            else if (jsType == JSValue.Global["Object"])
            {
                // TODO: Check for required (non-nullable) properties in the JS object?
                // For now, assume any plain JS object can be marshalled as a .NET struct.
                return true;
            }
            else
            {
                // For structs, the JS object does not directly wrap a .NET object,
                // but the JS object's constructor may still wrap the .NET type.
                dotnetType = jsType?.TryUnwrap() as Type;
                return parameterType == dotnetType;
            }
        }
        else if (parameterType == typeof(IEnumerable<object>))
        {
            // This only checks for IEnumerable<object> (and not other type parameters) because
            // supported collection parameter types have been converted to generic interfaces
            // with object element types for the purposes of overload resolution by
            // JSMarshaller.EnsureObjectCollectionTypeForOverloadResolution().

            return arg.HasProperty(JSSymbol.Iterator);
        }
        else if (parameterType == typeof(IAsyncEnumerable<object>))
        {
            return arg.HasProperty(JSSymbol.AsyncIterator);
        }
        else if (parameterType == typeof(ICollection<object>))
        {
            // Either a JS array or a JS Set object can match this parameter type.
            if (arg.IsArray())
            {
                return true;
            }
            else
            {
                jsType ??= arg["constructor"];
                return jsType == JSValue.Global["Set"];
            }
        }
        else if (parameterType == typeof(IList<object>) || parameterType.IsArray)
        {
            return arg.IsArray();
        }
        else if (parameterType == typeof(ISet<object>))
        {
            jsType ??= arg["constructor"];
            return jsType == JSValue.Global["Set"];
        }
        else if (parameterType == typeof(IDictionary<object, object>))
        {
            jsType ??= arg["constructor"];
            return jsType == JSValue.Global["Map"];
        }
        else if (parameterType == typeof(Task) || (parameterType.IsGenericType &&
            parameterType.GetGenericTypeDefinition() == typeof(Task<>)))
        {
            return arg.IsPromise();
        }

        return false;
    }

    private static JSValue GetDefaultArg(Type parameterType, object? defaultValue)
    {
        if (defaultValue == null)
        {
            // JS undefined will convert to null for reference types or default for value types.
            return default;
        }

        return parameterType switch
        {
            Type t when t == typeof(string) => (JSValue)(string)defaultValue,
            Type t when t == typeof(bool) => (JSValue)(bool)defaultValue,
            Type t when t == typeof(sbyte) => (JSValue)(sbyte)defaultValue,
            Type t when t == typeof(byte) => (JSValue)(byte)defaultValue,
            Type t when t == typeof(short) => (JSValue)(short)defaultValue,
            Type t when t == typeof(ushort) => (JSValue)(ushort)defaultValue,
            Type t when t == typeof(int) => (JSValue)(int)defaultValue,
            Type t when t == typeof(uint) => (JSValue)(uint)defaultValue,
            Type t when t == typeof(long) || t == typeof(nint) => (JSValue)(long)defaultValue,
            Type t when t == typeof(ulong) || t == typeof(nuint) => (JSValue)(ulong)defaultValue,
            Type t when t == typeof(float) => (JSValue)(float)defaultValue,
            Type t when t == typeof(double) => (JSValue)(double)defaultValue,
            _ => throw new NotSupportedException(
                "Default parameter type not supported: " + parameterType.Name),
        };
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
