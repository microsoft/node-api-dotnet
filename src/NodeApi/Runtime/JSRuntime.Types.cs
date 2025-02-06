// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi.Runtime;

using System;
using System.Runtime.InteropServices;

// Type definitions from Node.JS js_native_api.h and js_native_api_types.h
public unsafe partial class JSRuntime
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate napi_value napi_register_module_v1(napi_env env, napi_value exports);

    //===========================================================================
    // Specialized pointer types
    //===========================================================================

    public record struct napi_env(nint Handle)
    {
        public readonly bool IsNull => Handle == default;
        public static napi_env Null => new(default);
    }
    public record struct napi_value(nint Handle)
    {
        public static napi_value Null => new(default);
        public readonly bool IsNull => Handle == default;
    }
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

    public record struct napi_callback(nint Handle)
    {
#if UNMANAGED_DELEGATES
        /// <summary>TEST TEST TEST</summary>
        public napi_callback(
            delegate* unmanaged[Cdecl]<napi_env, napi_callback_info, napi_value> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate napi_value Delegate(napi_env env, napi_callback_info callbackInfo);

        public napi_callback(napi_callback.Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public record struct napi_finalize(nint Handle)
    {
#if UNMANAGED_DELEGATES
        public napi_finalize(delegate* unmanaged[Cdecl]<napi_env, nint, nint, void> handle)
            : this((nint)handle) { }
#endif

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void Delegate(napi_env env, nint data, nint hint);

        public napi_finalize(napi_finalize.Delegate callback)
            : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
    }

    public struct napi_property_descriptor
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

    public readonly struct c_bool
    {
        private readonly byte _value;

        public c_bool(bool value) => _value = (byte)(value ? 1 : 0);

        public static implicit operator c_bool(bool value) => new(value);
        public static explicit operator bool(c_bool value) => value._value != 0;

        public static readonly c_bool True = new(true);
        public static readonly c_bool False = new(false);
    }
}
