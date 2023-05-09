// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NETFRAMEWORK

// This file provides empty definitions for attributes that are not available in .NET Framework.

using System;

namespace Microsoft.JavaScript.NodeApi
{
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal class NotNullWhenAttribute : Attribute
    {
        public NotNullWhenAttribute(bool returnValue) { ReturnValue = returnValue; }
        public bool ReturnValue { get; }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    internal class MaybeNullWhenAttribute : Attribute
    {
        public MaybeNullWhenAttribute(bool returnValue) { ReturnValue = returnValue; }
        public bool ReturnValue { get; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class DoesNotReturnAttribute : Attribute
    {
        public DoesNotReturnAttribute() { }
    }

    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class DoesNotReturnIfAttribute : Attribute
    {
        public DoesNotReturnIfAttribute(bool parameterValue) { ParameterValue = parameterValue; }
        public bool ParameterValue { get; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class UnmanagedCallersOnlyAttribute : Attribute
    {
        public UnmanagedCallersOnlyAttribute() { }
        public Type[]? CallConvs;
    }
}

namespace System.Runtime.CompilerServices
{
    public sealed class IsExternalInit
    {
    }
}

#endif
