// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Definitions from Node.JS js_native_api.h and js_native_api_types.h

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Microsoft.JavaScript.NodeApi;

public static partial class JSNativeApi
{
    // Node-API Interop definitions and functions.
    [SuppressUnmanagedCodeSecurity]
    public static unsafe partial class Interop
    {
        private static nint s_libraryHandle;
        private static FunctionFields s_fields = new();
        private static bool s_initialized;

        public static void Initialize(nint libraryHandle = default)
        {
            if (s_initialized) return;
            s_initialized = true;

            if (libraryHandle == default)
            {
                libraryHandle = NativeLibrary.GetMainProgramHandle();
            }

            s_libraryHandle = libraryHandle;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate napi_value napi_register_module_v1(napi_env env, napi_value exports);

        public static readonly nuint NAPI_AUTO_LENGTH = unchecked((nuint)(-1));

        private struct FunctionFields
        {
            // js_native_api.h APIs
            public nint napi_get_last_error_info;
            public nint napi_get_undefined;
            public nint napi_get_null;
            public nint napi_get_global;
            public nint napi_get_boolean;
            public nint napi_create_object;
            public nint napi_create_array;
            public nint napi_create_array_with_length;
            public nint napi_create_double;
            public nint napi_create_int32;
            public nint napi_create_uint32;
            public nint napi_create_int64;
            public nint napi_create_string_latin1;
            public nint napi_create_string_utf8;
            public nint napi_create_string_utf16;
            public nint napi_create_symbol;
            public nint node_api_symbol_for;
            public nint napi_create_function;
            public nint napi_create_error;
            public nint napi_create_type_error;
            public nint napi_create_range_error;
            public nint node_api_create_syntax_error;
            public nint napi_typeof;
            public nint napi_get_value_double;
            public nint napi_get_value_int32;
            public nint napi_get_value_uint32;
            public nint napi_get_value_int64;
            public nint napi_get_value_bool;
            public nint napi_get_value_string_latin1;
            public nint napi_get_value_string_utf8;
            public nint napi_get_value_string_utf16;
            public nint napi_coerce_to_bool;
            public nint napi_coerce_to_number;
            public nint napi_coerce_to_object;
            public nint napi_coerce_to_string;
            public nint napi_get_prototype;
            public nint napi_get_property_names;
            public nint napi_set_property;
            public nint napi_has_property;
            public nint napi_get_property;
            public nint napi_delete_property;
            public nint napi_has_own_property;
            public nint napi_set_named_property;
            public nint napi_has_named_property;
            public nint napi_get_named_property;
            public nint napi_set_element;
            public nint napi_has_element;
            public nint napi_get_element;
            public nint napi_delete_element;
            public nint napi_define_properties;
            public nint napi_is_array;
            public nint napi_get_array_length;
            public nint napi_strict_equals;
            public nint napi_call_function;
            public nint napi_new_instance;
            public nint napi_instanceof;
            public nint napi_get_cb_info;
            public nint napi_get_new_target;
            public nint napi_define_class;
            public nint napi_wrap;
            public nint napi_unwrap;
            public nint napi_remove_wrap;
            public nint napi_create_external;
            public nint napi_get_value_external;
            public nint napi_create_reference;
            public nint napi_delete_reference;
            public nint napi_reference_ref;
            public nint napi_reference_unref;
            public nint napi_get_reference_value;
            public nint napi_open_handle_scope;
            public nint napi_close_handle_scope;
            public nint napi_open_escapable_handle_scope;
            public nint napi_close_escapable_handle_scope;
            public nint napi_escape_handle;
            public nint napi_throw;
            public nint napi_throw_error;
            public nint napi_throw_type_error;
            public nint napi_throw_range_error;
            public nint node_api_throw_syntax_error;
            public nint napi_is_error;
            public nint napi_is_exception_pending;
            public nint napi_get_and_clear_last_exception;
            public nint napi_is_arraybuffer;
            public nint napi_create_arraybuffer;
            public nint napi_create_external_arraybuffer;
            public nint napi_get_arraybuffer_info;
            public nint napi_is_typedarray;
            public nint napi_create_typedarray;
            public nint napi_get_typedarray_info;
            public nint napi_create_dataview;
            public nint napi_is_dataview;
            public nint napi_get_dataview_info;
            public nint napi_get_version;
            public nint napi_create_promise;
            public nint napi_resolve_deferred;
            public nint napi_reject_deferred;
            public nint napi_is_promise;
            public nint napi_run_script;
            public nint napi_adjust_external_memory;
            public nint napi_create_date;
            public nint napi_is_date;
            public nint napi_get_date_value;
            public nint napi_add_finalizer;
            public nint napi_create_bigint_int64;
            public nint napi_create_bigint_uint64;
            public nint napi_create_bigint_words;
            public nint napi_get_value_bigint_int64;
            public nint napi_get_value_bigint_uint64;
            public nint napi_get_value_bigint_words;
            public nint napi_get_all_property_names;
            public nint napi_set_instance_data;
            public nint napi_get_instance_data;
            public nint napi_detach_arraybuffer;
            public nint napi_is_detached_arraybuffer;
            public nint napi_type_tag_object;
            public nint napi_check_object_type_tag;
            public nint napi_object_freeze;
            public nint napi_object_seal;

            // node_api.h APIs
            public nint napi_module_register;
            public nint napi_fatal_error;
            public nint napi_async_init;
            public nint napi_async_destroy;
            public nint napi_make_callback;
            public nint napi_create_buffer;
            public nint napi_create_external_buffer;
            public nint napi_create_buffer_copy;
            public nint napi_is_buffer;
            public nint napi_get_buffer_info;
            public nint napi_create_async_work;
            public nint napi_delete_async_work;
            public nint napi_queue_async_work;
            public nint napi_cancel_async_work;
            public nint napi_get_node_version;
            public nint napi_get_uv_event_loop;
            public nint napi_fatal_exception;
            public nint napi_add_env_cleanup_hook;
            public nint napi_remove_env_cleanup_hook;
            public nint napi_open_callback_scope;
            public nint napi_close_callback_scope;
            public nint napi_create_threadsafe_function;
            public nint napi_get_threadsafe_function_context;
            public nint napi_call_threadsafe_function;
            public nint napi_acquire_threadsafe_function;
            public nint napi_release_threadsafe_function;
            public nint napi_unref_threadsafe_function;
            public nint napi_ref_threadsafe_function;
            public nint napi_add_async_cleanup_hook;
            public nint napi_remove_async_cleanup_hook;
            public nint node_api_get_module_file_name;
        }

        //===========================================================================
        // Specialized pointer types
        //===========================================================================

        public record struct napi_env(nint Handle)
        {
            public bool IsNull => Handle == default;
            public static napi_env Null => new(default);
        }
        public record struct napi_value(nint Handle)
        {
            public static napi_value Null => new(default);
            public bool IsNull => Handle == default;
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

        public struct napi_callback
        {
            public nint Handle;

#if NETFRAMEWORK
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate napi_value Delegate(napi_env env, napi_callback_info callbackInfo);

            public napi_callback(napi_callback.Delegate callback)
                => Handle = Marshal.GetFunctionPointerForDelegate(callback);
#else
            public napi_callback(
                delegate* unmanaged[Cdecl]<napi_env, napi_callback_info, napi_value> handle)
                => Handle = (nint)handle;
#endif
        }

        public struct napi_finalize
        {
            public nint Handle;

#if NETFRAMEWORK
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void Delegate(napi_env env, nint data, nint hint);

            public napi_finalize(napi_finalize.Delegate callback)
                => Handle = Marshal.GetFunctionPointerForDelegate(callback);
#else
            public napi_finalize(delegate* unmanaged[Cdecl]<napi_env, nint, nint, void> handle)
                => Handle = (nint)handle;
#endif
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

        public struct napi_type_tag
        {
            public ulong lower;
            public ulong upper;
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

        internal static napi_status napi_get_last_error_info(napi_env env, out nint result)
            => CallInterop(ref s_fields.napi_get_last_error_info, env, out result);

        internal static napi_status napi_get_undefined(napi_env env, out napi_value result)
            => CallInterop(ref s_fields.napi_get_undefined, env, out result);

        internal static napi_status napi_get_null(napi_env env, out napi_value result)
            => CallInterop(ref s_fields.napi_get_null, env, out result);

        internal static napi_status napi_get_global(napi_env env, out napi_value result)
            => CallInterop(ref s_fields.napi_get_global, env, out result);

        internal static napi_status napi_get_boolean(
            napi_env env, c_bool value, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_get_boolean);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, c_bool, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, value, (nint)result_native);
            }
        }

        internal static napi_status napi_create_object(napi_env env, out napi_value result)
            => CallInterop(ref s_fields.napi_create_object, env, out result);

        internal static napi_status napi_create_array(napi_env env, out napi_value result)
            => CallInterop(ref s_fields.napi_create_array, env, out result);

        internal static napi_status napi_create_array_with_length(
            napi_env env, nuint length, out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_array_with_length, env, (nint)length, out result);

        internal static napi_status napi_create_double(
            napi_env env, double value, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_double);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, double, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, value, (nint)result_native);
            }
        }

        internal static napi_status napi_create_int32(
            napi_env env, int value, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_int32);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, int, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, value, (nint)result_native);
            }
        }

        internal static napi_status napi_create_uint32(
            napi_env env, uint value, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_uint32);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, uint, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, value, (nint)result_native);
            }
        }

        internal static napi_status napi_create_int64(
            napi_env env, long value, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_int64);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, long, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, value, (nint)result_native);
            }
        }

        internal static napi_status napi_create_string_latin1(
            napi_env env, byte* str, nuint length, out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_string_latin1,
                env,
                (nint)str,
                (nint)length,
                out result);

        internal static napi_status napi_create_string_utf8(
            napi_env env, byte* str, nuint length, out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_string_utf8,
                env,
                (nint)str,
                (nint)length,
                out result);

        internal static napi_status napi_create_string_utf16(
            napi_env env, char* str, nuint length, out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_string_utf16,
                env,
                (nint)str,
                (nint)length,
                out result);

        internal static napi_status napi_create_symbol(
            napi_env env, napi_value description, out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_symbol, env, description.Handle, out result);

        internal static napi_status node_api_symbol_for(
            napi_env env, byte* utf8name, nuint length, out napi_value result)
            => CallInterop(
                ref s_fields.node_api_symbol_for,
                env,
                (nint)utf8name,
                (nint)length,
                out result);

        internal static napi_status napi_create_function(
            napi_env env,
            byte* utf8name,
            nuint length,
            napi_callback cb,
            nint data,
            out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_function,
                env,
                (nint)utf8name,
                (nint)length,
                (nint)cb.Handle,
                data,
                out result);

        internal static napi_status napi_create_error(
            napi_env env, napi_value code, napi_value msg, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_error);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, code, msg, (nint)result_native);
            }
        }

        internal static napi_status napi_create_type_error(
            napi_env env, napi_value code, napi_value msg, out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_type_error,
                env,
                code.Handle,
                msg.Handle,
                out result);

        internal static napi_status napi_create_range_error(
            napi_env env, napi_value code, napi_value msg, out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_range_error,
                env,
                code.Handle,
                msg.Handle,
                out result);

        internal static napi_status node_api_create_syntax_error(
            napi_env env, napi_value code, napi_value msg, out napi_value result)
            => CallInterop(
                ref s_fields.node_api_create_syntax_error,
                env,
                code.Handle,
                msg.Handle,
                out result);

        internal static napi_status napi_typeof(
            napi_env env, napi_value value, out napi_valuetype result)
            => CallInterop(ref s_fields.napi_typeof, env, value.Handle, out result);

        internal static napi_status napi_get_value_double(
            napi_env env, napi_value value, out double result)
            => CallInterop(
                ref s_fields.napi_get_value_double, env, value.Handle, out result);

        internal static napi_status napi_get_value_int32(
            napi_env env, napi_value value, out int result)
            => CallInterop(
                ref s_fields.napi_get_value_int32, env, value.Handle, out result);

        internal static napi_status napi_get_value_uint32(
            napi_env env, napi_value value, out uint result)
            => CallInterop(
                ref s_fields.napi_get_value_uint32, env, value.Handle, out result);

        internal static napi_status napi_get_value_int64(
            napi_env env, napi_value value, out long result)
            => CallInterop(
                ref s_fields.napi_get_value_int64, env, value.Handle, out result);

        internal static napi_status napi_get_value_bool(
            napi_env env, napi_value value, out c_bool result)
            => CallInterop(
                ref s_fields.napi_get_value_bool, env, value.Handle, out result);

        internal static napi_status napi_get_value_string_latin1(
            napi_env env, napi_value value, nint buf, nuint bufsize, out nuint result)
            => CallInterop(
                ref s_fields.napi_get_value_string_latin1,
                env,
                value.Handle,
                buf,
                (nint)bufsize,
                out result);

        internal static napi_status napi_get_value_string_utf8(
            napi_env env, napi_value value, nint buf, nuint bufsize, out nuint result)
            => CallInterop(
                ref s_fields.napi_get_value_string_utf8,
                env,
                value.Handle,
                buf,
                (nint)bufsize,
                out result);

        internal static napi_status napi_get_value_string_utf16(
            napi_env env, napi_value value, nint buf, nuint bufsize, out nuint result)
            => CallInterop(
                ref s_fields.napi_get_value_string_utf16,
                env,
                value.Handle,
                buf,
                (nint)bufsize,
                out result);

        internal static napi_status napi_coerce_to_bool(
            napi_env env, napi_value value, out napi_value result)
            => CallInterop(ref s_fields.napi_coerce_to_bool, env, value.Handle, out result);

        internal static napi_status napi_coerce_to_number(
            napi_env env, napi_value value, out napi_value result)
            => CallInterop(
                ref s_fields.napi_coerce_to_number, env, value.Handle, out result);

        internal static napi_status napi_coerce_to_object(
            napi_env env, napi_value value, out napi_value result)
            => CallInterop(
                ref s_fields.napi_coerce_to_object, env, value.Handle, out result);

        internal static napi_status napi_coerce_to_string(
            napi_env env, napi_value value, out napi_value result)
            => CallInterop(
                ref s_fields.napi_coerce_to_string, env, value.Handle, out result);

        internal static napi_status napi_get_prototype(
            napi_env env, napi_value js_object, out napi_value result)
            => CallInterop(
                ref s_fields.napi_get_prototype, env, js_object.Handle, out result);

        internal static napi_status napi_get_property_names(
            napi_env env, napi_value js_object, out napi_value result)
            => CallInterop(
                ref s_fields.napi_get_property_names, env, js_object.Handle, out result);

        internal static napi_status napi_set_property(
            napi_env env, napi_value js_object, napi_value key, napi_value value)
            => CallInterop(
                ref s_fields.napi_set_property,
                env,
                js_object.Handle,
                key.Handle,
                value.Handle);

        internal static napi_status napi_has_property(
            napi_env env, napi_value js_object, napi_value key, out c_bool result)
            => CallInterop(
                ref s_fields.napi_has_property,
                env,
                js_object.Handle,
                key.Handle,
                out result);

        internal static napi_status napi_get_property(
            napi_env env, napi_value js_object, napi_value key, out napi_value result)
            => CallInterop(
                ref s_fields.napi_get_property,
                env,
                js_object.Handle,
                key.Handle,
                out result);

        internal static napi_status napi_delete_property(
            napi_env env, napi_value js_object, napi_value key, out c_bool result)
            => CallInterop(
                ref s_fields.napi_delete_property,
                env,
                js_object.Handle,
                key.Handle,
                out result);

        internal static napi_status napi_has_own_property(
            napi_env env, napi_value js_object, napi_value key, out c_bool result)
            => CallInterop(
                ref s_fields.napi_has_own_property,
                env,
                js_object.Handle,
                key.Handle,
                out result);

        internal static napi_status napi_set_named_property(
            napi_env env, napi_value js_object, nint utf8name, napi_value value)
            => CallInterop(
                ref s_fields.napi_set_named_property,
                env,
                js_object.Handle,
                utf8name,
                value.Handle);

        internal static napi_status napi_has_named_property(
            napi_env env, napi_value js_object, nint utf8name, out c_bool result)
            => CallInterop(
                ref s_fields.napi_has_named_property,
                env,
                js_object.Handle,
                utf8name,
                out result);

        internal static napi_status napi_get_named_property(
            napi_env env, napi_value js_object, nint utf8name, out napi_value result)
            => CallInterop(
                ref s_fields.napi_get_named_property,
                env,
                js_object.Handle,
                utf8name,
                out result);

        internal static napi_status napi_set_element(
            napi_env env, napi_value js_object, uint index, napi_value value)
        {
            nint funcHandle = GetExport(ref s_fields.napi_set_element);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, uint, napi_value, napi_status>)funcHandle;
            return funcDelegate(env, js_object, index, value);
        }

        internal static napi_status napi_has_element(
            napi_env env, napi_value js_object, uint index, out c_bool result)
            => CallInterop(
                ref s_fields.napi_has_element,
                env,
                js_object.Handle,
                index,
                out result);

        internal static napi_status napi_get_element(
            napi_env env, napi_value js_object, uint index, out napi_value result)
            => CallInterop(
                ref s_fields.napi_get_element,
                env,
                js_object.Handle,
                index,
                out result);

        internal static napi_status napi_delete_element(
            napi_env env, napi_value js_object, uint index, out c_bool result)
            => CallInterop(
                ref s_fields.napi_delete_element, env, js_object.Handle, index, out result);

        internal static napi_status napi_define_properties(
            napi_env env, napi_value js_object, nuint property_count, nint properties)
            => CallInterop(
                ref s_fields.napi_define_properties,
                env,
                js_object.Handle,
                (nint)property_count,
                properties);

        internal static napi_status napi_is_array(
            napi_env env, napi_value value, out c_bool result)
            => CallInterop(ref s_fields.napi_is_array, env, value.Handle, out result);

        internal static napi_status napi_get_array_length(
            napi_env env, napi_value value, out uint result)
            => CallInterop(
                ref s_fields.napi_get_array_length, env, value.Handle, out result);

        internal static napi_status napi_strict_equals(
            napi_env env, napi_value lhs, napi_value rhs, out c_bool result)
            => CallInterop(
                ref s_fields.napi_strict_equals, env, lhs.Handle, rhs.Handle, out result);

        internal static napi_status napi_call_function(
            napi_env env,
            napi_value recv,
            napi_value func,
            nuint argc,
            nint argv,
            out napi_value result)
            => CallInterop(
                ref s_fields.napi_call_function,
                env,
                recv.Handle,
                func.Handle,
                (nint)argc,
                argv,
                out result);

        internal static napi_status napi_new_instance(
            napi_env env,
            napi_value constructor,
            nuint argc,
            nint argv,
            out napi_value result)
            => CallInterop(
                ref s_fields.napi_new_instance,
                env,
                constructor.Handle,
                (nint)argc,
                argv,
                out result);

        internal static napi_status napi_instanceof(
            napi_env env, napi_value js_object, napi_value constructor, out c_bool result)
            => CallInterop(
                ref s_fields.napi_instanceof,
                env,
                js_object.Handle,
                constructor.Handle,
                out result);

        internal static napi_status napi_get_cb_info(
            napi_env env,              // [in] NAPI environment handle
            napi_callback_info cbinfo, // [in] Opaque callback-info handle
            nuint* argc,               // [in-out] Specifies the size of the provided argv array
                                       // and receives the actual count of args.
            napi_value* argv,          // [out] Array of values
            napi_value* this_arg,      // [out] Receives the JS 'this' arg for the call
            nint* data)                // [out] Receives the data pointer for the callback.
            => CallInterop(
                ref s_fields.napi_get_cb_info,
                env,
                cbinfo.Handle,
                (nint)argc,
                (nint)argv,
                (nint)this_arg,
                (nint)data);

        internal static napi_status napi_get_new_target(
            napi_env env, napi_callback_info cbinfo, out napi_value result)
            => CallInterop(
                ref s_fields.napi_get_new_target, env, cbinfo.Handle, out result);

        internal static napi_status napi_define_class(
            napi_env env,
            nint utf8name,
            nuint length,
            napi_callback constructor,
            nint data,
            nuint property_count,
            nint properties,
            out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_define_class);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, nint, nint, nint, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(
                    env,
                    utf8name,
                    (nint)length,
                    (nint)constructor.Handle,
                    data,
                    (nint)property_count,
                    properties,
                    (nint)result_native);
            }
        }

        internal static napi_status napi_wrap(
            napi_env env,
            napi_value js_object,
            nint native_object,
            napi_finalize finalize_cb,
            nint finalize_hint,
            napi_ref* result)
            => CallInterop(
                ref s_fields.napi_wrap,
                env,
                js_object.Handle,
                native_object,
                (nint)finalize_cb.Handle,
                finalize_hint,
                (nint)result);

        internal static napi_status napi_unwrap(
            napi_env env, napi_value js_object, out nint result)
            => CallInterop(ref s_fields.napi_unwrap, env, js_object.Handle, out result);

        internal static napi_status napi_remove_wrap(
            napi_env env, napi_value js_object, out nint result)
            => CallInterop(
                ref s_fields.napi_remove_wrap, env, js_object.Handle, out result);

        internal static napi_status napi_create_external(
            napi_env env,
            nint data,
            napi_finalize finalize_cb,
            nint finalize_hint,
            out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_external,
                env,
                data,
                (nint)finalize_cb.Handle,
                finalize_hint,
                out result);

        internal static napi_status napi_get_value_external(
            napi_env env, napi_value value, out nint result)
            => CallInterop(
                ref s_fields.napi_get_value_external, env, value.Handle, out result);

        internal static napi_status napi_create_reference(
            napi_env env, napi_value value, uint initial_refcount, out napi_ref result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_reference);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, napi_value, uint, nint, napi_status>)funcHandle;
            fixed (napi_ref* result_native = &result)
            {
                return funcDelegate(env, value, initial_refcount, (nint)result_native);
            }
        }

        internal static napi_status napi_delete_reference(napi_env env, napi_ref @ref)
            => CallInterop(ref s_fields.napi_delete_reference, env, @ref.Handle);

        internal static napi_status napi_reference_ref(napi_env env, napi_ref @ref, nint result)
            => CallInterop(ref s_fields.napi_reference_ref, env, @ref.Handle, result);

        internal static napi_status napi_reference_unref(napi_env env, napi_ref @ref, nint result)
            => CallInterop(ref s_fields.napi_reference_unref, env, @ref.Handle, result);

        internal static napi_status napi_get_reference_value(
            napi_env env, napi_ref @ref, out napi_value result)
            => CallInterop(
                ref s_fields.napi_get_reference_value, env, @ref.Handle, out result);

        internal static napi_status napi_open_handle_scope(
            napi_env env, out napi_handle_scope result)
            => CallInterop(ref s_fields.napi_open_handle_scope, env, out result);

        internal static napi_status napi_close_handle_scope(napi_env env, napi_handle_scope scope)
            => CallInterop(ref s_fields.napi_close_handle_scope, env, scope.Handle);

        internal static napi_status napi_open_escapable_handle_scope(
            napi_env env, out napi_escapable_handle_scope result)
            => CallInterop(ref s_fields.napi_open_escapable_handle_scope, env, out result);

        internal static napi_status napi_close_escapable_handle_scope(
            napi_env env, napi_escapable_handle_scope scope)
            => CallInterop(
                ref s_fields.napi_close_escapable_handle_scope, env, scope.Handle);

        internal static napi_status napi_escape_handle(napi_env env,
            napi_escapable_handle_scope scope, napi_value escapee, out napi_value result)
            => CallInterop(
                ref s_fields.napi_escape_handle,
                env,
                scope.Handle,
                escapee.Handle,
                out result);

        internal static napi_status napi_throw(napi_env env, napi_value error)
            => CallInterop(ref s_fields.napi_throw, env, error.Handle);

        internal static napi_status napi_throw_error(napi_env env, string? code, string msg)
            => CallInterop(ref s_fields.napi_throw_error, env, code, msg);

        internal static napi_status napi_throw_type_error(napi_env env, string? code, string msg)
            => CallInterop(ref s_fields.napi_throw_type_error, env, code, msg);

        internal static napi_status napi_throw_range_error(napi_env env, string? code, string msg)
            => CallInterop(ref s_fields.napi_throw_range_error, env, code, msg);

        internal static napi_status node_api_throw_syntax_error(
            napi_env env, string? code, string msg)
            => CallInterop(ref s_fields.node_api_throw_syntax_error, env, code, msg);

        internal static napi_status napi_is_error(
            napi_env env, napi_value value, out c_bool result)
            => CallInterop(ref s_fields.napi_is_error, env, value.Handle, out result);

        internal static napi_status napi_is_exception_pending(napi_env env, out c_bool result)
            => CallInterop(ref s_fields.napi_is_exception_pending, env, out result);

        internal static napi_status napi_get_and_clear_last_exception(
            napi_env env, out napi_value result)
            => CallInterop(ref s_fields.napi_get_and_clear_last_exception, env, out result);

        internal static napi_status napi_is_arraybuffer(
            napi_env env, napi_value value, out c_bool result)
            => CallInterop(ref s_fields.napi_is_arraybuffer, env, value.Handle, out result);

        internal static napi_status napi_create_arraybuffer(
            napi_env env, nuint byte_length, out nint data, out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_arraybuffer,
                env,
                (nint)byte_length,
                out data,
                out result);

        internal static napi_status napi_create_external_arraybuffer(
            napi_env env,
            nint external_data,
            nuint byte_length,
            napi_finalize finalize_cb,
            nint finalize_hint,
            out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_external_arraybuffer,
                env,
                external_data,
                (nint)byte_length,
                (nint)finalize_cb.Handle,
                finalize_hint,
                out result);

        internal static napi_status napi_get_arraybuffer_info(
            napi_env env, napi_value arraybuffer, out nint data, out nuint byte_length)
            => CallInterop(
                ref s_fields.napi_get_arraybuffer_info,
                env,
                arraybuffer.Handle,
                out data,
                out byte_length);

        internal static napi_status napi_is_typedarray(
            napi_env env, napi_value value, out c_bool result)
            => CallInterop(ref s_fields.napi_is_typedarray, env, value.Handle, out result);

        internal static napi_status napi_create_typedarray(
            napi_env env,
            napi_typedarray_type type,
            nuint length,
            napi_value arraybuffer,
            nuint byte_offset,
            out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_typedarray);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env,
                napi_typedarray_type,
                nuint,
                napi_value,
                nuint,
                nint,
                napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(
                    env,
                    type,
                    length,
                    arraybuffer,
                    byte_offset,
                    (nint)result_native);
            }
        }

        internal static napi_status napi_get_typedarray_info(
                napi_env env,
                napi_value typedarray,
                out napi_typedarray_type type,
                out nuint length,
                out nint data,
                out napi_value arraybuffer,
                out nuint byte_offset)
                => CallInterop(
                        ref s_fields.napi_get_typedarray_info,
                    env,
                    typedarray.Handle,
                    out type,
                    out length,
                    out data,
                    out arraybuffer,
                    out byte_offset);

        internal static napi_status napi_create_dataview(
            napi_env env,
            nuint length,
            napi_value arraybuffer,
            nuint byte_offset,
            out napi_value result)
            => CallInterop(
                ref s_fields.napi_create_dataview,
                env,
                (nint)length,
                arraybuffer.Handle,
                (nint)byte_offset,
                out result);

        internal static napi_status napi_is_dataview(
            napi_env env, napi_value value, out c_bool result)
            => CallInterop(ref s_fields.napi_is_dataview, env, value.Handle, out result);

        internal static napi_status napi_get_dataview_info(
            napi_env env,
            napi_value dataview,
            out nuint bytelength,
            out nint data,
            out napi_value arraybuffer,
            out nuint byte_offset)
            => CallInterop(
                ref s_fields.napi_get_dataview_info,
                env,
                dataview.Handle,
                out bytelength,
                out data,
                out arraybuffer,
                out byte_offset);

        internal static napi_status napi_get_version(napi_env env, out uint result)
            => CallInterop(ref s_fields.napi_get_version, env, out result);

        internal static napi_status napi_create_promise(
            napi_env env, out napi_deferred deferred, out napi_value promise)
            => CallInterop(
                ref s_fields.napi_create_promise, env, out deferred, out promise);

        internal static napi_status napi_resolve_deferred(
            napi_env env, napi_deferred deferred, napi_value resolution)
            => CallInterop(
                ref s_fields.napi_resolve_deferred,
                env,
                deferred.Handle,
                resolution.Handle);

        internal static napi_status napi_reject_deferred(
            napi_env env, napi_deferred deferred, napi_value rejection)
            => CallInterop(
                ref s_fields.napi_reject_deferred, env, deferred.Handle, rejection.Handle);

        internal static napi_status napi_is_promise(
            napi_env env, napi_value value, out c_bool is_promise)
            => CallInterop(ref s_fields.napi_is_promise, env, value.Handle, out is_promise);

        internal static napi_status napi_run_script(
            napi_env env, napi_value script, out napi_value result)
            => CallInterop(ref s_fields.napi_run_script, env, script.Handle, out result);

        internal static napi_status napi_adjust_external_memory(
            napi_env env, long change_in_bytes, out long adjusted_value)
        {
            nint funcHandle = GetExport(ref s_fields.napi_adjust_external_memory);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, long, nint, napi_status>)funcHandle;
            fixed (long* adjusted_value_native = &adjusted_value)
            {
                return funcDelegate(env, change_in_bytes, (nint)adjusted_value_native);
            }
        }

        internal static napi_status napi_create_date(
            napi_env env, double time, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_date);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, double, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, time, (nint)result_native);
            }
        }

        internal static napi_status napi_is_date(
            napi_env env, napi_value value, out c_bool is_date)
            => CallInterop(ref s_fields.napi_is_date, env, value.Handle, out is_date);

        internal static napi_status napi_get_date_value(
            napi_env env, napi_value value, out double result)
            => CallInterop(ref s_fields.napi_get_date_value, env, value.Handle, out result);

        internal static napi_status napi_add_finalizer(
            napi_env env,
            napi_value js_object,
            nint native_object,
            napi_finalize finalize_cb,
            nint finalize_hint,
            napi_ref* result)
            => CallInterop(
                ref s_fields.napi_add_finalizer,
                env,
                js_object.Handle,
                native_object,
                (nint)finalize_cb.Handle,
                finalize_hint,
                (nint)result);

        internal static napi_status napi_create_bigint_int64(
            napi_env env, long value, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_bigint_int64);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, long, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, value, (nint)result_native);
            }
        }


        internal static napi_status napi_create_bigint_uint64(
            napi_env env, ulong value, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_bigint_uint64);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, ulong, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, value, (nint)result_native);
            }
        }

        internal static napi_status napi_create_bigint_words(
            napi_env env, int sign_bit, nuint word_count, ulong* words, out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_create_bigint_words);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, int, nuint, nint, nint, napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(env, sign_bit, word_count, (nint)words, (nint)result_native);
            }
        }

        internal static napi_status napi_get_value_bigint_int64(
            napi_env env, napi_value value, out long result, out c_bool lossless)
            => CallInterop(
                ref s_fields.napi_get_value_bigint_int64,
                env,
                value.Handle,
                out result,
                out lossless);

        internal static napi_status napi_get_value_bigint_uint64(
            napi_env env, napi_value value, out ulong result, out c_bool lossless)
            => CallInterop(
                ref s_fields.napi_get_value_bigint_uint64,
                env,
                value.Handle,
                out result,
                out lossless);

        internal static napi_status napi_get_value_bigint_words(
            napi_env env, napi_value value, out int sign_bit, out nuint word_count, ulong* words)
            => CallInterop(
                ref s_fields.napi_get_value_bigint_words,
                env,
                value.Handle,
                out sign_bit,
                out word_count,
                (nint)words);

        internal static napi_status napi_get_all_property_names(
            napi_env env,
            napi_value js_object,
            napi_key_collection_mode key_mode,
            napi_key_filter key_filter,
            napi_key_conversion key_conversion,
            out napi_value result)
        {
            nint funcHandle = GetExport(ref s_fields.napi_get_all_property_names);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env,
                napi_value,
                napi_key_collection_mode,
                napi_key_filter,
                napi_key_conversion,
                nint,
                napi_status>)funcHandle;
            fixed (napi_value* result_native = &result)
            {
                return funcDelegate(
                    env,
                    js_object,
                    key_mode,
                    key_filter,
                    key_conversion,
                    (nint)result_native);
            }
        }

        internal static napi_status napi_set_instance_data(
            napi_env env, nint data, napi_finalize finalize_cb, nint finalize_hint)
        {
            nint funcHandle = GetExport(ref s_fields.napi_set_instance_data);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_finalize, nint, napi_status>)funcHandle;
            return funcDelegate(env, data, finalize_cb, finalize_hint);
        }

        internal static napi_status napi_get_instance_data(napi_env env, out nint data)
            => CallInterop(ref s_fields.napi_get_instance_data, env, out data);

        internal static napi_status napi_detach_arraybuffer(napi_env env, napi_value arraybuffer)
            => CallInterop(ref s_fields.napi_detach_arraybuffer, env, arraybuffer.Handle);

        internal static napi_status napi_is_detached_arraybuffer(
            napi_env env, napi_value value, out c_bool result)
            => CallInterop(
                ref s_fields.napi_is_detached_arraybuffer, env, value.Handle, out result);

        internal static napi_status napi_type_tag_object(
            napi_env env, napi_value value, in napi_type_tag type_tag)
        {
            fixed (napi_type_tag* type_tag_native = &type_tag)
            {
                return CallInterop(
                    ref s_fields.napi_type_tag_object,
                    env,
                    value.Handle,
                    (nint)type_tag_native);
            }
        }

        internal static napi_status napi_check_object_type_tag(
            napi_env env, napi_value value, in napi_type_tag type_tag, out c_bool result)
        {
            fixed (napi_type_tag* type_tag_native = &type_tag)
            fixed (c_bool* result_native = &result)
            {
                return CallInterop(
                    ref s_fields.napi_check_object_type_tag,
                    env,
                    value.Handle,
                    (nint)type_tag_native,
                    (nint)result_native);
            }
        }

        internal static napi_status napi_object_freeze(napi_env env, napi_value js_object)
            => CallInterop(ref s_fields.napi_object_freeze, env, js_object.Handle);

        internal static napi_status napi_object_seal(napi_env env, napi_value js_object)
            => CallInterop(ref s_fields.napi_object_seal, env, js_object.Handle);

        private static nint GetExport(ref nint field, [CallerMemberName] string functionName = "")
        {
            nint methodPtr = field;
            if (methodPtr == default)
            {
                methodPtr = NativeLibrary.GetExport(s_libraryHandle, functionName);
                field = methodPtr;
            }

            return methodPtr;
        }

        internal static bool TryGetExport(ref nint field, [CallerMemberName] string functionName = "")
        {
            nint methodPtr = field;
            if (methodPtr == default)
            {
                if (NativeLibrary.TryGetExport(s_libraryHandle, functionName, out methodPtr))
                {
                    field = methodPtr;
                    return true;
                }
                else
                {
                    return false;
                }

            }

            return true;
        }

        private static napi_status CallInterop<TResult>(
            ref nint field,
            napi_env env,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate(env, (nint)result_native);
            }
        }

        private static napi_status CallInterop<TResult1, TResult2>(
            ref nint field,
            napi_env env,
            out TResult1 result1,
            out TResult2 result2,
            [CallerMemberName] string functionName = "")
            where TResult1 : unmanaged
            where TResult2 : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, napi_status>)funcHandle;
            fixed (TResult1* result1_native = &result1)
            fixed (TResult2* result2_native = &result2)
            {
                return funcDelegate(env, (nint)result1_native, (nint)result2_native);
            }
        }

        private static napi_status CallInterop<TResult>(
            ref nint field,
            napi_env env,
            nint value,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, napi_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate(env, value, (nint)result_native);
            }
        }

        private static napi_status CallInterop<TResult1, TResult2>(
            ref nint field,
            napi_env env,
            nint value,
            out TResult1 result1,
            out TResult2 result2,
            [CallerMemberName] string functionName = "")
            where TResult1 : unmanaged
            where TResult2 : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, napi_status>)funcHandle;
            fixed (TResult1* result1_native = &result1)
            fixed (TResult2* result2_native = &result2)
            {
                return funcDelegate(env, value, (nint)result1_native, (nint)result2_native);
            }
        }

        private static napi_status CallInterop<TResult1, TResult2>(
            ref nint field,
            napi_env env,
            nint value,
            out TResult1 result1,
            out TResult2 result2,
            nint result3,
            [CallerMemberName] string functionName = "")
            where TResult1 : unmanaged
            where TResult2 : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, nint, napi_status>)funcHandle;
            fixed (TResult1* result1_native = &result1)
            fixed (TResult2* result2_native = &result2)
            {
                return funcDelegate(
                    env, value, (nint)result1_native, (nint)result2_native, result3);
            }
        }

        private static napi_status CallInterop<TResult1, TResult2, TResult3, TResult4>(
            ref nint field,
            napi_env env,
            nint value,
            out TResult1 result1,
            out TResult2 result2,
            out TResult3 result3,
            out TResult4 result4,
            [CallerMemberName] string functionName = "")
            where TResult1 : unmanaged
            where TResult2 : unmanaged
            where TResult3 : unmanaged
            where TResult4 : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, nint, nint, napi_status>)funcHandle;
            fixed (TResult1* result1_native = &result1)
            fixed (TResult2* result2_native = &result2)
            fixed (TResult3* result3_native = &result3)
            fixed (TResult4* result4_native = &result4)
            {
                return funcDelegate(
                    env,
                    value,
                    (nint)result1_native,
                    (nint)result2_native,
                    (nint)result3_native,
                    (nint)result4_native);
            }
        }

        private static napi_status CallInterop<
            TResult1, TResult2, TResult3, TResult4, TResult5>(
            ref nint field,
            napi_env env,
            nint value,
            out TResult1 result1,
            out TResult2 result2,
            out TResult3 result3,
            out TResult4 result4,
            out TResult5 result5,
            [CallerMemberName] string functionName = "")
            where TResult1 : unmanaged
            where TResult2 : unmanaged
            where TResult3 : unmanaged
            where TResult4 : unmanaged
            where TResult5 : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env,
                nint,
                nint,
                nint,
                nint,
                nint,
                nint,
                napi_status>)funcHandle;
            fixed (TResult1* result1_native = &result1)
            fixed (TResult2* result2_native = &result2)
            fixed (TResult3* result3_native = &result3)
            fixed (TResult4* result4_native = &result4)
            fixed (TResult5* result5_native = &result5)
            {
                return funcDelegate(
                    env,
                    value,
                    (nint)result1_native,
                    (nint)result2_native,
                    (nint)result3_native,
                    (nint)result4_native,
                    (nint)result5_native);
            }
        }

        private static napi_status CallInterop<TResult>(
            ref nint field,
            napi_env env,
            nint value1,
            nint value2,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, napi_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate(env, value1, value2, (nint)result_native);
            }
        }

        private static napi_status CallInterop<TResult>(
            ref nint field,
            napi_env env,
            nint value1,
            uint value2,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, uint, nint, napi_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate(env, value1, value2, (nint)result_native);
            }
        }
        private static napi_status CallInterop(
            ref nint field,
            napi_env env,
            nint value1,
            nint value2,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, napi_status>)funcHandle;
            return funcDelegate(env, value1, value2);
        }

        private static napi_status CallInterop(
            ref nint field,
            napi_env env,
            nint value1,
            nint value2,
            nint value3,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, napi_status>)funcHandle;
            return funcDelegate(env, value1, value2, value3);
        }

        private static napi_status CallInterop<TResult>(
            ref nint field,
            napi_env env,
            nint value1,
            nint value2,
            nint value3,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, nint, napi_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate(env, value1, value2, value3, (nint)result_native);
            }
        }

        private static napi_status CallInterop<TResult>(
            ref nint field,
            napi_env env,
            nint value1,
            nint value2,
            nint value3,
            nint value4,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, nint, nint, napi_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate(env, value1, value2, value3, value4, (nint)result_native);
            }
        }

        private static napi_status CallInterop(
            ref nint field,
            napi_env env,
            nint value1,
            nint value2,
            nint value3,
            nint value4,
            nint value5,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, nint, nint, napi_status>)funcHandle;
            return funcDelegate(env, value1, value2, value3, value4, value5);
        }

        private static napi_status CallInterop<TResult>(
            ref nint field,
            napi_env env,
            nint value1,
            nint value2,
            nint value3,
            nint value4,
            nint value5,
            out TResult result,
            [CallerMemberName] string functionName = "")
            where TResult : unmanaged
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, nint, nint, nint, nint, napi_status>)funcHandle;
            fixed (TResult* result_native = &result)
            {
                return funcDelegate(
                    env, value1, value2, value3, value4, value5, (nint)result_native);
            }
        }

        private static napi_status CallInterop(
            ref nint field,
            napi_env env,
            nint value,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)funcHandle;
            return funcDelegate(env, value);
        }

        private static napi_status CallInterop(
            ref nint field,
            napi_env env,
            string? value1,
            string? value2,
            [CallerMemberName] string functionName = "")
        {
            nint funcHandle = GetExport(ref field, functionName);
            var funcDelegate = (delegate* unmanaged[Cdecl]<
                napi_env, nint, nint, napi_status>)funcHandle;

            nint value1_native = value1 == null ? default : StringToHGlobalUtf8(value1);
            nint value2_native = value2 == null ? default : StringToHGlobalUtf8(value2);
            try
            {
                return funcDelegate(env, value1_native, value2_native);
            }
            finally
            {
                if (value1_native != default) Marshal.FreeHGlobal(value1_native);
                if (value2_native != default) Marshal.FreeHGlobal(value2_native);
            }
        }

        internal static nint StringToHGlobalUtf8(string s)
        {
            if (s == null) return default;
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            nint ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            return ptr;
        }
    }
}
