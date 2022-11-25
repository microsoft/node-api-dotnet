namespace NodeApi;

// Matches to napi_typed_arraytype
public enum JSTypedArrayType : int
{
  SByteArray,
  ByteArray,
  ByteClampedArray,
  Int16Array,
  UInt16Array,
  Int32Array,
  UInt32Array,
  SingleArray,
  DoubleArray,
  BigInt64Array,
  BigUInt64Array,
}
