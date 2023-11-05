// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#if NETFRAMEWORK

// This file provides empty definitions for attributes that are not available in .NET Framework.

using System;

namespace System.Diagnostics.CodeAnalysis
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
}

namespace System
{
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

    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    public sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }

        public string ParameterName { get; }
    }
}

#endif
