using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static Microsoft.JavaScript.NodeApi.JSNativeApi;
using static Microsoft.JavaScript.NodeApi.JSNativeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

public enum JSErrorType { Error, TypeError, RangeError, SyntaxError }

file record struct JSErrorInfo(string? Message, napi_status Status)
{
    public static unsafe JSErrorInfo GetLastErrorInfo()
    {
        napi_get_last_error_info(
            (napi_env)JSValueScope.Current,
            out nint errorInfoHandle).ThrowIfFailed();
        var errorInfo = (napi_extended_error_info*)errorInfoHandle;
        if (errorInfo == null)
        {
            return new JSErrorInfo(null, napi_status.napi_ok);
        }

        if (errorInfo->error_message != null)
        {
            string message = Encoding.UTF8.GetString(
                MemoryMarshal.CreateReadOnlySpanFromNullTerminated(errorInfo->error_message));
            return new JSErrorInfo(message, errorInfo->error_code);
        }

        return new JSErrorInfo(null, errorInfo->error_code);
    }

}

public struct JSError
{
    private string? _message;
    private readonly JSReference? _errorRef;

    private const string ErrorWrapValue = "4bda9e7e-4913-4dbc-95de-891cbf66598e-errorVal";
    private const string DefaultMessage = "Error in native callback";

    public unsafe JSError(
        string? message = null, JSErrorType errorType = JSErrorType.Error, string? code = null)
    {
        // Fatal error instead of exception to avoid stack overflows
        using var fatalScope = new FatalIfFailedScope();

        // We must retrieve the last error info before doing anything else because
        // doing anything else will replace the last error info.
        JSErrorInfo errorInfo = JSErrorInfo.GetLastErrorInfo();

        // A pending JS exception takes precedence over any internal error status.
        if (IsExceptionPending())
        {
            _message = null;
            _errorRef = CreateErrorReference(GetAndClearLastException());
            return;
        }

        var messageBuilder = new StringBuilder();
        if (!string.IsNullOrEmpty(errorInfo.Message))
        {
            messageBuilder.Append(errorInfo.Message);
        }

        if (!string.IsNullOrEmpty(message))
        {
            if (messageBuilder.Length != 0)
            {
                messageBuilder.Append(": ");
            }
            messageBuilder.Append(message);
        }

        if (messageBuilder.Length == 0)
        {
            messageBuilder.Append(DefaultMessage);
        }

        _message = messageBuilder.ToString();
        _errorRef = CreateErrorReference(errorInfo.Status switch
        {
            napi_status.napi_ok => errorType switch
            {
                JSErrorType.TypeError
                  => JSValue.CreateTypeError(ToJSValue(code), (JSValue)_message),
                JSErrorType.RangeError
                  => JSValue.CreateRangeError(ToJSValue(code), (JSValue)_message),
                JSErrorType.SyntaxError
                  => JSValue.CreateSyntaxError(ToJSValue(code), (JSValue)_message),
                _ => JSValue.CreateError(ToJSValue(code), (JSValue)_message),
            },
            napi_status.napi_object_expected or
            napi_status.napi_string_expected or
            napi_status.napi_boolean_expected or
            napi_status.napi_number_expected
              => JSValue.CreateTypeError(ToJSValue(code), (JSValue)_message),
            _ => JSValue.CreateError(ToJSValue(code), (JSValue)_message),
        });

        static JSValue? ToJSValue(string? value)
            => value is not null ? (JSValue)value : (JSValue?)null;
    }

    public JSError(JSValue? error)
    {
        if (error is null)
            return;

        _errorRef = CreateErrorReference(error.Value);
    }

    public JSError(Exception exception)
    {
        if (exception is JSException jsException)
        {
            JSError? error = jsException.Error;
            if (error.HasValue)
            {
                _message = error.Value._message;
                _errorRef = error.Value._errorRef;
                return;
            }
        }

        var tempError = new JSError(exception.Message);
        _message = tempError._message;
        _errorRef = tempError._errorRef;
    }

    public string Message
    {
        get
        {
            if (_message is null && _errorRef is not null)
            {
                try
                {
                    _message = (string?)_errorRef.GetValue()?["message"];
                }
                catch
                {
                    // Catch all errors here, because this method may not throw.
                }
            }

            return _message ?? DefaultMessage;
        }
    }

    public JSValue Value
    {
        get
        {
            JSValue? error = _errorRef?.GetValue();
            if (error is JSValue jsError)
            {
                if (jsError.TypeOf() != JSValueType.Object)
                {
                    return jsError;
                }

                // We are checking if the object is wrapped
                if (jsError.HasOwnProperty(ErrorWrapValue))
                {
                    return jsError[ErrorWrapValue];
                }

                return jsError;
            }

            return JSValue.Undefined;
        }
    }

    public void ThrowError()
    {
        if (_errorRef is null)
            return;

        using var scope = new JSValueScope(JSValueScopeType.Handle);

        if (IsExceptionPending())
            throw new JSException(new JSError());

        napi_status status = napi_throw((napi_env)JSValueScope.Current, (napi_value)Value);
        if (status == napi_status.napi_ok)
            return;

        if (status == napi_status.napi_pending_exception)
        {
            // The environment must be terminating as we checked earlier and there
            // was no pending exception. In this case continuing will result
            // in a fatal error and there is nothing the author has done incorrectly
            // in their code that is worth flagging through a fatal error
            return;
        }

        throw new JSException(new JSError());
    }

    public static void ThrowError(Exception exception)
        => new JSError(exception).ThrowError();

    [DoesNotReturn]
    public static void Fatal(string message,
                             [CallerMemberName] string memberName = "",
                             [CallerFilePath] string sourceFilePath = "",
                             [CallerLineNumber] int sourceLineNumber = 0)
        => napi_fatal_error($"{memberName} at {sourceFilePath}:{sourceLineNumber}", message);

    private static JSReference CreateErrorReference(JSValue error)
    {
        // Attempting to create a reference on the error object.
        // If it's not a Object/Function/Symbol, this call will return an error status.
        if (JSReference.TryCreateReference(error, isWeak: false, out JSReference? errorRef))
            return errorRef;

        using var fataScope = new FatalIfFailedScope();

        // Wrap error value
        JSValue wrappedErrorObj = JSValue.CreateObject();
        wrappedErrorObj.DefineProperties(
            new JSPropertyDescriptor((JSValue)ErrorWrapValue, value: error));

        return new JSReference(wrappedErrorObj);
    }

    /// <summary>
    /// Converts ThrowIfFailed to FatalIfFailed in that scope.
    /// </summary>
    public ref struct FatalIfFailedScope
    {
        private readonly bool _previousIsFatal = false;
        private bool _isDisposed = false;

        [ThreadStatic] private static bool s_isFatal;

        public static bool IsFatal => s_isFatal;

        public FatalIfFailedScope()
        {
            _previousIsFatal = s_isFatal;
            s_isFatal = true;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            s_isFatal = _previousIsFatal;
        }
    }
}

public static partial class JSNativeApi
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

        JSError.Fatal(message, memberName, sourceFilePath, sourceLineNumber);
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
