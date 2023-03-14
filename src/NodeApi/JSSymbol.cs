using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.JavaScript.NodeApi;

[DebuggerDisplay("{ToDebugString(),nq}")]
public readonly struct JSSymbol : IEquatable<JSValue>
{
    private readonly JSValue _value;

    private static readonly Lazy<JSReference> s_iteratorSymbol =
        new(new JSReference(JSValue.Global["Symbol"]["iterator"]));

    public static explicit operator JSSymbol(JSValue value) => new(value);
    public static implicit operator JSValue(JSSymbol symbol) => symbol._value;

    private JSSymbol(JSValue value)
    {
        _value = value;
    }

    public JSSymbol(string? description = null)
    {
        _value = JSValue.CreateSymbol(description ?? JSValue.Undefined);
    }

    public static JSSymbol For(ReadOnlySpan<byte> utf8Name)
    {
        return new JSSymbol(JSValue.SymbolFor(utf8Name));
    }

    public static JSSymbol Iterator => (JSSymbol)s_iteratorSymbol.Value.GetValue()!;

    // TODO: Add static properties for other well-known symbols.

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSSymbol a, JSSymbol b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSSymbol a, JSSymbol b) => !a._value.StrictEquals(b);

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
    public override string ToString() => _value.ToString();

    internal string ToDebugString() => _value.ToDebugString();
}
