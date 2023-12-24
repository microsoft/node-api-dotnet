// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Microsoft.JavaScript.NodeApi.Runtime;
// This part of the class includes the constructor and private helper methods.
// See the other parts of this class for the actual imported APIs.
[SuppressUnmanagedCodeSecurity]
public unsafe partial class NodejsRuntime : JSRuntime
{
    private readonly nint _libraryHandle;

    public NodejsRuntime(nint libraryHandle = default)
    {
        _libraryHandle = libraryHandle != default ?
            libraryHandle : NativeLibrary.GetMainProgramHandle();
    }

    public override bool IsAvailable(string functionName)
        => NativeLibrary.TryGetExport(_libraryHandle, functionName, out _);

    private nint Import(string functionName)
        => NativeLibrary.GetExport(_libraryHandle, functionName);

    // Unmanaged delegate types cannot be used as generic type parameters, which is unfortunate
    // since that would have allowed a single generic Import() method. Instead there are separate
    // generic helpers for different numbers of parameters. These assume all imported functions have
    // an environment (napi_env or napi_platform) as the first parameter and return napi_status.

    private delegate* unmanaged[Cdecl]<E, napi_status> Import<E>(
        ref delegate* unmanaged[Cdecl]<E, napi_status> function,
        [CallerArgumentExpression(nameof(function))] string functionName = "")
    {
        if (function == null)
        {
            function = (delegate* unmanaged[Cdecl]<E, napi_status>)Import(functionName);
        }
        return function;
    }

    private delegate* unmanaged[Cdecl]<E, T1, napi_status> Import<E, T1>(
        ref delegate* unmanaged[Cdecl]<E, T1, napi_status> function,
        [CallerArgumentExpression(nameof(function))] string functionName = "")
    {
        if (function == null)
        {
            function = (delegate* unmanaged[Cdecl]<E, T1, napi_status>)Import(functionName);
        }
        return function;
    }

    private delegate* unmanaged[Cdecl]<E, T1, T2, napi_status> Import<E, T1, T2>(
        ref delegate* unmanaged[Cdecl]<E, T1, T2, napi_status> function,
        [CallerArgumentExpression(nameof(function))] string functionName = "")
    {
        if (function == null)
        {
            function = (delegate* unmanaged[Cdecl]<E, T1, T2, napi_status>)Import(functionName);
        }
        return function;
    }

    private delegate* unmanaged[Cdecl]<E, T1, T2, T3, napi_status> Import<E, T1, T2, T3>(
        ref delegate* unmanaged[Cdecl]<E, T1, T2, T3, napi_status> function,
        [CallerArgumentExpression(nameof(function))] string functionName = "")
    {
        if (function == null)
        {
            function = (delegate* unmanaged[Cdecl]<E, T1, T2, T3, napi_status>)Import(functionName);
        }
        return function;
    }

    private delegate* unmanaged[Cdecl]<E, T1, T2, T3, T4, napi_status> Import<E, T1, T2, T3, T4>(
        ref delegate* unmanaged[Cdecl]<E, T1, T2, T3, T4, napi_status> function,
        [CallerArgumentExpression(nameof(function))] string functionName = "")
    {
        if (function == null)
        {
            function = (delegate* unmanaged[Cdecl]<E, T1, T2, T3, T4, napi_status>)
                Import(functionName);
        }
        return function;
    }

    private delegate* unmanaged[Cdecl]<E, T1, T2, T3, T4, T5, napi_status>
    Import<E, T1, T2, T3, T4, T5>(
        ref delegate* unmanaged[Cdecl]<E, T1, T2, T3, T4, T5, napi_status> function,
        [CallerArgumentExpression(nameof(function))] string functionName = "")
    {
        if (function == null)
        {
            function = (delegate* unmanaged[Cdecl]<E, T1, T2, T3, T4, T5, napi_status>)
                Import(functionName);
        }
        return function;
    }

    private static unsafe string? PtrToStringUTF8(byte* ptr)
    {
        if (ptr == null) return null;
        int length = 0;
        while (ptr[length] != 0) length++;
        return Encoding.UTF8.GetString(ptr, length);
    }

    private static nint StringToHGlobalUtf8(string? s)
    {
        if (s == null) return default;
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        nint ptr = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        Marshal.WriteByte(ptr, bytes.Length, 0);
        return ptr;
    }

    private static nint StringsToHGlobalUtf8(string?[]? s, out int count)
    {
        nint array_ptr = default;
        count = 0;

        if (s != null)
        {
            count = s.Length;
            array_ptr = Marshal.AllocHGlobal(count * sizeof(nint));
            for (int i = 0; i < count; i++)
            {
                nint ptr = StringToHGlobalUtf8(s[i]);
                Marshal.WriteIntPtr(array_ptr + i * sizeof(nint), ptr);
            }
        }

        return array_ptr;
    }

    private static void FreeStringsHGlobal(nint array_ptr, int count)
    {
        if (array_ptr != default)
        {
            for (int i = 0; i < count; i++)
            {
                nint ptr = Marshal.ReadIntPtr(array_ptr + i * sizeof(nint));
                if (ptr != default)
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }

            Marshal.FreeHGlobal(array_ptr);
        }
    }
}
