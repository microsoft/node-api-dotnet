// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

#if NETFRAMEWORK || NETSTANDARD

namespace Microsoft.JavaScript.NodeApi.Generator;

internal static class TypeExtensions
{
    //https://github.com/dotnet/runtime/issues/23493
    public static bool IsGenericTypeParameter(this Type target)
    {
        return target.IsGenericParameter &&
               target.DeclaringType != null &&
               target.DeclaringMethod == null;
    }

    //https://github.com/dotnet/runtime/issues/23493
    public static bool IsGenericMethodParameter(this Type target)
    {
        return target.IsGenericParameter &&
               target.DeclaringMethod != null;
    }
}

#endif
