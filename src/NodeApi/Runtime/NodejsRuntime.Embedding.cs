// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.JavaScript.NodeApi.Runtime;

// Imports embedding APIs from libnode.
public unsafe partial class NodejsRuntime
{
#pragma warning disable IDE1006 // Naming: missing prefix '_'

    private delegate* unmanaged[Cdecl]<
        int, nint, int, nint, napi_error_message_handler, nint, napi_status>
        napi_create_platform;

    public override napi_status CreatePlatform(
        string[]? args,
        string[]? execArgs,
        Action<string>? errorHandler,
        out napi_platform result)
    {
        napi_error_message_handler native_error_handler = errorHandler == null ? default :
            new((byte* error) =>
            {
                string? message = PtrToStringUTF8(error);
                if (message is not null) errorHandler(message);
            });

        nint args_ptr = StringsToHGlobalUtf8(args, out int args_count);
        nint exec_args_ptr = StringsToHGlobalUtf8(execArgs, out int exec_args_count);

        try
        {
            result = default;
            fixed (napi_platform* result_ptr = &result)
            {
                if (napi_create_platform == null)
                {
                    napi_create_platform = (delegate* unmanaged[Cdecl]<
                        int, nint, int, nint, napi_error_message_handler, nint, napi_status>)
                        Import(nameof(napi_create_platform));
                }

                return napi_create_platform(
                    args_count,
                    args_ptr,
                    exec_args_count,
                    exec_args_ptr,
                    native_error_handler,
                    (nint)result_ptr);
            }
        }
        finally
        {
            FreeStringsHGlobal(args_ptr, args_count);
            FreeStringsHGlobal(exec_args_ptr, exec_args_count);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_platform, napi_status>
        napi_destroy_platform;

    public override napi_status DestroyPlatform(napi_platform platform)
    {
        return Import(ref napi_destroy_platform)(platform);
    }

    private delegate* unmanaged[Cdecl]<
        napi_platform, napi_error_message_handler, nint, nint, napi_status>
        napi_create_environment;

    public override napi_status CreateEnvironment(
        napi_platform platform,
        Action<string>? errorHandler,
        string? mainScript,
        out napi_env result)
    {
        napi_error_message_handler native_error_handler = errorHandler == null ? default :
            new((byte* error) =>
            {
                string? message = PtrToStringUTF8(error);
                if (message is not null) errorHandler(message);
            });

        nint main_script_ptr = StringToHGlobalUtf8(mainScript);

        try
        {
            fixed (napi_env* result_ptr = &result)
            {
                return Import(ref napi_create_environment)(
                    platform, native_error_handler, main_script_ptr, (nint)result_ptr);
            }
        }
        finally
        {
            if (main_script_ptr != default) Marshal.FreeHGlobal(main_script_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, nint, napi_status>
        napi_destroy_environment;

    public override napi_status DestroyEnvironment(napi_env env, out int exitCode)
    {
        fixed (int* exit_code_ptr = &exitCode)
        {
            return Import(ref napi_destroy_environment)(env, (nint)exit_code_ptr);
        }
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_status>
        napi_run_environment;

    public override napi_status RunEnvironment(napi_env env)
    {
        return Import(ref napi_run_environment)(env);
    }

    private delegate* unmanaged[Cdecl]<napi_env, napi_value, nint, napi_status>
        napi_await_promise;

    public override napi_status AwaitPromise(
        napi_env env, napi_value promise, out napi_value result)
    {
        result = default;
        fixed (napi_value* result_ptr = &result)
        {
            return Import(ref napi_await_promise)(env, promise, (nint)result_ptr);
        }
    }

#pragma warning restore IDE1006
}
