using System;

namespace NodeApi;

public struct JSArrayBuffer
{
    private JSValue _value;

    public static implicit operator JSValue(JSArrayBuffer value) => value._value;
    public static implicit operator JSObject(JSArrayBuffer value) => (JSObject)value._value;

    public static explicit operator JSArrayBuffer(JSValue value) => new() { _value = value };
    public static explicit operator JSArrayBuffer(JSObject value) => (JSArrayBuffer)(JSValue)value;

    public JSArrayBuffer(int byteLength) =>
        _value = JSValue.CreateArrayBuffer(byteLength);

    public JSArrayBuffer(ReadOnlySpan<byte> data)
        => _value = JSValue.CreateArrayBuffer(data);

    public JSArrayBuffer(object? external, ReadOnlyMemory<byte> data)
        => _value = JSValue.CreateExternalArrayBuffer(external, data);

    public JSArrayBuffer(ReadOnlyMemory<byte> data)
        => _value = JSValue.CreateExternalArrayBuffer(null, data);

    public Span<byte> Data => _value.GetArrayBufferInfo();

    public int ByteLength => _value.GetArrayBufferInfo().Length;

    public bool IsDetached => _value.IsDetachedArrayBuffer();

    public void Detach() => _value.DetachArrayBuffer();
}
