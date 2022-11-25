using System;
using System.Diagnostics.Contracts;
using static NodeApi.JSNativeApi.Interop;

namespace NodeApi;

public struct JSValue
{
  private JSValueScope _scope;
  private napi_value _handle;

  public JSValueScope Scope => _scope;

  public JSValue(JSValueScope scope, napi_value handle)
  {
    Contract.Requires(handle.Handle != nint.Zero, "handle must be not null");
    _scope = scope;
    _handle = handle;
  }

  public JSValue(napi_value handle)
  {
    Contract.Requires(handle.Handle != nint.Zero, "handle must be not null");
    _scope = JSValueScope.Current ?? throw new InvalidOperationException("No current scope");
    _handle = handle;
  }

  public napi_value GetCheckedHandle()
  {
    if (_scope.IsInvalid)
    {
      throw new InvalidOperationException("The value handle is invalid because its scope is closed");
    }
    return _handle;
  }

  public static JSValue Undefined => JSNativeApi.GetUndefined();
  public static JSValue Null => JSNativeApi.GetUndefined();
  public static JSValue Global => JSNativeApi.GetGlobal();
  public static JSValue True => JSNativeApi.GetBoolean(true);
  public static JSValue False => JSNativeApi.GetBoolean(false);
  public static JSValue GetBoolean(bool value) => JSNativeApi.GetBoolean(value);

  public static implicit operator JSValue(bool value) => JSNativeApi.GetBoolean(value);
  public static implicit operator JSValue(sbyte value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(byte value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(short value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(ushort value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(int value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(uint value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(long value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(ulong value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(float value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(double value) => JSNativeApi.CreateNumber(value);
  public static implicit operator JSValue(string value) => JSNativeApi.CreateStringUtf16(value);
  public static implicit operator JSValue(JSCallback callback) => JSNativeApi.CreateFunction("Unknown", callback);

  public static explicit operator bool(JSValue value) => value.GetValueBool();
  public static explicit operator sbyte(JSValue value) => (sbyte)value.GetValueInt32();
  public static explicit operator byte(JSValue value) => (byte)value.GetValueUInt32();
  public static explicit operator short(JSValue value) => (short)value.GetValueInt32();
  public static explicit operator ushort(JSValue value) => (ushort)value.GetValueUInt32();
  public static explicit operator int(JSValue value) => value.GetValueInt32();
  public static explicit operator uint(JSValue value) => value.GetValueUInt32();
  public static explicit operator long(JSValue value) => value.GetValueInt64();
  public static explicit operator ulong(JSValue value) => (ulong)value.GetValueInt64();
  public static explicit operator float(JSValue value) => (float)value.GetValueDouble();
  public static explicit operator double(JSValue value) => value.GetValueDouble();
  public static explicit operator string(JSValue value) => value.GetValueStringUtf16();

  public JSValue this[JSValue name]
  {
    get => this.GetProperty(name);
    set => this.SetProperty(name, value);
  }

  public JSValue this[string name]
  {
    get => this.GetProperty(name);
    set { this.SetProperty(name, value); }
  }

  public JSValue this[int index]
  {
    get { return this.GetElement(index); }
    set { this.SetElement(index, value); }
  }

  public static explicit operator napi_value(JSValue value) => value.GetCheckedHandle();
  public static explicit operator napi_value(JSValue? value) => value != null ? value.Value.GetCheckedHandle() : new napi_value(nint.Zero);

  public static implicit operator JSValue(napi_value handle) => new JSValue(handle);
  public static implicit operator JSValue?(napi_value handle) => handle.Handle != nint.Zero ? new JSValue(handle) : (JSValue?)null;
}
