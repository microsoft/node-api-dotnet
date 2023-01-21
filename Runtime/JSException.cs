using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static NodeApi.JSNativeApi.Interop;
using static NodeApi.JSNativeApi.NodeApiInterop;

namespace NodeApi;

public class JSException : Exception
{
    public override string Message => ErrorInfo.Message;

    public JSErrorInfo ErrorInfo { get; }

    public JSException(JSErrorInfo errorInfo) => ErrorInfo = errorInfo;

    public unsafe JSException(string message)
    {
        ErrorInfo = new JSErrorInfo(message, JSStatus.GenericFailure);
    }


    [DoesNotReturn]
    public static void Fatal(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
        => napi_fatal_error(
            $"{memberName} at {sourceFilePath}:{sourceLineNumber}",
            NAPI_AUTO_LENGTH,
            message,
            NAPI_AUTO_LENGTH);
}

public static partial class JSNativeApi
{
    public static void FatalIfFailed(
        [DoesNotReturnIf(true)] this napi_status status,
        string message = "",
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

        JSException.Fatal(message, memberName, sourceFilePath, sourceLineNumber);
    }
}
