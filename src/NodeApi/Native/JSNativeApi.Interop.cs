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
        private static FunctionPtrs s_funcs = new();
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

        // Pointers to the imported native functions.
        // We initialize them all initially to the DelayLoadStubs functions.
        // Each DelayLoadStubs function acquires the targeting native function on the first call
        // and replaces the native pointer with the actual function pointer.
        private struct FunctionPtrs
        {
            //--------------------------------------------------------------------------------------
            // js_native_api.h APIs (sorted alphabetically)
            //--------------------------------------------------------------------------------------

            public delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_finalize, nint, nint, napi_status>
                napi_add_finalizer;
            public delegate* unmanaged[Cdecl]<napi_env, long, nint, napi_status>
                napi_adjust_external_memory;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, nuint, nint, nint, napi_status>
                napi_call_function;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
                napi_check_object_type_tag;
            public delegate* unmanaged[Cdecl]<napi_env, napi_escapable_handle_scope, napi_status>
                napi_close_escapable_handle_scope;
            public delegate* unmanaged[Cdecl]<napi_env, napi_handle_scope, napi_status>
                napi_close_handle_scope;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_coerce_to_bool;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_coerce_to_number;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_coerce_to_object;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_coerce_to_string;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_create_array;
            public delegate* unmanaged[Cdecl]<napi_env, nuint, nint, napi_status>
                napi_create_array_with_length;
            public delegate* unmanaged[Cdecl]<napi_env, nuint, nint, nint, napi_status>
                napi_create_arraybuffer;
            public delegate* unmanaged[Cdecl]<napi_env, long, nint, napi_status>
                napi_create_bigint_int64;
            public delegate* unmanaged[Cdecl]<napi_env, ulong, nint, napi_status>
                napi_create_bigint_uint64;
            public delegate* unmanaged[Cdecl]<napi_env, int, nuint, nint, nint, napi_status>
                napi_create_bigint_words;
            public delegate* unmanaged[Cdecl]<
                napi_env, nuint, napi_value, nuint, nint, napi_status>
                napi_create_dataview;
            public delegate* unmanaged[Cdecl]<napi_env, double, nint, napi_status>
                napi_create_date;
            public delegate* unmanaged[Cdecl]<napi_env, double, nint, napi_status>
                napi_create_double;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_create_error;
            public delegate* unmanaged[Cdecl]<
                napi_env, nint, napi_finalize, nint, nint, napi_status>
                napi_create_external;
            public delegate* unmanaged[Cdecl]<
                napi_env, nint, nuint, napi_finalize, nint, nint, napi_status>
                napi_create_external_arraybuffer;
            public delegate* unmanaged[Cdecl]<
                napi_env, nint, nuint, napi_callback, nint, nint, napi_status>
                napi_create_function;
            public delegate* unmanaged[Cdecl]<napi_env, int, nint, napi_status>
                napi_create_int32;
            public delegate* unmanaged[Cdecl]<napi_env, long, nint, napi_status>
                napi_create_int64;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_create_object;
            public delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>
                napi_create_promise;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_create_range_error;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, nint, napi_status>
                napi_create_reference;
            public delegate* unmanaged[Cdecl]<napi_env, nint, nuint, nint, napi_status>
                napi_create_string_latin1;
            public delegate* unmanaged[Cdecl]<napi_env, nint, nuint, nint, napi_status>
                napi_create_string_utf16;
            public delegate* unmanaged[Cdecl]<napi_env, nint, nuint, nint, napi_status>
                napi_create_string_utf8;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_create_symbol;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_create_type_error;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_typedarray_type, nuint, napi_value, nuint, nint, napi_status>
                napi_create_typedarray;
            public delegate* unmanaged[Cdecl]<napi_env, uint, nint, napi_status>
                napi_create_uint32;
            public delegate* unmanaged[Cdecl]<
                napi_env, nint, nuint, napi_callback, nint, nuint, nint, nint, napi_status>
                napi_define_class;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nuint, nint, napi_status>
                napi_define_properties;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, nint, napi_status>
                napi_delete_element;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_delete_property;
            public delegate* unmanaged[Cdecl]<napi_env, napi_ref, napi_status>
                napi_delete_reference;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
                napi_detach_arraybuffer;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_escapable_handle_scope, napi_value, nint, napi_status>
                napi_escape_handle;
            public delegate* unmanaged[Cdecl]<
                napi_env,
                napi_value,
                napi_key_collection_mode,
                napi_key_filter,
                napi_key_conversion,
                nint,
                napi_status>
                napi_get_all_property_names;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_get_and_clear_last_exception;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_array_length;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
                napi_get_arraybuffer_info;
            public delegate* unmanaged[Cdecl]<napi_env, c_bool, nint, napi_status>
                napi_get_boolean;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_callback_info, nint, nint, nint, nint, napi_status>
                napi_get_cb_info;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, nint, nint, nint, napi_status>
                napi_get_dataview_info;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_date_value;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, nint, napi_status>
                napi_get_element;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_get_global;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_get_instance_data;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_get_last_error_info;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
                napi_get_named_property;
            public delegate* unmanaged[Cdecl]<napi_env, napi_callback_info, nint, napi_status>
                napi_get_new_target;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_get_null;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_get_property;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_property_names;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_prototype;
            public delegate* unmanaged[Cdecl]<napi_env, napi_ref, nint, napi_status>
                napi_get_reference_value;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, nint, nint, nint, nint, napi_status>
                napi_get_typedarray_info;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_get_undefined;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
                napi_get_value_bigint_int64;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
                napi_get_value_bigint_uint64;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, nint, napi_status>
                napi_get_value_bigint_words;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_value_bool;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_value_double;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_value_external;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_value_int32;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_value_int64;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nuint, nint, napi_status>
                napi_get_value_string_latin1;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nuint, nint, napi_status>
                napi_get_value_string_utf16;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nuint, nint, napi_status>
                napi_get_value_string_utf8;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_get_value_uint32;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_get_version;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, nint, napi_status>
                napi_has_element;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
                napi_has_named_property;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_has_own_property;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_has_property;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_instanceof;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_is_array;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_is_arraybuffer;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_is_dataview;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_is_date;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_is_detached_arraybuffer;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_is_error;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_is_exception_pending;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_is_promise;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_is_typedarray;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nuint, nint, nint, napi_status>
                napi_new_instance;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
                napi_object_freeze;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
                napi_object_seal;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_open_escapable_handle_scope;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_open_handle_scope;
            public delegate* unmanaged[Cdecl]<napi_env, napi_ref, nint, napi_status>
                napi_reference_ref;
            public delegate* unmanaged[Cdecl]<napi_env, napi_ref, nint, napi_status>
                napi_reference_unref;
            public delegate* unmanaged[Cdecl]<napi_env, napi_deferred, napi_value, napi_status>
                napi_reject_deferred;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_remove_wrap;
            public delegate* unmanaged[Cdecl]<napi_env, napi_deferred, napi_value, napi_status>
                napi_resolve_deferred;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_run_script;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, uint, napi_value, napi_status>
                napi_set_element;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_finalize, nint, napi_status>
                napi_set_instance_data;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_value, napi_status>
                napi_set_named_property;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_value, napi_value, napi_status>
                napi_set_property;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_strict_equals;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
                napi_throw;
            public delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>
                napi_throw_error;
            public delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>
                napi_throw_range_error;
            public delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>
                napi_throw_type_error;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_type_tag_object;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_typeof;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_unwrap;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_value, nint, napi_finalize, nint, nint, napi_status>
                napi_wrap;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                node_api_create_syntax_error;
            public delegate* unmanaged[Cdecl]<napi_env, nint, nuint, nint, napi_status>
                node_api_symbol_for;
            public delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>
                node_api_throw_syntax_error;

            //--------------------------------------------------------------------------------------
            // node_api.h APIs (sorted alphabetically)
            //--------------------------------------------------------------------------------------

            public delegate* unmanaged[Cdecl]<napi_threadsafe_function, napi_status>
                napi_acquire_threadsafe_function;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_async_cleanup_hook, nint, nint, napi_status>
                napi_add_async_cleanup_hook;
            public delegate* unmanaged[Cdecl]<napi_env, napi_cleanup_hook, nint, napi_status>
                napi_add_env_cleanup_hook;
            public delegate* unmanaged[Cdecl]<napi_env, napi_async_context, napi_status>
                napi_async_destroy;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_value, nint, napi_status>
                napi_async_init;
            public delegate* unmanaged[Cdecl]<
                napi_threadsafe_function, nint, napi_threadsafe_function_call_mode, napi_status>
                napi_call_threadsafe_function;
            public delegate* unmanaged[Cdecl]<napi_env, napi_async_work, napi_status>
                napi_cancel_async_work;
            public delegate* unmanaged[Cdecl]<napi_env, napi_callback_scope, napi_status>
                napi_close_callback_scope;
            public delegate* unmanaged[Cdecl]<
                napi_env,
                napi_value,
                napi_value,
                napi_async_execute_callback,
                napi_async_complete_callback,
                nint,
                nint,
                napi_status>
                napi_create_async_work;
            public delegate* unmanaged[Cdecl]<napi_env, nuint, nint, nint, napi_status>
                napi_create_buffer;
            public delegate* unmanaged[Cdecl]<napi_env, nuint, nint, nint, nint, napi_status>
                napi_create_buffer_copy;
            public delegate* unmanaged[Cdecl]<
                napi_env, nuint, nint, napi_finalize, nint, nint, napi_status>
                napi_create_external_buffer;
            public delegate* unmanaged[Cdecl]<
                napi_env,
                napi_value,
                napi_value,
                napi_value,
                nuint,
                nuint,
                nint,
                napi_finalize,
                nint,
                napi_threadsafe_function_call_js,
                nint,
                napi_status>
                napi_create_threadsafe_function;
            public delegate* unmanaged[Cdecl]<napi_env, napi_async_work, napi_status>
                napi_delete_async_work;
            public delegate* unmanaged[Cdecl]<nint, nuint, nint, nuint, void>
                napi_fatal_error;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>
                napi_fatal_exception;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, nint, napi_status>
                napi_get_buffer_info;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_get_node_version;
            public delegate* unmanaged[Cdecl]<napi_threadsafe_function, nint, napi_status>
                napi_get_threadsafe_function_context;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_get_uv_event_loop;
            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_is_buffer;
            public delegate* unmanaged[Cdecl]<
                napi_env,
                napi_async_context,
                napi_value,
                napi_value,
                nuint,
                nint,
                nint,
                napi_status>
                napi_make_callback;
            public delegate* unmanaged[Cdecl]<nint, void>
                napi_module_register;
            public delegate* unmanaged[Cdecl]<
                napi_env, napi_value, napi_async_context, nint, napi_status>
                napi_open_callback_scope;
            public delegate* unmanaged[Cdecl]<napi_env, napi_async_work, napi_status>
                napi_queue_async_work;
            public delegate* unmanaged[Cdecl]<napi_env, napi_threadsafe_function, napi_status>
                napi_ref_threadsafe_function;
            public delegate* unmanaged[Cdecl]<
                napi_threadsafe_function, napi_threadsafe_function_release_mode, napi_status>
                napi_release_threadsafe_function;
            public delegate* unmanaged[Cdecl]<napi_async_cleanup_hook_handle, napi_status>
                napi_remove_async_cleanup_hook;
            public delegate* unmanaged[Cdecl]<napi_env, napi_cleanup_hook, nint, napi_status>
                napi_remove_env_cleanup_hook;
            public delegate* unmanaged[Cdecl]<napi_env, napi_threadsafe_function, napi_status>
                napi_unref_threadsafe_function;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                node_api_get_module_file_name;

            //--------------------------------------------------------------------------------------
            // Embedding APIs
            //--------------------------------------------------------------------------------------

            public delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
                napi_await_promise;
            public delegate* unmanaged[Cdecl]<
                napi_platform, napi_error_message_handler, nint, nint, napi_status>
                napi_create_environment;
            public delegate* unmanaged[Cdecl]<
                int, nint, int, nint, napi_error_message_handler, nint, napi_status>
                napi_create_platform;
            public delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
                napi_destroy_environment;
            public delegate* unmanaged[Cdecl]<napi_platform, napi_status>
                napi_destroy_platform;
            public delegate* unmanaged[Cdecl]<napi_env, napi_status>
                napi_run_environment;

            public FunctionPtrs()
            {
#if NET6_0_OR_GREATER
                //----------------------------------------------------------------------------------
                // js_native_api.h APIs
                //----------------------------------------------------------------------------------

                napi_add_finalizer = &DelayLoadStubs.napi_add_finalizer;
                napi_adjust_external_memory = &DelayLoadStubs.napi_adjust_external_memory;
                napi_call_function = &DelayLoadStubs.napi_call_function;
                napi_check_object_type_tag = &DelayLoadStubs.napi_check_object_type_tag;
                napi_close_escapable_handle_scope =
                    &DelayLoadStubs.napi_close_escapable_handle_scope;
                napi_close_handle_scope = &DelayLoadStubs.napi_close_handle_scope;
                napi_coerce_to_bool = &DelayLoadStubs.napi_coerce_to_bool;
                napi_coerce_to_number = &DelayLoadStubs.napi_coerce_to_number;
                napi_coerce_to_object = &DelayLoadStubs.napi_coerce_to_object;
                napi_coerce_to_string = &DelayLoadStubs.napi_coerce_to_string;
                napi_create_array = &DelayLoadStubs.napi_create_array;
                napi_create_array_with_length = &DelayLoadStubs.napi_create_array_with_length;
                napi_create_arraybuffer = &DelayLoadStubs.napi_create_arraybuffer;
                napi_create_bigint_int64 = &DelayLoadStubs.napi_create_bigint_int64;
                napi_create_bigint_uint64 = &DelayLoadStubs.napi_create_bigint_uint64;
                napi_create_bigint_words = &DelayLoadStubs.napi_create_bigint_words;
                napi_create_dataview = &DelayLoadStubs.napi_create_dataview;
                napi_create_date = &DelayLoadStubs.napi_create_date;
                napi_create_double = &DelayLoadStubs.napi_create_double;
                napi_create_error = &DelayLoadStubs.napi_create_error;
                napi_create_external = &DelayLoadStubs.napi_create_external;
                napi_create_external_arraybuffer = &DelayLoadStubs.napi_create_external_arraybuffer;
                napi_create_function = &DelayLoadStubs.napi_create_function;
                napi_create_int32 = &DelayLoadStubs.napi_create_int32;
                napi_create_int64 = &DelayLoadStubs.napi_create_int64;
                napi_create_object = &DelayLoadStubs.napi_create_object;
                napi_create_promise = &DelayLoadStubs.napi_create_promise;
                napi_create_range_error = &DelayLoadStubs.napi_create_range_error;
                napi_create_reference = &DelayLoadStubs.napi_create_reference;
                napi_create_string_latin1 = &DelayLoadStubs.napi_create_string_latin1;
                napi_create_string_utf16 = &DelayLoadStubs.napi_create_string_utf16;
                napi_create_string_utf8 = &DelayLoadStubs.napi_create_string_utf8;
                napi_create_symbol = &DelayLoadStubs.napi_create_symbol;
                napi_create_type_error = &DelayLoadStubs.napi_create_type_error;
                napi_create_typedarray = &DelayLoadStubs.napi_create_typedarray;
                napi_create_uint32 = &DelayLoadStubs.napi_create_uint32;
                napi_define_class = &DelayLoadStubs.napi_define_class;
                napi_define_properties = &DelayLoadStubs.napi_define_properties;
                napi_delete_element = &DelayLoadStubs.napi_delete_element;
                napi_delete_property = &DelayLoadStubs.napi_delete_property;
                napi_delete_reference = &DelayLoadStubs.napi_delete_reference;
                napi_detach_arraybuffer = &DelayLoadStubs.napi_detach_arraybuffer;
                napi_escape_handle = &DelayLoadStubs.napi_escape_handle;
                napi_get_all_property_names = &DelayLoadStubs.napi_get_all_property_names;
                napi_get_and_clear_last_exception =
                    &DelayLoadStubs.napi_get_and_clear_last_exception;
                napi_get_array_length = &DelayLoadStubs.napi_get_array_length;
                napi_get_arraybuffer_info = &DelayLoadStubs.napi_get_arraybuffer_info;
                napi_get_boolean = &DelayLoadStubs.napi_get_boolean;
                napi_get_cb_info = &DelayLoadStubs.napi_get_cb_info;
                napi_get_dataview_info = &DelayLoadStubs.napi_get_dataview_info;
                napi_get_date_value = &DelayLoadStubs.napi_get_date_value;
                napi_get_element = &DelayLoadStubs.napi_get_element;
                napi_get_global = &DelayLoadStubs.napi_get_global;
                napi_get_instance_data = &DelayLoadStubs.napi_get_instance_data;
                napi_get_last_error_info = &DelayLoadStubs.napi_get_last_error_info;
                napi_get_named_property = &DelayLoadStubs.napi_get_named_property;
                napi_get_new_target = &DelayLoadStubs.napi_get_new_target;
                napi_get_null = &DelayLoadStubs.napi_get_null;
                napi_get_property = &DelayLoadStubs.napi_get_property;
                napi_get_property_names = &DelayLoadStubs.napi_get_property_names;
                napi_get_prototype = &DelayLoadStubs.napi_get_prototype;
                napi_get_reference_value = &DelayLoadStubs.napi_get_reference_value;
                napi_get_typedarray_info = &DelayLoadStubs.napi_get_typedarray_info;
                napi_get_undefined = &DelayLoadStubs.napi_get_undefined;
                napi_get_value_bigint_int64 = &DelayLoadStubs.napi_get_value_bigint_int64;
                napi_get_value_bigint_uint64 = &DelayLoadStubs.napi_get_value_bigint_uint64;
                napi_get_value_bigint_words = &DelayLoadStubs.napi_get_value_bigint_words;
                napi_get_value_bool = &DelayLoadStubs.napi_get_value_bool;
                napi_get_value_double = &DelayLoadStubs.napi_get_value_double;
                napi_get_value_external = &DelayLoadStubs.napi_get_value_external;
                napi_get_value_int32 = &DelayLoadStubs.napi_get_value_int32;
                napi_get_value_int64 = &DelayLoadStubs.napi_get_value_int64;
                napi_get_value_string_latin1 = &DelayLoadStubs.napi_get_value_string_latin1;
                napi_get_value_string_utf16 = &DelayLoadStubs.napi_get_value_string_utf16;
                napi_get_value_string_utf8 = &DelayLoadStubs.napi_get_value_string_utf8;
                napi_get_value_uint32 = &DelayLoadStubs.napi_get_value_uint32;
                napi_get_version = &DelayLoadStubs.napi_get_version;
                napi_has_element = &DelayLoadStubs.napi_has_element;
                napi_has_named_property = &DelayLoadStubs.napi_has_named_property;
                napi_has_own_property = &DelayLoadStubs.napi_has_own_property;
                napi_has_property = &DelayLoadStubs.napi_has_property;
                napi_instanceof = &DelayLoadStubs.napi_instanceof;
                napi_is_array = &DelayLoadStubs.napi_is_array;
                napi_is_arraybuffer = &DelayLoadStubs.napi_is_arraybuffer;
                napi_is_dataview = &DelayLoadStubs.napi_is_dataview;
                napi_is_date = &DelayLoadStubs.napi_is_date;
                napi_is_detached_arraybuffer = &DelayLoadStubs.napi_is_detached_arraybuffer;
                napi_is_error = &DelayLoadStubs.napi_is_error;
                napi_is_exception_pending = &DelayLoadStubs.napi_is_exception_pending;
                napi_is_promise = &DelayLoadStubs.napi_is_promise;
                napi_is_typedarray = &DelayLoadStubs.napi_is_typedarray;
                napi_new_instance = &DelayLoadStubs.napi_new_instance;
                napi_object_freeze = &DelayLoadStubs.napi_object_freeze;
                napi_object_seal = &DelayLoadStubs.napi_object_seal;
                napi_open_escapable_handle_scope = &DelayLoadStubs.napi_open_escapable_handle_scope;
                napi_open_handle_scope = &DelayLoadStubs.napi_open_handle_scope;
                napi_reference_ref = &DelayLoadStubs.napi_reference_ref;
                napi_reference_unref = &DelayLoadStubs.napi_reference_unref;
                napi_reject_deferred = &DelayLoadStubs.napi_reject_deferred;
                napi_remove_wrap = &DelayLoadStubs.napi_remove_wrap;
                napi_resolve_deferred = &DelayLoadStubs.napi_resolve_deferred;
                napi_run_script = &DelayLoadStubs.napi_run_script;
                napi_set_element = &DelayLoadStubs.napi_set_element;
                napi_set_instance_data = &DelayLoadStubs.napi_set_instance_data;
                napi_set_named_property = &DelayLoadStubs.napi_set_named_property;
                napi_set_property = &DelayLoadStubs.napi_set_property;
                napi_strict_equals = &DelayLoadStubs.napi_strict_equals;
                napi_throw = &DelayLoadStubs.napi_throw;
                napi_throw_error = &DelayLoadStubs.napi_throw_error;
                napi_throw_range_error = &DelayLoadStubs.napi_throw_range_error;
                napi_throw_type_error = &DelayLoadStubs.napi_throw_type_error;
                napi_type_tag_object = &DelayLoadStubs.napi_type_tag_object;
                napi_typeof = &DelayLoadStubs.napi_typeof;
                napi_unwrap = &DelayLoadStubs.napi_unwrap;
                napi_wrap = &DelayLoadStubs.napi_wrap;
                node_api_create_syntax_error = &DelayLoadStubs.node_api_create_syntax_error;
                node_api_symbol_for = &DelayLoadStubs.node_api_symbol_for;
                node_api_throw_syntax_error = &DelayLoadStubs.node_api_throw_syntax_error;

                //----------------------------------------------------------------------------------
                // node_api.h APIs
                //----------------------------------------------------------------------------------

                napi_acquire_threadsafe_function = &DelayLoadStubs.napi_acquire_threadsafe_function;
                napi_add_async_cleanup_hook = &DelayLoadStubs.napi_add_async_cleanup_hook;
                napi_add_env_cleanup_hook = &DelayLoadStubs.napi_add_env_cleanup_hook;
                napi_async_destroy = &DelayLoadStubs.napi_async_destroy;
                napi_async_init = &DelayLoadStubs.napi_async_init;
                napi_call_threadsafe_function = &DelayLoadStubs.napi_call_threadsafe_function;
                napi_cancel_async_work = &DelayLoadStubs.napi_cancel_async_work;
                napi_close_callback_scope = &DelayLoadStubs.napi_close_callback_scope;
                napi_create_async_work = &DelayLoadStubs.napi_create_async_work;
                napi_create_buffer = &DelayLoadStubs.napi_create_buffer;
                napi_create_buffer_copy = &DelayLoadStubs.napi_create_buffer_copy;
                napi_create_external_buffer = &DelayLoadStubs.napi_create_external_buffer;
                napi_create_threadsafe_function = &DelayLoadStubs.napi_create_threadsafe_function;
                napi_delete_async_work = &DelayLoadStubs.napi_delete_async_work;
                napi_fatal_error = &DelayLoadStubs.napi_fatal_error;
                napi_fatal_exception = &DelayLoadStubs.napi_fatal_exception;
                napi_get_buffer_info = &DelayLoadStubs.napi_get_buffer_info;
                napi_get_node_version = &DelayLoadStubs.napi_get_node_version;
                napi_get_threadsafe_function_context =
                    &DelayLoadStubs.napi_get_threadsafe_function_context;
                napi_get_uv_event_loop = &DelayLoadStubs.napi_get_uv_event_loop;
                napi_is_buffer = &DelayLoadStubs.napi_is_buffer;
                napi_make_callback = &DelayLoadStubs.napi_make_callback;
                napi_module_register = &DelayLoadStubs.napi_module_register;
                napi_open_callback_scope = &DelayLoadStubs.napi_open_callback_scope;
                napi_queue_async_work = &DelayLoadStubs.napi_queue_async_work;
                napi_ref_threadsafe_function = &DelayLoadStubs.napi_ref_threadsafe_function;
                napi_release_threadsafe_function = &DelayLoadStubs.napi_release_threadsafe_function;
                napi_remove_async_cleanup_hook = &DelayLoadStubs.napi_remove_async_cleanup_hook;
                napi_remove_env_cleanup_hook = &DelayLoadStubs.napi_remove_env_cleanup_hook;
                napi_unref_threadsafe_function = &DelayLoadStubs.napi_unref_threadsafe_function;
                node_api_get_module_file_name = &DelayLoadStubs.node_api_get_module_file_name;

                //----------------------------------------------------------------------------------
                // Embedding APIs
                //----------------------------------------------------------------------------------

                napi_await_promise = &DelayLoadStubs.napi_await_promise;
                napi_create_environment = &DelayLoadStubs.napi_create_environment;
                napi_create_platform = &DelayLoadStubs.napi_create_platform;
                napi_destroy_environment = &DelayLoadStubs.napi_destroy_environment;
                napi_destroy_platform = &DelayLoadStubs.napi_destroy_platform;
                napi_run_environment = &DelayLoadStubs.napi_run_environment;
#else
                //----------------------------------------------------------------------------------
                // js_native_api.h APIs
                //----------------------------------------------------------------------------------

                napi_add_finalizer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_finalize, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_add_finalizer>(
                    DelayLoadStubs.napi_add_finalizer);
                napi_adjust_external_memory = (delegate* unmanaged[Cdecl]<
                    napi_env, long, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_adjust_external_memory>(
                    DelayLoadStubs.napi_adjust_external_memory);
                napi_call_function = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nuint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_call_function>(
                    DelayLoadStubs.napi_call_function);
                napi_check_object_type_tag = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_check_object_type_tag>(
                    DelayLoadStubs.napi_check_object_type_tag);
                napi_close_escapable_handle_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_escapable_handle_scope, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_close_escapable_handle_scope>(
                    DelayLoadStubs.napi_close_escapable_handle_scope);
                napi_close_handle_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_handle_scope, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_close_handle_scope>(
                    DelayLoadStubs.napi_close_handle_scope);
                napi_coerce_to_bool = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_coerce_to_bool>(
                    DelayLoadStubs.napi_coerce_to_bool);
                napi_coerce_to_number = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_coerce_to_number>(
                    DelayLoadStubs.napi_coerce_to_number);
                napi_coerce_to_object = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_coerce_to_object>(
                    DelayLoadStubs.napi_coerce_to_object);
                napi_coerce_to_string = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_coerce_to_string>(
                    DelayLoadStubs.napi_coerce_to_string);
                napi_create_array = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_array>(
                    DelayLoadStubs.napi_create_array);
                napi_create_array_with_length = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_create_array_with_length>(
                    DelayLoadStubs.napi_create_array_with_length);
                napi_create_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_arraybuffer>(
                    DelayLoadStubs.napi_create_arraybuffer);
                napi_create_bigint_int64 = (delegate* unmanaged[Cdecl]<
                    napi_env, long, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_bigint_int64>(
                    DelayLoadStubs.napi_create_bigint_int64);
                napi_create_bigint_uint64 = (delegate* unmanaged[Cdecl]<
                    napi_env, ulong, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_bigint_uint64>(
                    DelayLoadStubs.napi_create_bigint_uint64);
                napi_create_bigint_words = (delegate* unmanaged[Cdecl]<
                    napi_env, int, nuint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_bigint_words>(
                    DelayLoadStubs.napi_create_bigint_words);
                napi_create_dataview = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, napi_value, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_dataview>(
                    DelayLoadStubs.napi_create_dataview);
                napi_create_date = (delegate* unmanaged[Cdecl]<napi_env, double, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_date>(
                    DelayLoadStubs.napi_create_date);
                napi_create_double = (delegate* unmanaged[Cdecl]<
                    napi_env, double, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_double>(
                    DelayLoadStubs.napi_create_double);
                napi_create_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_error>(
                    DelayLoadStubs.napi_create_error);
                napi_create_external = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_finalize, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_external>(
                    DelayLoadStubs.napi_create_external);
                napi_create_external_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, napi_finalize, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_create_external_arraybuffer>(
                    DelayLoadStubs.napi_create_external_arraybuffer);
                napi_create_function = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, napi_callback, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_function>(
                    DelayLoadStubs.napi_create_function);
                napi_create_int32 = (delegate* unmanaged[Cdecl]<napi_env, int, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_int32>(
                    DelayLoadStubs.napi_create_int32);
                napi_create_int64 = (delegate* unmanaged[Cdecl]<napi_env, long, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_int64>(
                    DelayLoadStubs.napi_create_int64);
                napi_create_object = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_object>(
                    DelayLoadStubs.napi_create_object);
                napi_create_promise = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_promise>(
                    DelayLoadStubs.napi_create_promise);
                napi_create_range_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_range_error>(
                    DelayLoadStubs.napi_create_range_error);
                napi_create_reference = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_reference>(
                    DelayLoadStubs.napi_create_reference);
                napi_create_string_latin1 = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_string_latin1>(
                    DelayLoadStubs.napi_create_string_latin1);
                napi_create_string_utf16 = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_string_utf16>(
                    DelayLoadStubs.napi_create_string_utf16);
                napi_create_string_utf8 = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_string_utf8>(
                    DelayLoadStubs.napi_create_string_utf8);
                napi_create_symbol = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_symbol>(
                    DelayLoadStubs.napi_create_symbol);
                napi_create_type_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_type_error>(
                    DelayLoadStubs.napi_create_type_error);
                napi_create_typedarray = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_typedarray_type, nuint, napi_value, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_typedarray>(
                    DelayLoadStubs.napi_create_typedarray);
                napi_create_uint32 = (delegate* unmanaged[Cdecl]<napi_env, uint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_uint32>(
                    DelayLoadStubs.napi_create_uint32);
                napi_define_class = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, napi_callback, nint, nuint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_define_class>(
                    DelayLoadStubs.napi_define_class);
                napi_define_properties = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_define_properties>(
                    DelayLoadStubs.napi_define_properties);
                napi_delete_element = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_delete_element>(
                    DelayLoadStubs.napi_delete_element);
                napi_delete_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_delete_property>(
                    DelayLoadStubs.napi_delete_property);
                napi_delete_reference = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_ref, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_delete_reference>(
                    DelayLoadStubs.napi_delete_reference);
                napi_detach_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_detach_arraybuffer>(
                    DelayLoadStubs.napi_detach_arraybuffer);
                napi_escape_handle = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_escapable_handle_scope, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_escape_handle>(
                    DelayLoadStubs.napi_escape_handle);
                napi_get_all_property_names = (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_value,
                    napi_key_collection_mode,
                    napi_key_filter,
                    napi_key_conversion,
                    nint,
                    napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_get_all_property_names>(
                    DelayLoadStubs.napi_get_all_property_names);
                napi_get_and_clear_last_exception = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_get_and_clear_last_exception>(
                    DelayLoadStubs.napi_get_and_clear_last_exception);
                napi_get_array_length = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_array_length>(
                    DelayLoadStubs.napi_get_array_length);
                napi_get_arraybuffer_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_arraybuffer_info>(
                    DelayLoadStubs.napi_get_arraybuffer_info);
                napi_get_boolean = (delegate* unmanaged[Cdecl]<napi_env, c_bool, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_boolean>(
                    DelayLoadStubs.napi_get_boolean);
                napi_get_cb_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_callback_info, nint, nint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_cb_info>(
                    DelayLoadStubs.napi_get_cb_info);
                napi_get_dataview_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_dataview_info>(
                    DelayLoadStubs.napi_get_dataview_info);
                napi_get_date_value = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_date_value>(
                    DelayLoadStubs.napi_get_date_value);
                napi_get_element = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_element>(
                    DelayLoadStubs.napi_get_element);
                napi_get_global = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_global>(
                    DelayLoadStubs.napi_get_global);
                napi_get_instance_data = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_instance_data>(
                    DelayLoadStubs.napi_get_instance_data);
                napi_get_last_error_info = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_last_error_info>(
                    DelayLoadStubs.napi_get_last_error_info);
                napi_get_named_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_named_property>(
                    DelayLoadStubs.napi_get_named_property);
                napi_get_new_target = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_callback_info, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_new_target>(
                    DelayLoadStubs.napi_get_new_target);
                napi_get_null = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_null>(
                    DelayLoadStubs.napi_get_null);
                napi_get_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_property>(
                    DelayLoadStubs.napi_get_property);
                napi_get_property_names = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_property_names>(
                    DelayLoadStubs.napi_get_property_names);
                napi_get_prototype = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_prototype>(
                    DelayLoadStubs.napi_get_prototype);
                napi_get_reference_value = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_ref, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_reference_value>(
                    DelayLoadStubs.napi_get_reference_value);
                napi_get_typedarray_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, nint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_typedarray_info>(
                    DelayLoadStubs.napi_get_typedarray_info);
                napi_get_undefined = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_undefined>(
                    DelayLoadStubs.napi_get_undefined);
                napi_get_value_bigint_int64 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_get_value_bigint_int64>(
                    DelayLoadStubs.napi_get_value_bigint_int64);
                napi_get_value_bigint_uint64 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_get_value_bigint_uint64>(
                    DelayLoadStubs.napi_get_value_bigint_uint64);
                napi_get_value_bigint_words = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_get_value_bigint_words>(
                    DelayLoadStubs.napi_get_value_bigint_words);
                napi_get_value_bool = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_value_bool>(
                    DelayLoadStubs.napi_get_value_bool);
                napi_get_value_double = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_value_double>(
                    DelayLoadStubs.napi_get_value_double);
                napi_get_value_external = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_value_external>(
                    DelayLoadStubs.napi_get_value_external);
                napi_get_value_int32 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_value_int32>(
                    DelayLoadStubs.napi_get_value_int32);
                napi_get_value_int64 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_value_int64>(
                    DelayLoadStubs.napi_get_value_int64);
                napi_get_value_string_latin1 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_get_value_string_latin1>(
                    DelayLoadStubs.napi_get_value_string_latin1);
                napi_get_value_string_utf16 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_get_value_string_utf16>(
                    DelayLoadStubs.napi_get_value_string_utf16);
                napi_get_value_string_utf8 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_get_value_string_utf8>(
                    DelayLoadStubs.napi_get_value_string_utf8);
                napi_get_value_uint32 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_value_uint32>(
                    DelayLoadStubs.napi_get_value_uint32);
                napi_get_version = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_version>(
                    DelayLoadStubs.napi_get_version);
                napi_has_element = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_has_element>(
                    DelayLoadStubs.napi_has_element);
                napi_has_named_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_has_named_property>(
                    DelayLoadStubs.napi_has_named_property);
                napi_has_own_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_has_own_property>(
                    DelayLoadStubs.napi_has_own_property);
                napi_has_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_has_property>(
                    DelayLoadStubs.napi_has_property);
                napi_instanceof = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_instanceof>(
                    DelayLoadStubs.napi_instanceof);
                napi_is_array = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_is_array>(
                    DelayLoadStubs.napi_is_array);
                napi_is_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_is_arraybuffer>(
                    DelayLoadStubs.napi_is_arraybuffer);
                napi_is_dataview = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_is_dataview>(
                    DelayLoadStubs.napi_is_dataview);
                napi_is_date = (delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_is_date>(
                    DelayLoadStubs.napi_is_date);
                napi_is_detached_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_is_detached_arraybuffer>(
                    DelayLoadStubs.napi_is_detached_arraybuffer);
                napi_is_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_is_error>(
                    DelayLoadStubs.napi_is_error);
                napi_is_exception_pending = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_is_exception_pending>(
                    DelayLoadStubs.napi_is_exception_pending);
                napi_is_promise = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_is_promise>(
                    DelayLoadStubs.napi_is_promise);
                napi_is_typedarray = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_is_typedarray>(
                    DelayLoadStubs.napi_is_typedarray);
                napi_new_instance = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nuint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_new_instance>(
                    DelayLoadStubs.napi_new_instance);
                napi_object_freeze = (delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_object_freeze>(
                    DelayLoadStubs.napi_object_freeze);
                napi_object_seal = (delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_object_seal>(
                    DelayLoadStubs.napi_object_seal);
                napi_open_escapable_handle_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_open_escapable_handle_scope>(
                    DelayLoadStubs.napi_open_escapable_handle_scope);
                napi_open_handle_scope = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_open_handle_scope>(
                    DelayLoadStubs.napi_open_handle_scope);
                napi_reference_ref = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_ref, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_reference_ref>(
                    DelayLoadStubs.napi_reference_ref);
                napi_reference_unref = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_ref, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_reference_unref>(
                    DelayLoadStubs.napi_reference_unref);
                napi_reject_deferred = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_deferred, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_reject_deferred>(
                    DelayLoadStubs.napi_reject_deferred);
                napi_remove_wrap = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_remove_wrap>(
                    DelayLoadStubs.napi_remove_wrap);
                napi_resolve_deferred = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_deferred, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_resolve_deferred>(
                    DelayLoadStubs.napi_resolve_deferred);
                napi_run_script = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_run_script>(
                    DelayLoadStubs.napi_run_script);
                napi_set_element = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_set_element>(
                    DelayLoadStubs.napi_set_element);
                napi_set_instance_data = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_finalize, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_set_instance_data>(
                    DelayLoadStubs.napi_set_instance_data);
                napi_set_named_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_set_named_property>(
                    DelayLoadStubs.napi_set_named_property);
                napi_set_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_set_property>(
                    DelayLoadStubs.napi_set_property);
                napi_strict_equals = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_strict_equals>(
                    DelayLoadStubs.napi_strict_equals);
                napi_throw = (delegate* unmanaged[Cdecl]<napi_env, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_throw>(
                    DelayLoadStubs.napi_throw);
                napi_throw_error = (delegate* unmanaged[Cdecl]<napi_env, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_throw_error>(
                    DelayLoadStubs.napi_throw_error);
                napi_throw_range_error = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_throw_range_error>(
                    DelayLoadStubs.napi_throw_range_error);
                napi_throw_type_error = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_throw_type_error>(
                    DelayLoadStubs.napi_throw_type_error);
                napi_type_tag_object = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_type_tag_object>(
                    DelayLoadStubs.napi_type_tag_object);
                napi_typeof = (delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_typeof>(
                    DelayLoadStubs.napi_typeof);
                napi_unwrap = (delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_unwrap>(
                    DelayLoadStubs.napi_unwrap);
                napi_wrap = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_finalize, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_wrap>(
                    DelayLoadStubs.napi_wrap);
                node_api_create_syntax_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.node_api_create_syntax_error>(
                    DelayLoadStubs.node_api_create_syntax_error);
                node_api_symbol_for = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.node_api_symbol_for>(
                    DelayLoadStubs.node_api_symbol_for);
                node_api_throw_syntax_error = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.node_api_throw_syntax_error>(
                    DelayLoadStubs.node_api_throw_syntax_error);

                //----------------------------------------------------------------------------------
                // node_api.h APIs
                //----------------------------------------------------------------------------------

                napi_acquire_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_threadsafe_function, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_acquire_threadsafe_function>(
                    DelayLoadStubs.napi_acquire_threadsafe_function);
                napi_add_async_cleanup_hook = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_cleanup_hook, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_add_async_cleanup_hook>(
                    DelayLoadStubs.napi_add_async_cleanup_hook);
                napi_add_env_cleanup_hook = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_cleanup_hook, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_add_env_cleanup_hook>(
                    DelayLoadStubs.napi_add_env_cleanup_hook);
                napi_async_destroy = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_context, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_async_destroy>(
                    DelayLoadStubs.napi_async_destroy);
                napi_async_init = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_async_init>(
                    DelayLoadStubs.napi_async_init);
                napi_call_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_threadsafe_function,
                    nint,
                    napi_threadsafe_function_call_mode,
                    napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_call_threadsafe_function>(
                    DelayLoadStubs.napi_call_threadsafe_function);
                napi_cancel_async_work = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_work, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_cancel_async_work>(
                    DelayLoadStubs.napi_cancel_async_work);
                napi_close_callback_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_callback_scope, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_close_callback_scope>(
                    DelayLoadStubs.napi_close_callback_scope);
                napi_create_async_work = (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_value,
                    napi_value,
                    napi_async_execute_callback,
                    napi_async_complete_callback,
                    nint,
                    nint,
                    napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_async_work>(
                    DelayLoadStubs.napi_create_async_work);
                napi_create_buffer = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_buffer>(
                    DelayLoadStubs.napi_create_buffer);
                napi_create_buffer_copy = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_buffer_copy>(
                    DelayLoadStubs.napi_create_buffer_copy);
                napi_create_external_buffer = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, napi_finalize, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_create_external_buffer>(
                    DelayLoadStubs.napi_create_external_buffer);
                napi_create_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_value,
                    napi_value,
                    napi_value,
                    nuint,
                    nuint,
                    nint,
                    napi_finalize,
                    nint,
                    napi_threadsafe_function_call_js,
                    nint,
                    napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_create_threadsafe_function>(
                    DelayLoadStubs.napi_create_threadsafe_function);
                napi_delete_async_work = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_work, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_delete_async_work>(
                    DelayLoadStubs.napi_delete_async_work);
                napi_fatal_error = (delegate* unmanaged[Cdecl]<
                    nint, nuint, nint, nuint, void>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_fatal_error>(
                    DelayLoadStubs.napi_fatal_error);
                napi_fatal_exception = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_fatal_exception>(
                    DelayLoadStubs.napi_fatal_exception);
                napi_get_buffer_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_buffer_info>(
                    DelayLoadStubs.napi_get_buffer_info);
                napi_get_node_version = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_node_version>(
                    DelayLoadStubs.napi_get_node_version);
                napi_get_threadsafe_function_context = (delegate* unmanaged[Cdecl]<
                    napi_threadsafe_function, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_get_threadsafe_function_context>(
                    DelayLoadStubs.napi_get_threadsafe_function_context);
                napi_get_uv_event_loop = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_get_uv_event_loop>(
                    DelayLoadStubs.napi_get_uv_event_loop);
                napi_is_buffer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_is_buffer>(
                    DelayLoadStubs.napi_is_buffer);
                napi_make_callback = (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_async_context,
                    napi_value,
                    napi_value,
                    nuint,
                    nint,
                    nint,
                    napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_make_callback>(
                    DelayLoadStubs.napi_make_callback);
                napi_module_register = (delegate* unmanaged[Cdecl]<nint, void>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_module_register>(
                    DelayLoadStubs.napi_module_register);
                napi_open_callback_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_async_context, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_open_callback_scope>(
                    DelayLoadStubs.napi_open_callback_scope);
                napi_queue_async_work = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_work, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_queue_async_work>(
                    DelayLoadStubs.napi_queue_async_work);
                napi_ref_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_threadsafe_function, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_ref_threadsafe_function>(
                    DelayLoadStubs.napi_ref_threadsafe_function);
                napi_release_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_threadsafe_function, napi_threadsafe_function_release_mode, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_release_threadsafe_function>(
                    DelayLoadStubs.napi_release_threadsafe_function);
                napi_remove_async_cleanup_hook = (delegate* unmanaged[Cdecl]<
                    napi_async_cleanup_hook_handle, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_remove_async_cleanup_hook>(
                    DelayLoadStubs.napi_remove_async_cleanup_hook);
                napi_remove_env_cleanup_hook = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_cleanup_hook, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_remove_env_cleanup_hook>(
                    DelayLoadStubs.napi_remove_env_cleanup_hook);
                napi_unref_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_threadsafe_function, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.napi_unref_threadsafe_function>(
                    DelayLoadStubs.napi_unref_threadsafe_function);
                node_api_get_module_file_name = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<
                    DelegateTypes.node_api_get_module_file_name>(
                    DelayLoadStubs.node_api_get_module_file_name);

                //----------------------------------------------------------------------------------
                // Embedding APIs
                //----------------------------------------------------------------------------------

                napi_await_promise = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_await_promise>(
                    DelayLoadStubs.napi_await_promise);
                napi_create_environment = (delegate* unmanaged[Cdecl]<
                    napi_platform, napi_error_message_handler, nint, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_environment>(
                    DelayLoadStubs.napi_create_environment);
                napi_create_platform = (delegate* unmanaged[Cdecl]<
                    int, nint, int, nint, napi_error_message_handler, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_create_platform>(
                    DelayLoadStubs.napi_create_platform);
                napi_destroy_environment = (delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_destroy_environment>(
                    DelayLoadStubs.napi_destroy_environment);
                napi_destroy_platform = (delegate* unmanaged[Cdecl]<napi_platform, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_destroy_platform>(
                    DelayLoadStubs.napi_destroy_platform);
                napi_run_environment = (delegate* unmanaged[Cdecl]<napi_env, napi_status>)
                    GetFunctionPointerForDelegateAndRootIt<DelegateTypes.napi_run_environment>(
                    DelayLoadStubs.napi_run_environment);
#endif
            }
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
        public record struct napi_platform(nint Handle);

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
#if NET6_0_OR_GREATER
            public napi_callback(
                delegate* unmanaged[Cdecl]<napi_env, napi_callback_info, napi_value> handle)
                : this((nint)handle) { }
#else
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate napi_value Delegate(napi_env env, napi_callback_info callbackInfo);

            public napi_callback(napi_callback.Delegate callback)
                : this(Marshal.GetFunctionPointerForDelegate(callback)) { }
#endif
        }

        public record struct napi_finalize(nint Handle)
        {
#if NET6_0_OR_GREATER
            public napi_finalize(delegate* unmanaged[Cdecl]<napi_env, nint, nint, void> handle)
                : this((nint)handle) { }
#else
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void Delegate(napi_env env, nint data, nint hint);

            public napi_finalize(napi_finalize.Delegate callback)
                : this (Marshal.GetFunctionPointerForDelegate(callback)) { }
#endif
        }

        public struct napi_error_message_handler
        {
            public nint Handle;

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            public delegate void Delegate(byte* message);

            public napi_error_message_handler(napi_error_message_handler.Delegate handler)
                => Handle = Marshal.GetFunctionPointerForDelegate(handler);
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
        {
            result = default;
            fixed (nint* result_ptr = &result)
            {
                return s_funcs.napi_get_last_error_info(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_undefined(napi_env env, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_undefined(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_null(napi_env env, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_null(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_global(napi_env env, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_global(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_boolean(
            napi_env env, c_bool value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_boolean(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_object(napi_env env, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_object(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_array(napi_env env, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_array(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_array_with_length(
            napi_env env, nuint length, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_array_with_length(env, length, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_double(
            napi_env env, double value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_double(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_int32(
            napi_env env, int value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_int32(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_uint32(
            napi_env env, uint value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_uint32(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_int64(
            napi_env env, long value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_int64(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_string_latin1(
            napi_env env, byte* str, nuint length, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_string_latin1(
                    env, (nint)str, length, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_string_utf8(
            napi_env env, byte* str, nuint length, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_string_utf8(
                    env, (nint)str, length, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_string_utf16(
            napi_env env, char* str, nuint length, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_string_utf16(
                    env, (nint)str, length, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_symbol(
            napi_env env, napi_value description, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_symbol(env, description, (nint)result_ptr);
            }
        }

        internal static napi_status node_api_symbol_for(
            napi_env env, byte* utf8name, nuint length, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.node_api_symbol_for(
                    env, (nint)utf8name, length, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_function(
            napi_env env,
            byte* utf8name,
            nuint length,
            napi_callback cb,
            nint data,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_function(
                    env, (nint)utf8name, length, cb, data, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_error(
            napi_env env, napi_value code, napi_value msg, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_error(env, code, msg, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_type_error(
            napi_env env, napi_value code, napi_value msg, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_type_error(env, code, msg, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_range_error(
            napi_env env, napi_value code, napi_value msg, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_range_error(env, code, msg, (nint)result_ptr);
            }
        }

        internal static napi_status node_api_create_syntax_error(
            napi_env env, napi_value code, napi_value msg, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.node_api_create_syntax_error(env, code, msg, (nint)result_ptr);
            }
        }

        internal static napi_status napi_typeof(
            napi_env env, napi_value value, out napi_valuetype result)
        {
            result = default;
            fixed (napi_valuetype* result_ptr = &result)
            {
                return s_funcs.napi_typeof(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_double(
            napi_env env, napi_value value, out double result)
        {
            result = default;
            fixed (double* result_ptr = &result)
            {
                return s_funcs.napi_get_value_double(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_int32(
            napi_env env, napi_value value, out int result)
        {
            result = default;
            fixed (int* result_ptr = &result)
            {
                return s_funcs.napi_get_value_int32(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_uint32(
            napi_env env, napi_value value, out uint result)
        {
            result = default;
            fixed (uint* result_ptr = &result)
            {
                return s_funcs.napi_get_value_uint32(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_int64(
            napi_env env, napi_value value, out long result)
        {
            result = default;
            fixed (long* result_ptr = &result)
            {
                return s_funcs.napi_get_value_int64(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_bool(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_get_value_bool(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_string_latin1(
            napi_env env, napi_value value, nint buf, nuint bufsize, out nuint result)
        {
            result = default;
            fixed (nuint* result_ptr = &result)
            {
                return s_funcs.napi_get_value_string_latin1(
                    env, value, buf, bufsize, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_string_utf8(
            napi_env env, napi_value value, nint buf, nuint bufsize, out nuint result)
        {
            result = default;
            fixed (nuint* result_ptr = &result)
            {
                return s_funcs.napi_get_value_string_utf8(
                    env, value, buf, bufsize, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_string_utf16(
            napi_env env, napi_value value, nint buf, nuint bufsize, out nuint result)
        {
            result = default;
            fixed (nuint* result_ptr = &result)
            {
                return s_funcs.napi_get_value_string_utf16(
                    env, value, buf, bufsize, (nint)result_ptr);
            }
        }

        internal static napi_status napi_coerce_to_bool(
            napi_env env, napi_value value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_coerce_to_bool(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_coerce_to_number(
            napi_env env, napi_value value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_coerce_to_number(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_coerce_to_object(
            napi_env env, napi_value value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_coerce_to_object(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_coerce_to_string(
            napi_env env, napi_value value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_coerce_to_string(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_prototype(
            napi_env env, napi_value js_object, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_prototype(env, js_object, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_property_names(
            napi_env env, napi_value js_object, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_property_names(env, js_object, (nint)result_ptr);
            }
        }

        internal static napi_status napi_set_property(
            napi_env env, napi_value js_object, napi_value key, napi_value value)
            => s_funcs.napi_set_property(env, js_object, key, value);

        internal static napi_status napi_has_property(
            napi_env env, napi_value js_object, napi_value key, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_has_property(env, js_object, key, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_property(
            napi_env env, napi_value js_object, napi_value key, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_property(env, js_object, key, (nint)result_ptr);
            }
        }

        internal static napi_status napi_delete_property(
            napi_env env, napi_value js_object, napi_value key, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_delete_property(env, js_object, key, (nint)result_ptr);
            }
        }

        internal static napi_status napi_has_own_property(
            napi_env env, napi_value js_object, napi_value key, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_has_own_property(env, js_object, key, (nint)result_ptr);
            }
        }

        internal static napi_status napi_set_named_property(
            napi_env env, napi_value js_object, nint utf8name, napi_value value)
            => s_funcs.napi_set_named_property(env, js_object, utf8name, value);

        internal static napi_status napi_has_named_property(
            napi_env env, napi_value js_object, nint utf8name, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_has_named_property(
                    env, js_object, utf8name, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_named_property(
            napi_env env, napi_value js_object, nint utf8name, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_named_property(
                    env, js_object, utf8name, (nint)result_ptr);
            }
        }

        internal static napi_status napi_set_element(
            napi_env env, napi_value js_object, uint index, napi_value value)
            => s_funcs.napi_set_element(env, js_object, index, value);

        internal static napi_status napi_has_element(
            napi_env env, napi_value js_object, uint index, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_has_element(env, js_object, index, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_element(
            napi_env env, napi_value js_object, uint index, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_element(env, js_object, index, (nint)result_ptr);
            }
        }

        internal static napi_status napi_delete_element(
            napi_env env, napi_value js_object, uint index, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_delete_element(env, js_object, index, (nint)result_ptr);
            }
        }

        internal static napi_status napi_define_properties(
            napi_env env, napi_value js_object, nuint property_count, nint properties)
            => s_funcs.napi_define_properties(env, js_object, property_count, properties);

        internal static napi_status napi_is_array(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_array(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_array_length(
            napi_env env, napi_value value, out uint result)
        {
            result = default;
            fixed (uint* result_ptr = &result)
            {
                return s_funcs.napi_get_array_length(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_strict_equals(
            napi_env env, napi_value lhs, napi_value rhs, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_strict_equals(env, lhs, rhs, (nint)result_ptr);
            }
        }

        internal static napi_status napi_call_function(
            napi_env env,
            napi_value recv,
            napi_value func,
            nuint argc,
            nint argv,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_call_function(env, recv, func, argc, argv, (nint)result_ptr);
            }
        }

        internal static napi_status napi_new_instance(
            napi_env env,
            napi_value constructor,
            nuint argc,
            nint argv,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_new_instance(env, constructor, argc, argv, (nint)result_ptr);
            }
        }

        internal static napi_status napi_instanceof(
            napi_env env, napi_value js_object, napi_value constructor, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_instanceof(env, js_object, constructor, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_cb_info(
            napi_env env,              // [in] NAPI environment handle
            napi_callback_info cbinfo, // [in] Opaque callback-info handle
            nuint* argc,               // [in-out] Specifies the size of the provided argv array
                                       // and receives the actual count of args.
            napi_value* argv,          // [out] Array of values
            napi_value* this_arg,      // [out] Receives the JS 'this' arg for the call
            nint* data)                // [out] Receives the data pointer for the callback.
            => s_funcs.napi_get_cb_info(
                env, cbinfo, (nint)argc, (nint)argv, (nint)this_arg, (nint)data);

        internal static napi_status napi_get_new_target(
            napi_env env, napi_callback_info cbinfo, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_new_target(env, cbinfo, (nint)result_ptr);
            }
        }

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
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_define_class(
                    env,
                    utf8name,
                    length,
                    constructor,
                    data,
                    property_count,
                    properties,
                    (nint)result_ptr);
            }
        }

        internal static napi_status napi_wrap(
            napi_env env,
            napi_value js_object,
            nint native_object,
            napi_finalize finalize_cb,
            nint finalize_hint,
            napi_ref* result)
            => s_funcs.napi_wrap(
                env, js_object, native_object, finalize_cb, finalize_hint, (nint)result);

        internal static napi_status napi_unwrap(
            napi_env env, napi_value js_object, out nint result)
        {
            result = default;
            fixed (nint* result_ptr = &result)
            {
                return s_funcs.napi_unwrap(env, js_object, (nint)result_ptr);
            }
        }

        internal static napi_status napi_remove_wrap(
            napi_env env, napi_value js_object, out nint result)
        {
            result = default;
            fixed (nint* result_ptr = &result)
            {
                return s_funcs.napi_remove_wrap(env, js_object, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_external(
            napi_env env,
            nint data,
            napi_finalize finalize_cb,
            nint finalize_hint,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_external(
                    env, data, finalize_cb, finalize_hint, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_external(
            napi_env env, napi_value value, out nint result)
        {
            result = default;
            fixed (nint* result_ptr = &result)
            {
                return s_funcs.napi_get_value_external(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_reference(
            napi_env env, napi_value value, uint initial_refcount, out napi_ref result)
        {
            result = default;
            fixed (napi_ref* result_ptr = &result)
            {
                return s_funcs.napi_create_reference(
                    env, value, initial_refcount, (nint)result_ptr);
            }
        }

        internal static napi_status napi_delete_reference(napi_env env, napi_ref @ref)
            => s_funcs.napi_delete_reference(env, @ref);

        internal static napi_status napi_reference_ref(napi_env env, napi_ref @ref, nint result)
            => s_funcs.napi_reference_ref(env, @ref, result);

        internal static napi_status napi_reference_unref(napi_env env, napi_ref @ref, nint result)
            => s_funcs.napi_reference_unref(env, @ref, result);

        internal static napi_status napi_get_reference_value(
            napi_env env, napi_ref @ref, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_reference_value(env, @ref, (nint)result_ptr);
            }
        }

        internal static napi_status napi_open_handle_scope(
            napi_env env, out napi_handle_scope result)
        {
            result = default;
            fixed (napi_handle_scope* result_ptr = &result)
            {
                return s_funcs.napi_open_handle_scope(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_close_handle_scope(napi_env env, napi_handle_scope scope)
            => s_funcs.napi_close_handle_scope(env, scope);

        internal static napi_status napi_open_escapable_handle_scope(
            napi_env env, out napi_escapable_handle_scope result)
        {
            result = default;
            fixed (napi_escapable_handle_scope* result_ptr = &result)
            {
                return s_funcs.napi_open_escapable_handle_scope(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_close_escapable_handle_scope(
            napi_env env, napi_escapable_handle_scope scope)
            => s_funcs.napi_close_escapable_handle_scope(env, scope);

        internal static napi_status napi_escape_handle(napi_env env,
            napi_escapable_handle_scope scope, napi_value escapee, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_escape_handle(env, scope, escapee, (nint)result_ptr);
            }
        }

        internal static napi_status napi_throw(napi_env env, napi_value error)
            => s_funcs.napi_throw(env, error);

        internal static napi_status napi_throw_error(napi_env env, string? code, string msg)
        {
            nint code_ptr = code == null ? default : StringToHGlobalUtf8(code);
            nint msg_ptr = StringToHGlobalUtf8(msg);
            try
            {
                return s_funcs.napi_throw_error(env, code_ptr, msg_ptr);
            }
            finally
            {
                if (code_ptr != default) Marshal.FreeHGlobal(code_ptr);
                if (msg_ptr != default) Marshal.FreeHGlobal(msg_ptr);
            }
        }

        internal static napi_status napi_throw_type_error(napi_env env, string? code, string msg)
        {
            nint code_ptr = code == null ? default : StringToHGlobalUtf8(code);
            nint msg_ptr = StringToHGlobalUtf8(msg);
            try
            {
                return s_funcs.napi_throw_type_error(env, code_ptr, msg_ptr);
            }
            finally
            {
                if (code_ptr != default) Marshal.FreeHGlobal(code_ptr);
                if (msg_ptr != default) Marshal.FreeHGlobal(msg_ptr);
            }
        }

        internal static napi_status napi_throw_range_error(napi_env env, string? code, string msg)
        {
            nint code_ptr = code == null ? default : StringToHGlobalUtf8(code);
            nint msg_ptr = StringToHGlobalUtf8(msg);
            try
            {
                return s_funcs.napi_throw_range_error(env, code_ptr, msg_ptr);
            }
            finally
            {
                if (code_ptr != default) Marshal.FreeHGlobal(code_ptr);
                if (msg_ptr != default) Marshal.FreeHGlobal(msg_ptr);
            }
        }

        internal static napi_status node_api_throw_syntax_error(
            napi_env env, string? code, string msg)
        {
            nint code_ptr = code == null ? default : StringToHGlobalUtf8(code);
            nint msg_ptr = StringToHGlobalUtf8(msg);
            try
            {
                return s_funcs.node_api_throw_syntax_error(env, code_ptr, msg_ptr);
            }
            finally
            {
                if (code_ptr != default) Marshal.FreeHGlobal(code_ptr);
                if (msg_ptr != default) Marshal.FreeHGlobal(msg_ptr);
            }
        }

        internal static napi_status napi_is_error(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_error(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_is_exception_pending(napi_env env, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_exception_pending(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_and_clear_last_exception(
            napi_env env, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_and_clear_last_exception(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_is_arraybuffer(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_arraybuffer(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_arraybuffer(
            napi_env env, nuint byte_length, out nint data, out napi_value result)
        {
            data = default;
            result = default;
            fixed (nint* data_ptr = &data)
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_arraybuffer(
                    env, byte_length, (nint)data_ptr, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_external_arraybuffer(
            napi_env env,
            nint external_data,
            nuint byte_length,
            napi_finalize finalize_cb,
            nint finalize_hint,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_external_arraybuffer(
                    env,
                    external_data,
                    byte_length,
                    finalize_cb,
                    finalize_hint,
                    (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_arraybuffer_info(
            napi_env env, napi_value arraybuffer, out nint data, out nuint byte_length)
        {
            data = default;
            byte_length = default;
            fixed (nint* data_ptr = &data)
            fixed (nuint* byte_length_ptr = &byte_length)
            {
                return s_funcs.napi_get_arraybuffer_info(
                    env, arraybuffer, (nint)data_ptr, (nint)byte_length_ptr);
            }
        }

        internal static napi_status napi_is_typedarray(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_typedarray(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_typedarray(
            napi_env env,
            napi_typedarray_type type,
            nuint length,
            napi_value arraybuffer,
            nuint byte_offset,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_typedarray(
                    env, type, length, arraybuffer, byte_offset, (nint)result_ptr);
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
        {
            type = default;
            length = default;
            data = default;
            arraybuffer = default;
            byte_offset = default;
            fixed (napi_typedarray_type* type_ptr = &type)
            fixed (nuint* length_ptr = &length)
            fixed (nint* data_ptr = &data)
            fixed (napi_value* arraybuffer_ptr = &arraybuffer)
            fixed (nuint* byte_offset_ptr = &byte_offset)
            {
                return s_funcs.napi_get_typedarray_info(
                    env,
                    typedarray,
                    (nint)type_ptr,
                    (nint)length_ptr,
                    (nint)data_ptr,
                    (nint)arraybuffer_ptr,
                    (nint)byte_offset_ptr);
            }
        }

        internal static napi_status napi_create_dataview(
            napi_env env,
            nuint length,
            napi_value arraybuffer,
            nuint byte_offset,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_dataview(
                    env, length, arraybuffer, byte_offset, (nint)result_ptr);
            }
        }

        internal static napi_status napi_is_dataview(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_dataview(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_dataview_info(
            napi_env env,
            napi_value dataview,
            out nuint bytelength,
            out nint data,
            out napi_value arraybuffer,
            out nuint byte_offset)
        {
            bytelength = default;
            data = default;
            arraybuffer = default;
            byte_offset = default;
            fixed (nuint* bytelength_ptr = &bytelength)
            fixed (nint* data_ptr = &data)
            fixed (napi_value* arraybuffer_ptr = &arraybuffer)
            fixed (nuint* byte_offset_ptr = &byte_offset)
            {
                return s_funcs.napi_get_dataview_info(
                    env,
                    dataview,
                    (nint)bytelength_ptr,
                    (nint)data_ptr,
                    (nint)arraybuffer_ptr,
                    (nint)byte_offset_ptr);
            }
        }

        internal static napi_status napi_get_version(napi_env env, out uint result)
        {
            result = default;
            fixed (uint* result_ptr = &result)
            {
                return s_funcs.napi_get_version(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_promise(
            napi_env env, out napi_deferred deferred, out napi_value promise)
        {
            deferred = default;
            promise = default;
            fixed (napi_deferred* deferred_ptr = &deferred)
            fixed (napi_value* promise_ptr = &promise)
            {
                return s_funcs.napi_create_promise(env, (nint)deferred_ptr, (nint)promise_ptr);
            }
        }

        internal static napi_status napi_resolve_deferred(
            napi_env env, napi_deferred deferred, napi_value resolution)
            => s_funcs.napi_resolve_deferred(env, deferred, resolution);

        internal static napi_status napi_reject_deferred(
            napi_env env, napi_deferred deferred, napi_value rejection)
            => s_funcs.napi_reject_deferred(env, deferred, rejection);


        internal static napi_status napi_is_promise(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_promise(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_run_script(
            napi_env env, napi_value script, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_run_script(env, script, (nint)result_ptr);
            }
        }

        internal static napi_status napi_adjust_external_memory(
            napi_env env, long change_in_bytes, out long adjusted_value)
        {
            adjusted_value = default;
            fixed (long* adjusted_value_ptr = &adjusted_value)
            {
                return s_funcs.napi_adjust_external_memory(
                    env, change_in_bytes, (nint)adjusted_value_ptr);
            }
        }

        internal static napi_status napi_create_date(
            napi_env env, double time, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_date(env, time, (nint)result_ptr);
            }
        }

        internal static napi_status napi_is_date(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_date(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_date_value(
            napi_env env, napi_value value, out double result)
        {
            result = default;
            fixed (double* result_ptr = &result)
            {
                return s_funcs.napi_get_date_value(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_add_finalizer(
            napi_env env,
            napi_value js_object,
            nint native_object,
            napi_finalize finalize_cb,
            nint finalize_hint,
            napi_ref* result)
            => s_funcs.napi_add_finalizer(
                env,
                js_object,
                native_object,
                finalize_cb,
                finalize_hint,
                (nint)result);

        internal static napi_status napi_create_bigint_int64(
            napi_env env, long value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_bigint_int64(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_bigint_uint64(
            napi_env env, ulong value, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_bigint_uint64(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_create_bigint_words(
            napi_env env, int sign_bit, nuint word_count, ulong* words, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_create_bigint_words(
                    env, sign_bit, word_count, (nint)words, (nint)result_ptr);
            }
        }

        internal static napi_status napi_get_value_bigint_int64(
            napi_env env, napi_value value, out long result, out c_bool lossless)
        {
            result = default;
            lossless = default;
            fixed (long* result_ptr = &result)
            fixed (c_bool* lossless_ptr = &lossless)
            {
                return s_funcs.napi_get_value_bigint_int64(
                    env, value, (nint)result_ptr, (nint)lossless_ptr);
            }
        }

        internal static napi_status napi_get_value_bigint_uint64(
            napi_env env, napi_value value, out ulong result, out c_bool lossless)
        {
            result = default;
            lossless = default;
            fixed (ulong* result_ptr = &result)
            fixed (c_bool* lossless_ptr = &lossless)
            {
                return s_funcs.napi_get_value_bigint_uint64(
                    env, value, (nint)result_ptr, (nint)lossless_ptr);
            }
        }

        internal static napi_status napi_get_value_bigint_words(
            napi_env env, napi_value value, out int sign_bit, out nuint word_count, ulong* words)
        {
            sign_bit = default;
            word_count = default;
            fixed (int* sign_bit_ptr = &sign_bit)
            fixed (nuint* word_count_ptr = &word_count)
            {
                return s_funcs.napi_get_value_bigint_words(
                    env, value, (nint)sign_bit_ptr, (nint)word_count_ptr, (nint)words);
            }
        }

        internal static napi_status napi_get_all_property_names(
            napi_env env,
            napi_value js_object,
            napi_key_collection_mode key_mode,
            napi_key_filter key_filter,
            napi_key_conversion key_conversion,
            out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_get_all_property_names(
                    env, js_object, key_mode, key_filter, key_conversion, (nint)result_ptr);
            }
        }

        internal static napi_status napi_set_instance_data(
            napi_env env, nint data, napi_finalize finalize_cb, nint finalize_hint)
            => s_funcs.napi_set_instance_data(env, data, finalize_cb, finalize_hint);

        internal static napi_status napi_get_instance_data(napi_env env, out nint result)
        {
            result = default;
            fixed (nint* result_ptr = &result)
            {
                return s_funcs.napi_get_instance_data(env, (nint)result_ptr);
            }
        }

        internal static napi_status napi_detach_arraybuffer(napi_env env, napi_value arraybuffer)
            => s_funcs.napi_detach_arraybuffer(env, arraybuffer);

        internal static napi_status napi_is_detached_arraybuffer(
            napi_env env, napi_value value, out c_bool result)
        {
            result = default;
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_is_detached_arraybuffer(env, value, (nint)result_ptr);
            }
        }

        internal static napi_status napi_type_tag_object(
            napi_env env, napi_value value, in napi_type_tag type_tag)
        {
            fixed (napi_type_tag* type_tag_ptr = &type_tag)
            {
                return s_funcs.napi_type_tag_object(env, value, (nint)type_tag_ptr);
            }
        }

        internal static napi_status napi_check_object_type_tag(
            napi_env env, napi_value value, in napi_type_tag type_tag, out c_bool result)
        {
            result = default;
            fixed (napi_type_tag* type_tag_ptr = &type_tag)
            fixed (c_bool* result_ptr = &result)
            {
                return s_funcs.napi_check_object_type_tag(
                    env, value, (nint)type_tag_ptr, (nint)result_ptr);
            }
        }

        internal static napi_status napi_object_freeze(napi_env env, napi_value js_object)
            => s_funcs.napi_object_freeze(env, js_object);

        internal static napi_status napi_object_seal(napi_env env, napi_value js_object)
            => s_funcs.napi_object_seal(env, js_object);

        //==========================================================================================
        // Embedding APIs
        //==========================================================================================

        internal static napi_status napi_create_platform(
            string[]? args,
            string[]? exec_args,
            napi_error_message_handler err_handler,
            out napi_platform result)
        {
            result = default;
            fixed (napi_platform* result_ptr = &result)
            {
                // TODO: Handle args, exec_args.
                return s_funcs.napi_create_platform(
                    0, default, 0, default, err_handler, (nint)result_ptr);
            }
        }

        internal static napi_status napi_destroy_platform(napi_platform platform)
            => s_funcs.napi_destroy_platform(platform);

        internal static napi_status napi_create_environment(
            napi_platform platform,
            napi_error_message_handler err_handler,
            string? main_script,
            out napi_env result)
        {
            result = default;
            nint main_script_ptr = main_script == null ? default : StringToHGlobalUtf8(main_script);
            try
            {
                fixed (napi_env* result_ptr = &result)
                {
                    return s_funcs.napi_create_environment(
                        platform, err_handler, main_script_ptr, (nint)result_ptr);
                }
            }
            finally
            {
                if (main_script_ptr != default) Marshal.FreeHGlobal(main_script_ptr);
            }
        }

        internal static napi_status napi_run_environment(napi_env env)
            => s_funcs.napi_run_environment(env);

        internal static napi_status napi_await_promise(
            napi_env env, napi_value promise, out napi_value result)
        {
            result = default;
            fixed (napi_value* result_ptr = &result)
            {
                return s_funcs.napi_await_promise(env, promise, (nint)result_ptr);
            }
        }

        internal static napi_status napi_destroy_environment(napi_env env, out int exit_code)
        {
            exit_code = default;
            fixed (int* exit_code_ptr = &exit_code)
            {
                return s_funcs.napi_destroy_environment(env, (nint)exit_code_ptr);
            }
        }

        private static nint GetExport([CallerMemberName] string functionName = "")
            => NativeLibrary.GetExport(s_libraryHandle, functionName);

        internal static bool TryGetExport(
            ref nint field, [CallerMemberName] string functionName = "")
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

        internal static nint StringToHGlobalUtf8(string s)
        {
            if (s == null) return default;
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            nint ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            return ptr;
        }

        private static nint GetFunctionPointerForDelegateAndRootIt<TDelegate>(TDelegate d)
            where TDelegate : notnull
        {
            GCHandle.Alloc(d); // To make sure that the delegate is not collected.
            return Marshal.GetFunctionPointerForDelegate<TDelegate>(d);
        }


#if !NET6_0_OR_GREATER
            // Delegates for the native functions to implement native pointers that were not
            // supported in the .Net Framework.
            public static class DelegateTypes
            {
                //----------------------------------------------------------------------------------
                // js_native_api.h APIs
                //----------------------------------------------------------------------------------

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_add_finalizer(napi_env env,
                    napi_value js_object,
                    nint native_object,
                    napi_finalize finalize_cb,
                    nint finalize_hint,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_adjust_external_memory(
                    napi_env env, long change_in_bytes, nint adjusted_value);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_call_function(
                    napi_env env,
                    napi_value recv,
                    napi_value func,
                    nuint argc,
                    nint argv,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_check_object_type_tag(
                    napi_env env, napi_value value, nint type_tag, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_close_escapable_handle_scope(
                    napi_env env, napi_escapable_handle_scope scope);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_close_handle_scope(
                    napi_env env, napi_handle_scope scope);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_coerce_to_bool(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_coerce_to_number(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_coerce_to_object(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_coerce_to_string(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_array(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_array_with_length(
                    napi_env env, nuint length, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_arraybuffer(
                    napi_env env, nuint byte_length, nint data, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_bigint_int64(
                    napi_env env, long value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_bigint_uint64(
                    napi_env env, ulong value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_bigint_words(
                    napi_env env, int sign_bit, nuint word_count, nint words, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_dataview(
                    napi_env env,
                    nuint length,
                    napi_value arraybuffer,
                    nuint byte_offset,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_date(
                    napi_env env, double time, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_double(
                    napi_env env, double value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_error(
                    napi_env env, napi_value code, napi_value msg, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_external(
                    napi_env env,
                    nint data,
                    napi_finalize finalize_cb,
                    nint finalize_hint,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_external_arraybuffer(
                    napi_env env,
                    nint external_data,
                    nuint byte_length,
                    napi_finalize finalize_cb,
                    nint finalize_hint,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_function(
                    napi_env env,
                    nint utf8name,
                    nuint length,
                    napi_callback cb,
                    nint data,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_int32(napi_env env, int value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_int64(
                    napi_env env, long value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_object(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_promise(
                    napi_env env, nint deferred, nint promise);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_range_error(
                    napi_env env, napi_value code, napi_value msg, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_reference(
                    napi_env env, napi_value value, uint initial_refcount, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_string_latin1(
                    napi_env env, nint str, nuint length, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_string_utf16(
                    napi_env env, nint str, nuint length, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_string_utf8(
                    napi_env env, nint str, nuint length, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_symbol(
                    napi_env env, napi_value description, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_type_error(
                    napi_env env, napi_value code, napi_value msg, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_typedarray(
                    napi_env env,
                    napi_typedarray_type type,
                    nuint length,
                    napi_value arraybuffer,
                    nuint byte_offset,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_uint32(
                    napi_env env, uint value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_define_class(
                    napi_env env,
                    nint utf8name,
                    nuint length,
                    napi_callback constructor,
                    nint data,
                    nuint property_count,
                    nint properties,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_define_properties(
                    napi_env env, napi_value js_object, nuint property_count, nint properties);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_delete_element(
                    napi_env env, napi_value js_object, uint index, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_delete_property(
                    napi_env env, napi_value js_object, napi_value key, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_delete_reference(napi_env env, napi_ref @ref);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_detach_arraybuffer(
                    napi_env env, napi_value arraybuffer);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_escape_handle(
                    napi_env env,
                    napi_escapable_handle_scope scope,
                    napi_value escapee,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_all_property_names(
                    napi_env env,
                    napi_value js_object,
                    napi_key_collection_mode key_mode,
                    napi_key_filter key_filter,
                    napi_key_conversion key_conversion,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_and_clear_last_exception(
                    napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_array_length(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_arraybuffer_info(
                    napi_env env, napi_value arraybuffer, nint data, nint byte_length);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_boolean(
                    napi_env env, c_bool value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_cb_info(
                    napi_env env,
                    napi_callback_info cbinfo,
                    nint argc,
                    nint argv,
                    nint this_arg,
                    nint data);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_dataview_info(
                    napi_env env,
                    napi_value dataview,
                    nint bytelength,
                    nint data,
                    nint arraybuffer,
                    nint byte_offset);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_date_value(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_element(
                    napi_env env, napi_value js_object, uint index, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_global(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_instance_data(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_last_error_info(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_named_property(
                    napi_env env, napi_value js_object, nint utf8name, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_new_target(
                    napi_env env, napi_callback_info cbinfo, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_null(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_property(
                    napi_env env, napi_value js_object, napi_value key, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_property_names(
                    napi_env env, napi_value js_object, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_prototype(
                    napi_env env, napi_value js_object, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_reference_value(
                    napi_env env, napi_ref @ref, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_typedarray_info(
                    napi_env env,
                    napi_value typedarray,
                    nint type,
                    nint length,
                    nint data,
                    nint arraybuffer,
                    nint byte_offset);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_undefined(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_bigint_int64(
                    napi_env env, napi_value value, nint result, nint lossless);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_bigint_uint64(
                    napi_env env, napi_value value, nint result, nint lossless);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_bigint_words(
                    napi_env env, napi_value value, nint sign_bit, nint word_count, nint words);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_bool(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_double(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_external(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_int32(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_int64(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_string_latin1(
                    napi_env env, napi_value value, nint buf, nuint bufsize, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_string_utf16(
                    napi_env env, napi_value value, nint buf, nuint bufsize, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_string_utf8(
                    napi_env env, napi_value value, nint buf, nuint bufsize, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_value_uint32(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_version(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_has_element(
                    napi_env env, napi_value js_object, uint index, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_has_named_property(
                    napi_env env, napi_value js_object, nint utf8name, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_has_own_property(
                    napi_env env, napi_value js_object, napi_value key, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_has_property(
                    napi_env env, napi_value js_object, napi_value key, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_instanceof(
                    napi_env env, napi_value js_object, napi_value constructor, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_array(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_arraybuffer(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_dataview(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_date(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_detached_arraybuffer(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_error(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_exception_pending(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_promise(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_typedarray(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_new_instance(
                    napi_env env, napi_value constructor, nuint argc, nint argv, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_object_freeze(napi_env env, napi_value js_object);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_object_seal(napi_env env, napi_value js_object);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_open_escapable_handle_scope(
                    napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_open_handle_scope(napi_env env, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_reference_ref(
                    napi_env env, napi_ref @ref, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_reference_unref(
                    napi_env env, napi_ref @ref, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_reject_deferred(
                    napi_env env, napi_deferred deferred, napi_value rejection);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_remove_wrap(
                    napi_env env, napi_value js_object, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_resolve_deferred(
                    napi_env env, napi_deferred deferred, napi_value resolution);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_run_script(
                    napi_env env, napi_value script, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_set_element(
                    napi_env env, napi_value js_object, uint index, napi_value value);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_set_instance_data(
                    napi_env env, nint data, napi_finalize finalize_cb, nint finalize_hint);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_set_named_property(
                    napi_env env, napi_value js_object, nint utf8name, napi_value value);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_set_property(
                    napi_env env, napi_value js_object, napi_value key, napi_value value);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_strict_equals(
                    napi_env env, napi_value lhs, napi_value rhs, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_throw(napi_env env, napi_value error);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_throw_error(napi_env env, nint code, nint msg);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_throw_range_error(
                    napi_env env, nint code, nint msg);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_throw_type_error(
                    napi_env env, nint code, nint msg);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_type_tag_object(
                    napi_env env, napi_value value, nint type_tag);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_typeof(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_unwrap(
                    napi_env env, napi_value js_object, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_wrap(
                    napi_env env,
                    napi_value js_object,
                    nint native_object,
                    napi_finalize finalize_cb,
                    nint finalize_hint,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status node_api_create_syntax_error(
                    napi_env env, napi_value code, napi_value msg, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status node_api_symbol_for(
                    napi_env env, nint utf8name, nuint length, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status node_api_throw_syntax_error(
                    napi_env env, nint code, nint msg);

                //----------------------------------------------------------------------------------
                // node_api.h APIs
                //----------------------------------------------------------------------------------

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_acquire_threadsafe_function(
                    napi_threadsafe_function func);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_add_async_cleanup_hook(
                     napi_env env, napi_async_cleanup_hook hook, nint arg, nint remove_handle);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_add_env_cleanup_hook(
                    napi_env env, napi_cleanup_hook fun, nint arg);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_async_destroy(
                    napi_env env, napi_async_context async_context);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_async_init(
                    napi_env env,
                    napi_value async_resource,
                    napi_value async_resource_name,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_call_threadsafe_function(
                    napi_threadsafe_function func,
                    nint data,
                    napi_threadsafe_function_call_mode is_blocking);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_cancel_async_work(
                    napi_env env, napi_async_work work);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_close_callback_scope(
                    napi_env env, napi_callback_scope scope);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_async_work(
                    napi_env env,
                    napi_value async_resource,
                    napi_value async_resource_name,
                    napi_async_execute_callback execute,
                    napi_async_complete_callback complete,
                    nint data,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_buffer(
                    napi_env env, nuint length, nint data, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_buffer_copy(
                    napi_env env, nuint length, nint data, nint result_data, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_external_buffer(
                    napi_env env,
                    nuint length,
                    nint data,
                    napi_finalize finalize_cb,
                    nint finalize_hint,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_threadsafe_function(
                    napi_env env,
                    napi_value func,
                    napi_value async_resource,
                    napi_value async_resource_name,
                    nuint max_queue_size,
                    nuint initial_thread_count,
                    nint thread_finalize_data,
                    napi_finalize thread_finalize_cb,
                    nint context,
                    napi_threadsafe_function_call_js call_js_cb,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_delete_async_work(
                    napi_env env, napi_async_work work);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate void napi_fatal_error(
                    nint location, nuint location_length, nint message, nuint message_length);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_fatal_exception(napi_env env, napi_value err);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_buffer_info(
                    napi_env env, napi_value value, nint data, nint length);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_node_version(napi_env env, nint version);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_threadsafe_function_context(
                    napi_threadsafe_function func, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_get_uv_event_loop(napi_env env, nint loop);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_is_buffer(
                    napi_env env, napi_value value, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_make_callback(
                    napi_env env,
                    napi_async_context async_context,
                    napi_value recv,
                    napi_value func,
                    nuint argc,
                    nint argv,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate void napi_module_register(nint mod);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_open_callback_scope(
                    napi_env env,
                    napi_value resource_object,
                    napi_async_context context,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_queue_async_work(
                    napi_env env, napi_async_work work);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_ref_threadsafe_function(
                    napi_env env, napi_threadsafe_function func);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_release_threadsafe_function(
                    napi_threadsafe_function func, napi_threadsafe_function_release_mode mode);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_remove_async_cleanup_hook(
                    napi_async_cleanup_hook_handle remove_handle);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_remove_env_cleanup_hook(
                    napi_env env, napi_cleanup_hook fun, nint arg);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_unref_threadsafe_function(
                    napi_env env, napi_threadsafe_function func);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status node_api_get_module_file_name(
                    napi_env env, nint result);

                //----------------------------------------------------------------------------------
                // Embedding APIs
                //----------------------------------------------------------------------------------

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_await_promise(
                    napi_env env, napi_value promise, nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_environment(
                    napi_platform platform,
                    napi_error_message_handler err_handler,
                    nint main_script,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_create_platform(
                    int argc,
                    nint argv,
                    int exec_argc,
                    nint exec_argv,
                    napi_error_message_handler err_handler,
                    nint result);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_destroy_environment(napi_env env, nint exit_code);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_destroy_platform(napi_platform platform);

                [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
                public delegate napi_status napi_run_environment(napi_env env);
            }
#endif

        // Each delay load stub loads the target native function, assigns it to the
        // function pointer field, and then calls it.
        public static class DelayLoadStubs
        {
            //----------------------------------------------------------------------------------
            // js_native_api.h APIs
            //----------------------------------------------------------------------------------

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_add_finalizer(napi_env env,
                napi_value js_object,
                nint native_object,
                napi_finalize finalize_cb,
                nint finalize_hint,
                nint result)
            {
                s_funcs.napi_add_finalizer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_finalize, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_add_finalizer(
                    env, js_object, native_object, finalize_cb, finalize_hint, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_adjust_external_memory(
                napi_env env, long change_in_bytes, nint adjusted_value)
            {
                s_funcs.napi_adjust_external_memory =
                    (delegate* unmanaged[Cdecl]<napi_env, long, nint, napi_status>)GetExport();
                return s_funcs.napi_adjust_external_memory(
                    env, change_in_bytes, adjusted_value);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_call_function(
                napi_env env, napi_value recv, napi_value func, nuint argc, nint argv, nint result)
            {
                s_funcs.napi_call_function = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nuint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_call_function(env, recv, func, argc, argv, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_check_object_type_tag(
                napi_env env, napi_value value, nint type_tag, nint result)
            {
                s_funcs.napi_check_object_type_tag = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_check_object_type_tag(env, value, type_tag, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_close_escapable_handle_scope(
                napi_env env, napi_escapable_handle_scope scope)
            {
                s_funcs.napi_close_escapable_handle_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_escapable_handle_scope, napi_status>)GetExport();
                return s_funcs.napi_close_escapable_handle_scope(env, scope);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_close_handle_scope(
                napi_env env, napi_handle_scope scope)
            {
                s_funcs.napi_close_handle_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_handle_scope, napi_status>)GetExport();
                return s_funcs.napi_close_handle_scope(env, scope);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_coerce_to_bool(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_coerce_to_bool = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_coerce_to_bool(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_coerce_to_number(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_coerce_to_number = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_coerce_to_number(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_coerce_to_object(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_coerce_to_object = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_coerce_to_object(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_coerce_to_string(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_coerce_to_string = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_coerce_to_string(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_array(napi_env env, nint result)
            {
                s_funcs.napi_create_array = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_create_array(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_array_with_length(
                napi_env env, nuint length, nint result)
            {
                s_funcs.napi_create_array_with_length = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_array_with_length(env, length, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_arraybuffer(
                napi_env env, nuint byte_length, nint data, nint result)
            {
                s_funcs.napi_create_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_arraybuffer(env, byte_length, data, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_bigint_int64(
                napi_env env, long value, nint result)
            {
                s_funcs.napi_create_bigint_int64 = (delegate* unmanaged[Cdecl]<
                    napi_env, long, nint, napi_status>)GetExport();
                return s_funcs.napi_create_bigint_int64(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_bigint_uint64(
                napi_env env, ulong value, nint result)
            {
                s_funcs.napi_create_bigint_uint64 = (delegate* unmanaged[Cdecl]<
                    napi_env, ulong, nint, napi_status>)GetExport();
                return s_funcs.napi_create_bigint_uint64(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_bigint_words(
                napi_env env, int sign_bit, nuint word_count, nint words, nint result)
            {
                s_funcs.napi_create_bigint_words = (delegate* unmanaged[Cdecl]<
                    napi_env, int, nuint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_bigint_words(env, sign_bit, word_count, words, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_dataview(
                napi_env env, nuint length, napi_value arraybuffer, nuint byte_offset, nint result)
            {
                s_funcs.napi_create_dataview = (delegate* unmanaged[Cdecl]<
                napi_env, nuint, napi_value, nuint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_dataview(env, length, arraybuffer, byte_offset, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_date(napi_env env, double time, nint result)
            {
                s_funcs.napi_create_date = (delegate* unmanaged[Cdecl]<
                    napi_env, double, nint, napi_status>)GetExport();
                return s_funcs.napi_create_date(env, time, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_double(napi_env env, double value, nint result)
            {
                s_funcs.napi_create_double = (delegate* unmanaged[Cdecl]<
                    napi_env, double, nint, napi_status>)GetExport();
                return s_funcs.napi_create_double(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_error(
                napi_env env, napi_value code, napi_value msg, nint result)
            {
                s_funcs.napi_create_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_create_error(env, code, msg, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_external(
                napi_env env, nint data, napi_finalize finalize_cb, nint finalize_hint, nint result)
            {
                s_funcs.napi_create_external = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_finalize, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_external(env, data, finalize_cb, finalize_hint, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_external_arraybuffer(
                napi_env env,
                nint external_data,
                nuint byte_length,
                napi_finalize finalize_cb,
                nint finalize_hint,
                nint result)
            {
                s_funcs.napi_create_external_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, napi_finalize, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_external_arraybuffer(
                    env, external_data, byte_length, finalize_cb, finalize_hint, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_function(
                napi_env env, nint utf8name, nuint length, napi_callback cb, nint data, nint result)
            {
                s_funcs.napi_create_function = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, napi_callback, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_function(env, utf8name, length, cb, data, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_int32(napi_env env, int value, nint result)
            {
                s_funcs.napi_create_int32 = (delegate* unmanaged[Cdecl]<
                    napi_env, int, nint, napi_status>)GetExport();
                return s_funcs.napi_create_int32(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_int64(napi_env env, long value, nint result)
            {
                s_funcs.napi_create_int64 = (delegate* unmanaged[Cdecl]<
                    napi_env, long, nint, napi_status>)GetExport();
                return s_funcs.napi_create_int64(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_object(napi_env env, nint result)
            {
                s_funcs.napi_create_object = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_create_object(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_promise(
                napi_env env, nint deferred, nint promise)
            {
                s_funcs.napi_create_promise = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_promise(env, deferred, promise);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_range_error(
                napi_env env, napi_value code, napi_value msg, nint result)
            {
                s_funcs.napi_create_range_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_create_range_error(env, code, msg, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_reference(
                napi_env env, napi_value value, uint initial_refcount, nint result)
            {
                s_funcs.napi_create_reference = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_reference(env, value, initial_refcount, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_string_latin1(
                napi_env env, nint str, nuint length, nint result)
            {
                s_funcs.napi_create_string_latin1 = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_string_latin1(env, str, length, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_string_utf16(
                napi_env env, nint str, nuint length, nint result)
            {
                s_funcs.napi_create_string_utf16 = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_string_utf16(env, str, length, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_string_utf8(
                napi_env env, nint str, nuint length, nint result)
            {
                s_funcs.napi_create_string_utf8 = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_string_utf8(env, str, length, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_symbol(
                napi_env env, napi_value description, nint result)
            {
                s_funcs.napi_create_symbol = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_create_symbol(env, description, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_type_error(
                napi_env env, napi_value code, napi_value msg, nint result)
            {
                s_funcs.napi_create_type_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_create_type_error(env, code, msg, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_typedarray(
                napi_env env,
                napi_typedarray_type type,
                nuint length,
                napi_value arraybuffer,
                nuint byte_offset,
                nint result)
            {
                s_funcs.napi_create_typedarray = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_typedarray_type, nuint, napi_value, nuint, nint, napi_status>)
                    GetExport();
                return s_funcs.napi_create_typedarray(
                    env, type, length, arraybuffer, byte_offset, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_uint32(napi_env env, uint value, nint result)
            {
                s_funcs.napi_create_uint32 = (delegate* unmanaged[Cdecl]<
                    napi_env, uint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_uint32(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_define_class(
                napi_env env,
                nint utf8name,
                nuint length,
                napi_callback constructor,
                nint data,
                nuint property_count,
                nint properties,
                nint result)
            {
                s_funcs.napi_define_class = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, napi_callback, nint, nuint, nint, nint, napi_status>)
                    GetExport();
                return s_funcs.napi_define_class(
                    env, utf8name, length, constructor, data, property_count, properties, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_define_properties(
                napi_env env, napi_value js_object, nuint property_count, nint properties)
            {
                s_funcs.napi_define_properties = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nuint, nint, napi_status>)GetExport();
                return s_funcs.napi_define_properties(env, js_object, property_count, properties);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_delete_element(
                napi_env env, napi_value js_object, uint index, nint result)
            {
                s_funcs.napi_delete_element = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, nint, napi_status>)GetExport();
                return s_funcs.napi_delete_element(env, js_object, index, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_delete_property(
                napi_env env, napi_value js_object, napi_value key, nint result)
            {
                s_funcs.napi_delete_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_delete_property(env, js_object, key, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_delete_reference(napi_env env, napi_ref @ref)
            {
                s_funcs.napi_delete_reference = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_ref, napi_status>)GetExport();
                return s_funcs.napi_delete_reference(env, @ref);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_detach_arraybuffer(
                napi_env env, napi_value arraybuffer)
            {
                s_funcs.napi_detach_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_status>)GetExport();
                return s_funcs.napi_detach_arraybuffer(env, arraybuffer);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_escape_handle(
                napi_env env, napi_escapable_handle_scope scope, napi_value escapee, nint result)
            {
                s_funcs.napi_escape_handle = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_escapable_handle_scope, napi_value, nint, napi_status>)
                    GetExport();
                return s_funcs.napi_escape_handle(env, scope, escapee, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_all_property_names(
                napi_env env,
                napi_value js_object,
                napi_key_collection_mode key_mode,
                napi_key_filter key_filter,
                napi_key_conversion key_conversion,
                nint result)
            {
                s_funcs.napi_get_all_property_names = (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_value,
                    napi_key_collection_mode,
                    napi_key_filter,
                    napi_key_conversion,
                    nint,
                    napi_status>)GetExport();
                return s_funcs.napi_get_all_property_names(
                    env, js_object, key_mode, key_filter, key_conversion, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_and_clear_last_exception(napi_env env, nint result)
            {
                s_funcs.napi_get_and_clear_last_exception = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_get_and_clear_last_exception(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_array_length(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_get_array_length = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_array_length(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_arraybuffer_info(
                napi_env env, napi_value arraybuffer, nint data, nint byte_length)
            {
                s_funcs.napi_get_arraybuffer_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_arraybuffer_info(env, arraybuffer, data, byte_length);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_boolean(napi_env env, c_bool value, nint result)
            {
                s_funcs.napi_get_boolean = (delegate* unmanaged[Cdecl]<
                    napi_env, c_bool, nint, napi_status>)GetExport();
                return s_funcs.napi_get_boolean(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_cb_info(
                napi_env env,
                napi_callback_info cbinfo,
                nint argc,
                nint argv,
                nint this_arg,
                nint data)
            {
                s_funcs.napi_get_cb_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_callback_info, nint, nint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_cb_info(env, cbinfo, argc, argv, this_arg, data);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_dataview_info(
                napi_env env,
                napi_value dataview,
                nint bytelength,
                nint data,
                nint arraybuffer,
                nint byte_offset)
            {
                s_funcs.napi_get_dataview_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_dataview_info(
                    env, dataview, bytelength, data, arraybuffer, byte_offset);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_date_value(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_get_date_value = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_date_value(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_element(
                napi_env env, napi_value js_object, uint index, nint result)
            {
                s_funcs.napi_get_element = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_element(env, js_object, index, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_global(napi_env env, nint result)
            {
                s_funcs.napi_get_global = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_get_global(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_instance_data(napi_env env, nint result)
            {
                s_funcs.napi_get_instance_data = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_get_instance_data(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_last_error_info(napi_env env, nint result)
            {
                s_funcs.napi_get_last_error_info = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_get_last_error_info(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_named_property(
                napi_env env, napi_value js_object, nint utf8name, nint result)
            {
                s_funcs.napi_get_named_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_named_property(env, js_object, utf8name, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_new_target(
                napi_env env, napi_callback_info cbinfo, nint result)
            {
                s_funcs.napi_get_new_target = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_callback_info, nint, napi_status>)GetExport();
                return s_funcs.napi_get_new_target(env, cbinfo, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_null(napi_env env, nint result)
            {
                s_funcs.napi_get_null = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_get_null(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_property(
                napi_env env, napi_value js_object, napi_value key, nint result)
            {
                s_funcs.napi_get_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_property(env, js_object, key, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_property_names(
                napi_env env, napi_value js_object, nint result)
            {
                s_funcs.napi_get_property_names = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_property_names(env, js_object, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_prototype(
                napi_env env, napi_value js_object, nint result)
            {
                s_funcs.napi_get_prototype = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_prototype(env, js_object, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_reference_value(
                napi_env env, napi_ref @ref, nint result)
            {
                s_funcs.napi_get_reference_value = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_ref, nint, napi_status>)GetExport();
                return s_funcs.napi_get_reference_value(env, @ref, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_typedarray_info(
                napi_env env,
                napi_value typedarray,
                nint type,
                nint length,
                nint data,
                nint arraybuffer,
                nint byte_offset)
            {
                s_funcs.napi_get_typedarray_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, nint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_typedarray_info(
                    env, typedarray, type, length, data, arraybuffer, byte_offset);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_undefined(napi_env env, nint result)
            {
                s_funcs.napi_get_undefined = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_get_undefined(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_bigint_int64(
                napi_env env, napi_value value, nint result, nint lossless)
            {
                s_funcs.napi_get_value_bigint_int64 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_bigint_int64(env, value, result, lossless);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_bigint_uint64(
                napi_env env, napi_value value, nint result, nint lossless)
            {
                s_funcs.napi_get_value_bigint_uint64 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_bigint_uint64(env, value, result, lossless);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_bigint_words(
                napi_env env, napi_value value, nint sign_bit, nint word_count, nint words)
            {
                s_funcs.napi_get_value_bigint_words = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_bigint_words(
                    env, value, sign_bit, word_count, words);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_bool(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_get_value_bool = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_bool(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_double(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_get_value_double = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_double(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_external(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_get_value_external = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_external(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_int32(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_get_value_int32 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_int32(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_int64(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_get_value_int64 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_int64(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_string_latin1(
                napi_env env, napi_value value, nint buf, nuint bufsize, nint result)
            {
                s_funcs.napi_get_value_string_latin1 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nuint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_string_latin1(env, value, buf, bufsize, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_string_utf16(
                napi_env env, napi_value value, nint buf, nuint bufsize, nint result)
            {
                s_funcs.napi_get_value_string_utf16 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nuint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_string_utf16(env, value, buf, bufsize, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_string_utf8(
                napi_env env, napi_value value, nint buf, nuint bufsize, nint result)
            {
                s_funcs.napi_get_value_string_utf8 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nuint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_string_utf8(env, value, buf, bufsize, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_value_uint32(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_get_value_uint32 = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_get_value_uint32(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_version(napi_env env, nint result)
            {
                s_funcs.napi_get_version = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_get_version(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_has_element(
                napi_env env, napi_value js_object, uint index, nint result)
            {
                s_funcs.napi_has_element = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, nint, napi_status>)GetExport();
                return s_funcs.napi_has_element(env, js_object, index, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_has_named_property(
                napi_env env, napi_value js_object, nint utf8name, nint result)
            {
                s_funcs.napi_has_named_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_has_named_property(
                    env, js_object, utf8name, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_has_own_property(
                napi_env env, napi_value js_object, napi_value key, nint result)
            {
                s_funcs.napi_has_own_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_has_own_property(env, js_object, key, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_has_property(
                napi_env env, napi_value js_object, napi_value key, nint result)
            {
                s_funcs.napi_has_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_has_property(env, js_object, key, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_instanceof(
                napi_env env, napi_value js_object, napi_value constructor, nint result)
            {
                s_funcs.napi_instanceof = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_instanceof(env, js_object, constructor, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_array(napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_is_array = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_is_array(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_arraybuffer(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_is_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_is_arraybuffer(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_dataview(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_is_dataview = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_is_dataview(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_date(napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_is_date = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_is_date(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_detached_arraybuffer(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_is_detached_arraybuffer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_is_detached_arraybuffer(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_error(napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_is_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_is_error(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_exception_pending(napi_env env, nint result)
            {
                s_funcs.napi_is_exception_pending = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_is_exception_pending(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_promise(napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_is_promise = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_is_promise(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_typedarray(
                napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_is_typedarray = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_is_typedarray(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_new_instance(
                napi_env env, napi_value constructor, nuint argc, nint argv, nint result)
            {
                s_funcs.napi_new_instance = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nuint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_new_instance(env, constructor, argc, argv, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_object_freeze(napi_env env, napi_value js_object)
            {
                s_funcs.napi_object_freeze = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_status>)GetExport();
                return s_funcs.napi_object_freeze(env, js_object);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_object_seal(napi_env env, napi_value js_object)
            {
                s_funcs.napi_object_seal = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_status>)GetExport();
                return s_funcs.napi_object_seal(env, js_object);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_open_escapable_handle_scope(napi_env env, nint result)
            {
                s_funcs.napi_open_escapable_handle_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_open_escapable_handle_scope(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_open_handle_scope(napi_env env, nint result)
            {
                s_funcs.napi_open_handle_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_open_handle_scope(env, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_reference_ref(napi_env env, napi_ref @ref, nint result)
            {
                s_funcs.napi_reference_ref = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_ref, nint, napi_status>)GetExport();
                return s_funcs.napi_reference_ref(env, @ref, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_reference_unref(
                napi_env env, napi_ref @ref, nint result)
            {
                s_funcs.napi_reference_unref = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_ref, nint, napi_status>)GetExport();
                return s_funcs.napi_reference_unref(env, @ref, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_reject_deferred(
                napi_env env, napi_deferred deferred, napi_value rejection)
            {
                s_funcs.napi_reject_deferred = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_deferred, napi_value, napi_status>)GetExport();
                return s_funcs.napi_reject_deferred(env, deferred, rejection);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_remove_wrap(
                napi_env env, napi_value js_object, nint result)
            {
                s_funcs.napi_remove_wrap = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_remove_wrap(env, js_object, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_resolve_deferred(
                napi_env env, napi_deferred deferred, napi_value resolution)
            {
                s_funcs.napi_resolve_deferred = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_deferred, napi_value, napi_status>)GetExport();
                return s_funcs.napi_resolve_deferred(env, deferred, resolution);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_run_script(
                napi_env env, napi_value script, nint result)
            {
                s_funcs.napi_run_script = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_run_script(env, script, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_set_element(
                napi_env env, napi_value js_object, uint index, napi_value value)
            {
                s_funcs.napi_set_element = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, uint, napi_value, napi_status>)GetExport();
                return s_funcs.napi_set_element(env, js_object, index, value);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_set_instance_data(
                napi_env env, nint data, napi_finalize finalize_cb, nint finalize_hint)
            {
                s_funcs.napi_set_instance_data = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_finalize, nint, napi_status>)GetExport();
                return s_funcs.napi_set_instance_data(env, data, finalize_cb, finalize_hint);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_set_named_property(
                napi_env env, napi_value js_object, nint utf8name, napi_value value)
            {
                s_funcs.napi_set_named_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_value, napi_status>)GetExport();
                return s_funcs.napi_set_named_property(env, js_object, utf8name, value);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_set_property(
                napi_env env, napi_value js_object, napi_value key, napi_value value)
            {
                s_funcs.napi_set_property = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, napi_value, napi_status>)GetExport();
                return s_funcs.napi_set_property(env, js_object, key, value);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_strict_equals(
                napi_env env, napi_value lhs, napi_value rhs, nint result)
            {
                s_funcs.napi_strict_equals = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_strict_equals(env, lhs, rhs, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_throw(napi_env env, napi_value error)
            {
                s_funcs.napi_throw = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_status>)GetExport();
                return s_funcs.napi_throw(env, error);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_throw_error(napi_env env, nint code, nint msg)
            {
                s_funcs.napi_throw_error = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_throw_error(env, code, msg);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_throw_range_error(napi_env env, nint code, nint msg)
            {
                s_funcs.napi_throw_range_error = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_throw_range_error(env, code, msg);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_throw_type_error(napi_env env, nint code, nint msg)
            {
                s_funcs.napi_throw_type_error = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_throw_type_error(env, code, msg);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_type_tag_object(
                napi_env env, napi_value value, nint type_tag)
            {
                s_funcs.napi_type_tag_object = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_type_tag_object(env, value, type_tag);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_typeof(napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_typeof = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_typeof(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_unwrap(napi_env env, napi_value js_object, nint result)
            {
                s_funcs.napi_unwrap = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_unwrap(env, js_object, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_wrap(
                napi_env env,
                napi_value js_object,
                nint native_object,
                napi_finalize finalize_cb,
                nint finalize_hint,
                nint result)
            {
                s_funcs.napi_wrap = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_finalize, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_wrap(
                    env, js_object, native_object, finalize_cb, finalize_hint, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status node_api_create_syntax_error(
                napi_env env, napi_value code, napi_value msg, nint result)
            {
                s_funcs.node_api_create_syntax_error = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.node_api_create_syntax_error(env, code, msg, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status node_api_symbol_for(
                napi_env env, nint utf8name, nuint length, nint result)
            {
                s_funcs.node_api_symbol_for = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nuint, nint, napi_status>)GetExport();
                return s_funcs.node_api_symbol_for(env, utf8name, length, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status node_api_throw_syntax_error(
                napi_env env, nint code, nint msg)
            {
                s_funcs.node_api_throw_syntax_error = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, nint, napi_status>)GetExport();
                return s_funcs.node_api_throw_syntax_error(env, code, msg);
            }

            //----------------------------------------------------------------------------------
            // node_api.h APIs
            //----------------------------------------------------------------------------------

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_acquire_threadsafe_function(
                napi_threadsafe_function func)
            {
                s_funcs.napi_acquire_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_threadsafe_function, napi_status>)GetExport();
                return s_funcs.napi_acquire_threadsafe_function(func);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_add_async_cleanup_hook(
                 napi_env env, napi_async_cleanup_hook hook, nint arg, nint remove_handle)
            {
                s_funcs.napi_add_async_cleanup_hook = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_cleanup_hook, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_add_async_cleanup_hook(env, hook, arg, remove_handle);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_add_env_cleanup_hook(
                napi_env env, napi_cleanup_hook fun, nint arg)
            {
                s_funcs.napi_add_env_cleanup_hook = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_cleanup_hook, nint, napi_status>)GetExport();
                return s_funcs.napi_add_env_cleanup_hook(env, fun, arg);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_async_destroy(
                napi_env env, napi_async_context async_context)
            {
                s_funcs.napi_async_destroy = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_context, napi_status>)GetExport();
                return s_funcs.napi_async_destroy(env, async_context);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_async_init(
                napi_env env,
                napi_value async_resource,
                napi_value async_resource_name,
                nint result)
            {
                s_funcs.napi_async_init = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_async_init(env, async_resource, async_resource_name, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_call_threadsafe_function(
                napi_threadsafe_function func,
                nint data,
                napi_threadsafe_function_call_mode is_blocking)
            {
                s_funcs.napi_call_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_threadsafe_function,
                    nint,
                    napi_threadsafe_function_call_mode,
                    napi_status>)GetExport();
                return s_funcs.napi_call_threadsafe_function(func, data, is_blocking);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_cancel_async_work(napi_env env, napi_async_work work)
            {
                s_funcs.napi_cancel_async_work = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_work, napi_status>)GetExport();
                return s_funcs.napi_cancel_async_work(env, work);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_close_callback_scope(
                napi_env env, napi_callback_scope scope)
            {
                s_funcs.napi_close_callback_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_callback_scope, napi_status>)GetExport();
                return s_funcs.napi_close_callback_scope(env, scope);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_async_work(
                napi_env env,
                napi_value async_resource,
                napi_value async_resource_name,
                napi_async_execute_callback execute,
                napi_async_complete_callback complete,
                nint data,
                nint result)
            {
                s_funcs.napi_create_async_work = (delegate* unmanaged[Cdecl]<
                     napi_env,
                    napi_value,
                    napi_value,
                    napi_async_execute_callback,
                    napi_async_complete_callback,
                    nint,
                    nint,
                    napi_status>)GetExport();
                return s_funcs.napi_create_async_work(
                    env, async_resource, async_resource_name, execute, complete, data, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_buffer(
                napi_env env, nuint length, nint data, nint result)
            {
                s_funcs.napi_create_buffer = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_buffer(env, length, data, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_buffer_copy(
                napi_env env, nuint length, nint data, nint result_data, nint result)
            {
                s_funcs.napi_create_buffer_copy = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_buffer_copy(env, length, data, result_data, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_external_buffer(
                napi_env env,
                nuint length,
                nint data,
                napi_finalize finalize_cb,
                nint finalize_hint,
                nint result)
            {
                s_funcs.napi_create_external_buffer = (delegate* unmanaged[Cdecl]<
                    napi_env, nuint, nint, napi_finalize, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_external_buffer(
                    env, length, data, finalize_cb, finalize_hint, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_threadsafe_function(
                napi_env env,
                napi_value func,
                napi_value async_resource,
                napi_value async_resource_name,
                nuint max_queue_size,
                nuint initial_thread_count,
                nint thread_finalize_data,
                napi_finalize thread_finalize_cb,
                nint context,
                napi_threadsafe_function_call_js call_js_cb,
                nint result)
            {
                s_funcs.napi_create_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_value,
                    napi_value,
                    napi_value,
                    nuint,
                    nuint,
                    nint,
                    napi_finalize,
                    nint,
                    napi_threadsafe_function_call_js,
                    nint,
                    napi_status>)GetExport();
                return s_funcs.napi_create_threadsafe_function(env,
                    func,
                    async_resource,
                    async_resource_name,
                    max_queue_size,
                    initial_thread_count,
                    thread_finalize_data,
                    thread_finalize_cb,
                    context,
                    call_js_cb,
                    result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_delete_async_work(napi_env env, napi_async_work work)
            {
                s_funcs.napi_delete_async_work = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_work, napi_status>)GetExport();
                return s_funcs.napi_delete_async_work(env, work);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static void napi_fatal_error(
                nint location, nuint location_length, nint message, nuint message_length)
            {
                s_funcs.napi_fatal_error = (delegate* unmanaged[Cdecl]<
                    nint, nuint, nint, nuint, void>)GetExport();
                s_funcs.napi_fatal_error(location, location_length, message, message_length);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_fatal_exception(napi_env env, napi_value err)
            {
                s_funcs.napi_fatal_exception = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_status>)GetExport();
                return s_funcs.napi_fatal_exception(env, err);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_buffer_info(
                napi_env env, napi_value value, nint data, nint length)
            {
                s_funcs.napi_get_buffer_info = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_get_buffer_info(env, value, data, length);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_node_version(napi_env env, nint version)
            {
                s_funcs.napi_get_node_version = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_get_node_version(env, version);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_threadsafe_function_context(
                napi_threadsafe_function func, nint result)
            {
                s_funcs.napi_get_threadsafe_function_context = (delegate* unmanaged[Cdecl]<
                    napi_threadsafe_function, nint, napi_status>)GetExport();
                return s_funcs.napi_get_threadsafe_function_context(func, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_get_uv_event_loop(napi_env env, nint loop)
            {
                s_funcs.napi_get_uv_event_loop = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_get_uv_event_loop(env, loop);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_is_buffer(napi_env env, napi_value value, nint result)
            {
                s_funcs.napi_is_buffer = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_is_buffer(env, value, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_make_callback(
                napi_env env,
                napi_async_context async_context,
                napi_value recv,
                napi_value func,
                nuint argc,
                nint argv,
                nint result)
            {
                s_funcs.napi_make_callback = (delegate* unmanaged[Cdecl]<
                    napi_env,
                    napi_async_context,
                    napi_value,
                    napi_value,
                    nuint,
                    nint,
                    nint,
                    napi_status>)GetExport();
                return s_funcs.napi_make_callback(
                    env, async_context, recv, func, argc, argv, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static void napi_module_register(nint mod)
            {
                s_funcs.napi_module_register = (delegate* unmanaged[Cdecl]<nint, void>)GetExport();
                s_funcs.napi_module_register(mod);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_open_callback_scope(
                napi_env env, napi_value resource_object, napi_async_context context, nint result)
            {
                s_funcs.napi_open_callback_scope = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, napi_async_context, nint, napi_status>)GetExport();
                return s_funcs.napi_open_callback_scope(env, resource_object, context, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_queue_async_work(napi_env env, napi_async_work work)
            {
                s_funcs.napi_queue_async_work = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_async_work, napi_status>)GetExport();
                return s_funcs.napi_queue_async_work(env, work);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_ref_threadsafe_function(
                napi_env env, napi_threadsafe_function func)
            {
                s_funcs.napi_ref_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_threadsafe_function, napi_status>)GetExport();
                return s_funcs.napi_ref_threadsafe_function(env, func);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_release_threadsafe_function(
                napi_threadsafe_function func, napi_threadsafe_function_release_mode mode)
            {
                s_funcs.napi_release_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_threadsafe_function, napi_threadsafe_function_release_mode, napi_status>)
                    GetExport();
                return s_funcs.napi_release_threadsafe_function(func, mode);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_remove_async_cleanup_hook(
                napi_async_cleanup_hook_handle remove_handle)
            {
                s_funcs.napi_remove_async_cleanup_hook = (delegate* unmanaged[Cdecl]<
                    napi_async_cleanup_hook_handle, napi_status>)GetExport();
                return s_funcs.napi_remove_async_cleanup_hook(remove_handle);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_remove_env_cleanup_hook(
                napi_env env, napi_cleanup_hook fun, nint arg)
            {
                s_funcs.napi_remove_env_cleanup_hook = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_cleanup_hook, nint, napi_status>)GetExport();
                return s_funcs.napi_remove_env_cleanup_hook(env, fun, arg);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_unref_threadsafe_function(
                napi_env env, napi_threadsafe_function func)
            {
                s_funcs.napi_unref_threadsafe_function = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_threadsafe_function, napi_status>)GetExport();
                return s_funcs.napi_unref_threadsafe_function(env, func);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status node_api_get_module_file_name(napi_env env, nint result)
            {
                s_funcs.node_api_get_module_file_name = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.node_api_get_module_file_name(env, result);
            }

            //--------------------------------------------------------------------------------------
            // Embedding APIs
            //--------------------------------------------------------------------------------------

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_await_promise(
                napi_env env, napi_value promise, nint result)
            {
                s_funcs.napi_await_promise = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_value, nint, napi_status>)GetExport();
                return s_funcs.napi_await_promise(env, promise, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_environment(
                napi_platform platform,
                napi_error_message_handler err_handler,
                nint main_script,
                nint result)
            {
                s_funcs.napi_create_environment = (delegate* unmanaged[Cdecl]<
                    napi_platform, napi_error_message_handler, nint, nint, napi_status>)GetExport();
                return s_funcs.napi_create_environment(platform, err_handler, main_script, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_create_platform(
                int argc,
                nint argv,
                int exec_argc,
                nint exec_argv,
                napi_error_message_handler err_handler,
                nint result)
            {
                s_funcs.napi_create_platform = (delegate* unmanaged[Cdecl]<
                    int, nint, int, nint, napi_error_message_handler, nint, napi_status>)
                    GetExport();
                return s_funcs.napi_create_platform(
                    argc, argv, exec_argc, exec_argv, err_handler, result);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_destroy_environment(napi_env env, nint exit_code)
            {
                s_funcs.napi_destroy_environment = (delegate* unmanaged[Cdecl]<
                    napi_env, nint, napi_status>)GetExport();
                return s_funcs.napi_destroy_environment(env, exit_code);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_destroy_platform(napi_platform platform)
            {
                s_funcs.napi_destroy_platform = (delegate* unmanaged[Cdecl]<
                    napi_platform, napi_status>)GetExport();
                return s_funcs.napi_destroy_platform(platform);
            }

            [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
            internal static napi_status napi_run_environment(napi_env env)
            {
                s_funcs.napi_run_environment = (delegate* unmanaged[Cdecl]<
                    napi_env, napi_status>)GetExport();
                return s_funcs.napi_run_environment(env);
            }
        }
    }
}
