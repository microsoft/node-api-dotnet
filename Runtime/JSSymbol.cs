using System;
using System.Diagnostics.CodeAnalysis;

namespace NodeApi;

public readonly struct JSSymbol : IEquatable<JSValue>
{
    private readonly JSValue _value;

    private static readonly Lazy<JSReference> _iteratorSymbol =
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

    public static JSSymbol Iterator => (JSSymbol)_iteratorSymbol.Value.GetValue()!;

    // TODO: Add static properties for other well-known symbols.

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
