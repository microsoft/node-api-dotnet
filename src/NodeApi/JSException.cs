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
            JSValue? jsError = Error?.Value;
            if (jsError is not null)
            {
                string jsStack = (string)jsError.Value["stack"];

                // The first line of the stack is the error type name and message,
                // which is redundant when merged with the .NET exception.
                int firstLineEnd = jsStack.IndexOf('\n');
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

                // Strip the ThrowIfFailed() line(s) from the .NET stack trace.
                string dotnetStack = base.StackTrace?.TrimStart(s_trimChars) ??
                    string.Empty;
                firstLineEnd = dotnetStack.IndexOf('\n');
                while (firstLineEnd >= 0 && dotnetStack.IndexOf(
                    "." + nameof(NodeApiStatusExtensions.ThrowIfFailed), 0, firstLineEnd) >= 0)
                {
                    dotnetStack = dotnetStack.Substring(firstLineEnd + 1);
                    firstLineEnd = dotnetStack.IndexOf('\n');
                }

                return jsStack + "\n" + dotnetStack;
            }

            return base.StackTrace;
        }
    }

    private static readonly char[] s_trimChars = new[] { '\r', '\n' };
}
