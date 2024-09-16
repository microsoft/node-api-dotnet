// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// An exception that was caused by an error thrown by JavaScript code or
/// interactions with JavaScript objects.
/// </summary>
public class JSException : Exception
{
    /// <summary>
    /// Captures the JS stack when an exception propagates outside of the JS thread.
    /// </summary>
    private string? _jsStack;

    /// <summary>
    /// Creates a new instance of <see cref="JSException" /> with an exception message
    /// and optional inner exception.
    /// </summary>
    public JSException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="JSException" /> that wraps the JavaScript
    /// error that caused it.
    /// </summary>
    public JSException(JSError error) : base(error.Message)
    {
        Error = error;
    }

    /// <summary>
    /// Creates a new instance of <see cref="JSException" /> specifically for propagating
    /// an already-thrown JS exception out to another thread.
    /// </summary>
    /// <param name="innerException">Exception that was already thrown from the JS thread.</param>
    /// <remarks>
    /// This constructor must be called while still on the JS thread.
    /// </remarks>
    internal JSException(Exception innerException)
        : this("Exception thrown from JS thread: " + innerException?.Message, innerException)
    {
        JSException? innerJSException = innerException as JSException;
        JSValue? jsError = innerJSException?.Error?.Value;
        if (jsError is not null)
        {
            innerJSException!._jsStack = (string)jsError.Value["stack"];
        }
    }

    /// <summary>
    /// Gets the JavaScript error that caused this exception, or null if the exception
    /// was not caused by a JavaScript error.
    /// </summary>
    public JSError? Error { get; }

    /// <summary>
    /// Gets a stack trace that may include both .NET and JavaScript stack frames.
    /// </summary>
    public override string? StackTrace
    {
        get
        {
            string? jsStack = _jsStack;
            if (jsStack is null)
            {
                JSValue? jsError = Error?.Value;
                if (jsError is not null)
                {
                    jsStack = _jsStack ?? (string)jsError.Value["stack"];
                }
            }

            if (string.IsNullOrEmpty(jsStack))
            {
                // There's no JS stack, so just return the normal .NET stack.
                return base.StackTrace;
            }

            // The first line of the JS stack is the error type name and message,
            // which is redundant when merged with the .NET exception.
            int firstLineEnd = jsStack!.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                jsStack = jsStack.Substring(firstLineEnd + 1);
            }

            // Normalize indentation to 3 spaces, as used by .NET.
            // (JS traces indent with 4 spaces.)
            if (jsStack.StartsWith("    at "))
            {
                jsStack = jsStack.Replace("    at ", "   at ");
            }

            string dotnetStack = base.StackTrace?.TrimStart(s_trimChars) ??
                string.Empty;
            return jsStack + "\n" + dotnetStack;
        }
    }

    private static readonly char[] s_trimChars = new[] { '\r', '\n' };
}
