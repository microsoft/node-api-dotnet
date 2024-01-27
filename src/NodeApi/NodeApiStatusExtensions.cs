// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

public static class NodeApiStatusExtensions
{
    public static void FatalIfFailed([DoesNotReturnIf(true)] this napi_status status,
                                     string? message = null,
                                     [CallerMemberName] string memberName = "",
                                     [CallerFilePath] string sourceFilePath = "",
                                     [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (status == napi_status.napi_ok)
        {
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            message = status.ToString();
        }

        JSError.Fatal(message!, memberName, sourceFilePath, sourceLineNumber);
    }

    public static void ThrowIfFailed([DoesNotReturnIf(true)] this napi_status status,
                                     [CallerMemberName] string memberName = "",
                                     [CallerFilePath] string sourceFilePath = "",
                                     [CallerLineNumber] int sourceLineNumber = 0)
    {
        if (status == napi_status.napi_ok)
            return;

        if (JSError.FatalIfFailedScope.IsFatal)
            JSError.Fatal(
                "Failed while handling error", memberName, sourceFilePath, sourceLineNumber);

        throw new JSException(
            new JSError($"Error in {memberName} at {sourceFilePath}:{sourceLineNumber}"));
    }

    // Throw if status is not napi_ok. Otherwise, return the provided value.
    // This function helps writing compact wrappers for the interop calls.
    public static T ThrowIfFailed<T>(this napi_status status,
                                     T value,
                                     [CallerMemberName] string memberName = "",
                                     [CallerFilePath] string sourceFilePath = "",
                                     [CallerLineNumber] int sourceLineNumber = 0)
    {
        status.ThrowIfFailed(memberName, sourceFilePath, sourceLineNumber);
        return value;
    }
}

