namespace NodeApi;

// Matches to napi_typedarray_type
public enum JSTypedArrayType : int
{
    Int8,
    UInt8,
    UInt8Clamped,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Float32,
    Float64,
    BigInt64,
    BigUInt64,
}
