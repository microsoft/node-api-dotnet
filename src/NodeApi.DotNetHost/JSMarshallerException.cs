// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Exception thrown by <see cref="JSMarshaller" /> about a failure while dynamically generating
/// marshaling expressions.
/// </summary>
public class JSMarshallerException : JSException
{
    public JSMarshallerException(string message, Type type, Exception? innerException = null)
        : base(message + $" Type: {type}", innerException)
    {
        Type = type;
    }

    public JSMarshallerException(string message, MemberInfo member, Exception? innerException = null)
        : base(message + $" Type: {member.DeclaringType}, Member: {member}", innerException)
    {
        Type = member.DeclaringType!;
        Member = member;
    }

    public Type Type { get; }

    public MemberInfo? Member { get; }
}
