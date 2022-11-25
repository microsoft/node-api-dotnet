using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

namespace NodeApi;

public static partial class JSNativeApi
{
  // Node-API Interop definitions and functions.
  [SuppressUnmanagedCodeSecurity]
  public static unsafe partial class Interop
  {
    static Interop()
    {
      // Node APIs are all imported from the main `node` executable. Overriding the import
      // resolution is more efficient and avoids issues with library search paths and
      // differences in the name of the executable.
      NativeLibrary.SetDllImportResolver(
        typeof(JSNativeApi).Assembly,
        (libraryName, _, _) =>
        {
          return libraryName == nameof(NodeApi) ? NativeLibrary.GetMainProgramHandle() : default;
        });
    }

    //===========================================================================
    // Specialized pointer types
    //===========================================================================

    public record struct napi_env(nint Handle);
    public record struct napi_value(nint Handle);
    public record struct napi_ref(nint Handle);
    public record struct napi_handle_scope(nint Handle);
    public record struct napi_escapable_handle_scope(nint Handle);
    public record struct napi_callback_info(nint Handle);
    public record struct napi_deferred(nint Handle);

    //===========================================================================
    // Enum types
    //===========================================================================

    public enum napi_property_attributes : int
    {
      napi_default = 0,
      napi_writable = 1 << 0,
      napi_enumerable = 1 << 1,
      napi_configurable = 1 << 2,

      // Used with napi_define_class to distinguish static properties
      // from instance properties. Ignored by napi_define_properties.
      napi_static = 1 << 10,

      // Default for class methods.
      napi_default_method = napi_writable | napi_configurable,

      // Default for object properties, like in JS obj[prop].
      napi_default_jsproperty = napi_writable | napi_enumerable | napi_configurable,
    }

    public enum napi_valuetype : int
    {
      // ES6 types (corresponds to typeof)
      napi_undefined,
      napi_null,
      napi_boolean,
      napi_number,
      napi_string,
      napi_symbol,
      napi_object,
      napi_function,
      napi_external,
      napi_bigint,
    }

    public enum napi_typedarray_type : int
    {
      napi_int8_array,
      napi_uint8_array,
      napi_uint8_clamped_array,
      napi_int16_array,
      napi_uint16_array,
      napi_int32_array,
      napi_uint32_array,
      napi_float32_array,
      napi_float64_array,
      napi_bigint64_array,
      napi_biguint64_array,
    }

    public enum napi_status : int
    {
      napi_ok,
      napi_invalid_arg,
      napi_object_expected,
      napi_string_expected,
      napi_name_expected,
      napi_function_expected,
      napi_number_expected,
      napi_boolean_expected,
      napi_array_expected,
      napi_generic_failure,
      napi_pending_exception,
      napi_cancelled,
      napi_escape_called_twice,
      napi_handle_scope_mismatch,
      napi_callback_scope_mismatch,
      napi_queue_full,
      napi_closing,
      napi_bigint_expected,
      napi_date_expected,
      napi_arraybuffer_expected,
      napi_detachable_arraybuffer_expected,
      napi_would_deadlock,
    }

    public unsafe struct napi_callback
    {
      public delegate* unmanaged[Cdecl]<napi_env, napi_callback_info, napi_value> Handle;

      public napi_callback(delegate* unmanaged[Cdecl]<napi_env, napi_callback_info, napi_value> handle) =>
        Handle = handle;
    }

    public unsafe struct napi_finalize
    {
      public delegate* unmanaged[Cdecl]<napi_env, nint, nint, void> Handle;

      public napi_finalize(delegate* unmanaged[Cdecl]<napi_env, nint, nint, void> handle) =>
        Handle = handle;
    }

    public unsafe struct napi_property_descriptor
    {
      // One of utf8name or name should be NULL.
      public nint utf8name;
      public napi_value name;

      public napi_callback method;
      public napi_callback getter;
      public napi_callback setter;
      public napi_value value;

      public napi_property_attributes attributes;
      public nint data;
    }

    public struct napi_extended_error_info
    {
      public byte* error_message;
      public nint engine_reserved;
      public uint engine_error_code;
      public napi_status error_code;
    }

    public enum napi_key_collection_mode : int
    {
      napi_key_include_prototypes,
      napi_key_own_only,
    }

    [Flags]
    public enum napi_key_filter : int
    {
      napi_key_all_properties = 0,
      napi_key_writable = 1 << 0,
      napi_key_enumerable = 1 << 1,
      napi_key_configurable = 1 << 2,
      napi_key_skip_strings = 1 << 3,
      napi_key_skip_symbols = 1 << 4,
    }

    public enum napi_key_conversion : int
    {
      napi_key_keep_numbers,
      napi_key_numbers_to_strings,
    }

    public struct napi_type_tag
    {
      public ulong lower;
      public ulong upper;
    }

    public struct c_bool
    {
      private readonly byte value;

      public c_bool(bool value) => this.value = (byte)(value ? 1 : 0);

      public static implicit operator c_bool(bool value) => new c_bool(value);
      public static explicit operator bool(c_bool value) => value.value != 0;

