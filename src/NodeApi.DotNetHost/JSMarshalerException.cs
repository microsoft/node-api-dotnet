using System;
using System.Reflection;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Exception thrown by <see cref="JSMarshaler" /> about a failure while dynamically generating
/// marshaling expressions.
/// </summary>
public class JSMarshalerException : JSException
{
    public JSMarshalerException(string message, Type type, Exception? innerException = null)
        : base(message + $" Type: {type}", innerException)
    {
        Type = type;
    }

    public JSMarshalerException(string message, MemberInfo member, Exception? innerException = null)
        : base(message + $" Type: {member.DeclaringType}, Member: {member}", innerException)
    {
        Type = member.DeclaringType!;
        Member = member;
    }

    public Type Type { get; }

    public MemberInfo? Member { get; }
}
