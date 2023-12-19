// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using static Microsoft.JavaScript.NodeApi.JSNativeApi;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

public enum JSErrorType { Error, TypeError, RangeError, SyntaxError }

internal record struct JSErrorInfo(string? Message, napi_status Status)
{
    public static unsafe JSErrorInfo GetLastErrorInfo()
    {
        JSValueScope currentScope = JSValueScope.Current;
        currentScope.Runtime.GetLastErrorInfo(
            (napi_env)currentScope,
            out napi_extended_error_info? errorInfo).ThrowIfFailed();
        if (errorInfo == null)
        {
            return new JSErrorInfo(null, napi_status.napi_ok);
        }

        if (errorInfo.Value.error_message != null)
        {
#if NETFRAMEWORK
            string message = PtrToStringUTF8(errorInfo.Value.error_message)!;
#else
            string message = Marshal.PtrToStringUTF8((nint)errorInfo.Value.error_message)!;
#endif
            return new JSErrorInfo(message, errorInfo.Value.error_code);
        }

        return new JSErrorInfo(null, errorInfo.Value.error_code);
    }

#if NETFRAMEWORK
    private static unsafe string? PtrToStringUTF8(byte* ptr)
    {
        if (ptr == null) return null;
        int length = 0;
        while (ptr[length] != 0) length++;
        return Encoding.UTF8.GetString(ptr, length);
    }
#endif
}

public struct JSError
{
    private string? _message = null;
    private readonly JSReference? _errorRef = null;

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
        if (JSValue.IsExceptionPending())
        {
            _message = null;
            _errorRef = CreateErrorReference(JSValue.GetAndClearLastException());
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

    public readonly JSValue Value
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

    public readonly void ThrowError()
    {
        if (_errorRef is null)
            return;

        using var scope = new JSValueScope(JSValueScopeType.Handle);

        if (JSValue.IsExceptionPending())
            throw new JSException(new JSError());

        napi_status status = scope.Runtime.Throw(
            (napi_env)JSValueScope.Current, (napi_value)Value);
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

        throw new JSException("Failed to throw JS Error. Status: " + status);
    }

    /// <summary>
    /// Throws a JS error for a .NET exception.
    /// </summary>
    /// <remarks>
    /// Requires a current <see cref="JSValueScope" />, but does NOT require a current
    /// <see cref="Interop.JSRuntimeContext" />, so it can be safely used from "no-context"
    /// callbacks.
    /// </remarks>
    public static void ThrowError(Exception exception)
    {
        // Do not construct a JSError object here, because that would require a runtime context.

        string message = (exception as TargetInvocationException)?.InnerException?.Message
            ?? exception.Message;

        // If the exception is a JSException for an error value, throw that error value;
        // otherwise construct a new error value from the exception message.
        JSValue error = (exception as JSException)?.Error?.Value ??
            JSValue.CreateError(code: null, (JSValue)message);

        // When running on V8, the `Error.captureStackTrace()` function and `Error.stack` property
        // can be used to add the .NET stack info to the JS error stack.
        JSValue captureStackTrace = JSValue.Global["Error"]["captureStackTrace"];
        if (captureStackTrace.IsFunction())
        {
            // Capture the stack trace of the .NET exception, which will be combined with
            // the JS stack trace when requested.
            JSValue dotnetStack = exception.StackTrace?.Replace("\r", string.Empty) ?? string.Empty;

            // Capture the current JS stack trace as an object.
            // Defer formatting the stack as a string until requested.
            JSObject jsStack = new();
            captureStackTrace.Call(default, jsStack);

            // Override the `stack` property of the JS Error object, and add private
            // properties that the overridden property getter uses to construct the stack.
            error.DefineProperties(
                JSPropertyDescriptor.Accessor(
                    "stack", GetErrorStack, setter: null, JSPropertyAttributes.DefaultProperty),
                JSPropertyDescriptor.ForValue("__dotnetStack", dotnetStack),
                JSPropertyDescriptor.ForValue("__jsStack", jsStack));
        }

        napi_status status = error.Scope.Runtime.Throw(
            (napi_env)JSValueScope.Current, (napi_value)error);

        if (status != napi_status.napi_ok && status != napi_status.napi_pending_exception)
        {
            throw new JSException(
                $"Failed to throw JS Error. Status: {status}\n{exception.Message}");
        }
    }

    /// <summary>
    /// Gets a JS error stack trace that also includes a .NET stack trace,
    /// when the error was thrown via <see cref="ThrowError(Exception)"/>
    /// </summary>
    private static JSValue GetErrorStack(JSCallbackArgs args)
    {
        // Get the error type name and message from the current object.
        string name = (string)args.ThisArg["constructor"]["name"];
        string message = (string)args.ThisArg["message"];

        // Get the separate .NET and JS stacks that were stashed by `ThrowError()`.
        string dotnetStack = (string)args.ThisArg["__dotnetStack"];
        string jsStack = (string)args.ThisArg["__jsStack"]["stack"];

        // The first line is the error type name which was not captured on the private stack object.
        int firstLineEnd = jsStack.IndexOf('\n');
        if (firstLineEnd >= 0)
        {
            jsStack = jsStack.Substring(firstLineEnd + 1);
        }

        // Normalize indentation to 4 spaces, as used by JS. (.NET traces indent with 3 spaces.)
        if (jsStack.StartsWith("    at "))
        {
            dotnetStack = dotnetStack.Replace("   at ", "    at ");
        }

        return $"{name}: {message}\n{dotnetStack}\n{jsStack}";
    }

    [DoesNotReturn]
    public static void Fatal(string message,
                             [CallerMemberName] string memberName = "",
                             [CallerFilePath] string sourceFilePath = "",
                             [CallerLineNumber] int sourceLineNumber = 0)
        => JSValueScope.CurrentRuntime.FatalError(
            $"{memberName} at {sourceFilePath}:{sourceLineNumber}", message);

    private static JSReference CreateErrorReference(JSValue error)
    {
        // Attempting to create a reference on the error object.
        // If it's not a Object/Function/Symbol, this call will return an error status.
        if (JSReference.TryCreateReference(error, isWeak: false, out JSReference? errorRef))
        {
            return errorRef!;
        }

        using var fataScope = new FatalIfFailedScope();

        // Wrap error value
        JSValue wrappedErrorObj = JSValue.CreateObject();
        wrappedErrorObj.DefineProperties(
            new JSPropertyDescriptor(ErrorWrapValue, value: error));

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

public static class JSNativeApi
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