      public static c_bool True = new c_bool(true);
      public static c_bool False = new c_bool(false);
    }

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_last_error_info(napi_env env, out napi_extended_error_info* result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_undefined(napi_env env, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_null(napi_env env, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_global(napi_env env, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_boolean(napi_env env, c_bool value, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_object(napi_env env, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_array(napi_env env, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_array_with_length(napi_env env, nuint length, out napi_value result);

    // napi_status napi_create_double(napi_env env, double value, napi_value *result)
    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_double(napi_env env, double value, out napi_value result);

    // napi_status napi_create_int32(napi_env env, int32_t value, napi_value *result)
    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_int32(napi_env env, int value, out napi_value result);

    // napi_status napi_create_uint32(napi_env env, uint32_t value, napi_value *result)
    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_uint32(napi_env env, uint value, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_int64(napi_env env, long value, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_string_latin1(napi_env env, byte* str, nuint length, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_string_utf8(napi_env env, byte* str, nuint length, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_string_utf16(napi_env env, char* str, nuint length, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_symbol(napi_env env, napi_value description, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_function(napi_env env, byte* utf8name, nuint length,
      napi_callback cb, nint data, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_error(napi_env env, napi_value code, napi_value msg, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_type_error(napi_env env, napi_value code, napi_value msg, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_range_error(napi_env env, napi_value code, napi_value msg, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_typeof(napi_env env, napi_value value, out napi_valuetype result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_double(napi_env env, napi_value value, out double result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_int32(napi_env env, napi_value value, out int result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_uint32(napi_env env, napi_value value, out uint result);

    // napi_status napi_get_value_int64(napi_env env, napi_value value, int64_t *result)
    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_int64(napi_env env, napi_value value, out long result);

    // napi_status napi_get_value_bool(napi_env env, napi_value value, bool *result)
    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_bool(napi_env env, napi_value value, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_string_latin1(napi_env env, napi_value value,
       byte* buf, nuint bufsize, out nuint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_string_utf8(napi_env env, napi_value value,
       byte* buf, nuint bufsize, out nuint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_string_utf16(napi_env env, napi_value value,
       char* buf, nuint bufsize, out nuint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_coerce_to_bool(napi_env env, napi_value value, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_coerce_to_number(napi_env env, napi_value value, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_coerce_to_object(napi_env env, napi_value value, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_coerce_to_string(napi_env env, napi_value value, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_prototype(napi_env env, napi_value @object, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_property_names(napi_env env, napi_value @object, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_set_property(napi_env env, napi_value @object, napi_value key, napi_value value);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_has_property(napi_env env, napi_value @object, napi_value key, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_property(napi_env env, napi_value @object, napi_value key, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_delete_property(napi_env env, napi_value @object, napi_value key, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_has_own_property(napi_env env, napi_value @object, napi_value key, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_set_named_property(
      napi_env env,
      napi_value @object,
      byte* utf8name,
      napi_value value);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_has_named_property(
      napi_env env,
      napi_value @object,
      byte* utf8name,
      out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_named_property(
      napi_env env,
      napi_value @object,
      byte* utf8name,
      out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_set_element(napi_env env, napi_value @object, uint index, napi_value value);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_has_element(napi_env env, napi_value @object, uint index, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_element(napi_env env, napi_value @object, uint index, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_delete_element(napi_env env, napi_value @object, uint index, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_define_properties(napi_env env, napi_value @object,
       nuint property_count, napi_property_descriptor* properties);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_is_array(napi_env env, napi_value value, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_array_length(napi_env env, napi_value value, out uint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_strict_equals(napi_env env, napi_value lhs, napi_value rhs, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static unsafe partial napi_status napi_call_function(napi_env env, napi_value recv, napi_value func,
       nuint argc, napi_value* argv, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_new_instance(napi_env env, napi_value constructor,
       nuint argc, napi_value* argv, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_instanceof(napi_env env, napi_value @object, napi_value constructor, out c_bool result);

    // napi_status napi_get_cb_info(
    //     napi_env env,              // [in] NAPI environment handle
    //     napi_callback_info cbinfo, // [in] Opaque callback-info handle
    //     size_t* argc,              // [in-out] Specifies the size of the provided argv array
    //                                // and receives the actual count of args.
    //     napi_value* argv,          // [out] Array of values
    //     napi_value* this_arg,      // [out] Receives the JS 'this' arg for the call
    //     void** data)               // [out] Receives the data pointer for the callback.
    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static unsafe partial napi_status napi_get_cb_info(napi_env env, napi_callback_info cbinfo,
      nuint* argc, napi_value* argv, napi_value* this_arg, nint data);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_new_target(napi_env env, napi_callback_info cbinfo, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_define_class(
       napi_env env,
       byte* utf8name,
       nuint length,
       napi_callback constructor,
       nint data,
       nuint property_count,
       napi_property_descriptor* properties,
       out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_wrap(
      napi_env env,
      napi_value js_object,
      nint native_object,
      napi_finalize finalize_cb,
      nint finalize_hint,
      napi_ref* result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_unwrap(napi_env env, napi_value js_object, out nint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_remove_wrap(napi_env env, napi_value js_object, out nint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_external(
      napi_env env,
      nint data,
      napi_finalize finalize_cb,
      nint finalize_hint,
      out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_external(napi_env env, napi_value value, out nint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_reference(napi_env env, napi_value value,
       uint initial_refcount, out napi_ref result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_delete_reference(napi_env env, napi_ref @ref);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_reference_ref(napi_env env, napi_ref @ref, nint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_reference_unref(napi_env env, napi_ref @ref, nint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_reference_value(napi_env env, napi_ref @ref, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_open_handle_scope(napi_env env, out napi_handle_scope result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_close_handle_scope(napi_env env, napi_handle_scope scope);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_open_escapable_handle_scope(napi_env env, out napi_escapable_handle_scope result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_close_escapable_handle_scope(napi_env env, napi_escapable_handle_scope scope);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_escape_handle(napi_env env, napi_escapable_handle_scope scope,
       napi_value escapee, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_throw(napi_env env, napi_value error);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_throw_error(napi_env env,
       [MarshalAs(UnmanagedType.LPUTF8Str)] string code, [MarshalAs(UnmanagedType.LPUTF8Str)] string msg);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_throw_type_error(napi_env env,
       [MarshalAs(UnmanagedType.LPUTF8Str)] string code, [MarshalAs(UnmanagedType.LPUTF8Str)] string msg);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_throw_range_error(napi_env env,
       [MarshalAs(UnmanagedType.LPUTF8Str)] string code, [MarshalAs(UnmanagedType.LPUTF8Str)] string msg);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_is_error(napi_env env, napi_value value, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_is_exception_pending(napi_env env, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_and_clear_last_exception(napi_env env, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_is_arraybuffer(napi_env env, napi_value value, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_arraybuffer(napi_env env, nuint byte_length,
       out void* data, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_external_arraybuffer(napi_env env,
       void* external_data, nuint byte_length, napi_finalize finalize_cb,
       nint finalize_hint, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_arraybuffer_info(napi_env env, napi_value arraybuffer,
       out void* data, out nuint byte_length);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_is_typedarray(napi_env env, napi_value value, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_typedarray(
      napi_env env,
      napi_typedarray_type type,
      nuint length,
      napi_value arraybuffer,
      nuint byte_offset,
      out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_typedarray_info(
      napi_env env,
      napi_value typedarray,
      out napi_typedarray_type type,
      out nuint length,
      out void* data,
      out napi_value arraybuffer,
      out nuint byte_offset);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_dataview(napi_env env, nuint length,
       napi_value arraybuffer, nuint byte_offset, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_is_dataview(napi_env env, napi_value value, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_dataview_info(napi_env env, napi_value dataview,
       out nuint bytelength, out void* data, out napi_value arraybuffer, out nuint byte_offset);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_version(napi_env env, out uint result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_promise(napi_env env, out napi_deferred deferred, out napi_value promise);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_resolve_deferred(napi_env env, napi_deferred deferred, napi_value resolution);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_reject_deferred(napi_env env, napi_deferred deferred, napi_value rejection);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_is_promise(napi_env env, napi_value value, out c_bool is_promise);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_run_script(napi_env env, napi_value script, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_adjust_external_memory(napi_env env, long change_in_bytes, out long adjusted_value);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_date(napi_env env, double time, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_is_date(napi_env env, napi_value value, out c_bool is_date);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_date_value(napi_env env, napi_value value, out double result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_add_finalizer(napi_env env, napi_value js_object,
       nint native_object, napi_finalize finalize_cb, nint finalize_hint, napi_ref* result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_bigint_int64(napi_env env, long value, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_bigint_uint64(napi_env env, ulong value, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_create_bigint_words(napi_env env, int sign_bit,
       nuint word_count, ulong* words, out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_bigint_int64(napi_env env, napi_value value,
       out long result, out c_bool lossless);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_bigint_uint64(napi_env env, napi_value value,
       out ulong result, out c_bool lossless);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_value_bigint_words(napi_env env, napi_value value,
       out int sign_bit, out nuint word_count, ulong* words);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_all_property_names(
      napi_env env,
      napi_value @object,
      napi_key_collection_mode key_mode,
      napi_key_filter key_filter,
      napi_key_conversion key_conversion,
      out napi_value result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_set_instance_data(
      napi_env env,
      nint data,
      napi_finalize finalize_cb,
      nint finalize_hint);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_get_instance_data(napi_env env, out nint data);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_detach_arraybuffer(napi_env env, napi_value arraybuffer);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_is_detached_arraybuffer(napi_env env, napi_value value, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_type_tag_object(napi_env env, napi_value value, ref napi_type_tag type_tag);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_check_object_type_tag(napi_env env, napi_value value,
       in napi_type_tag type_tag, out c_bool result);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_object_freeze(napi_env env, napi_value @object);

    [LibraryImport(nameof(NodeApi)), UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    public static partial napi_status napi_object_seal(napi_env env, napi_value @object);
  }
}
