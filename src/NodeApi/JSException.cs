// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// An exception that was caused by an error thrown by JavaScript code or
/// interactions with JavaScript objects.
/// </summary>
public class JSException : Exception
{
    /// <summary>
    /// Matches source file and line number references in JS-style stack traces.
    /// Used for reformatting JS stack traces in .NET style.
    /// </summary>
    private static readonly Regex s_jsSourceLineRefRegex =
        new(@" \(((?:[A-Za-z]:)?[^:\)]+):(\d+)(:\d+)?\)$");

    /// <summary>
    /// Captures the JS stack to ensure it remains available when the exception propagates
    /// outside of the JS thread.
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
        _jsStack = GetJSErrorStack(error);

        // Clear the JS error state when re-throwing as a .NET exception.
        JSError.GetAndClearLastException();
    }

    /// <summary>
    /// Creates a new instance of <see cref="JSException" /> specifically for propagating
    /// an already-thrown JS exception out to another thread.
    /// </summary>
    /// <param name="innerException">Exception that was already thrown from the JS thread.</param>
    internal JSException(Exception innerException)
        : this("Exception thrown from JS thread: " + innerException?.Message, innerException)
    {
    }

    /// <summary>
    /// Gets the JS stack from a JS Error object.
    /// </summary>
    /// <remarks>
    /// The JS stack is saved as a string when constructing a <see cref="JSException" />, in case
    /// the exception is passed to another (non-JS) thread where the JS Error object (and stack)
    /// can no longer be accessed.
    /// </remarks>
    private static string? GetJSErrorStack(JSError? error)
    {
        var jsStack = error != null ? (string)error.Value.Value["stack"] : null;

        if (jsStack != null)
        {
            // Get the depth of the JS stack up to the call into .NET (if any), and remove it from
            // the current JS error stack. This way if/when the exception propagates back to JS,
            // the full stack will be merged correctly with no duplicate frames.
            var jsStackObject = JSError.CaptureStackTrace();
            var baseJsStack = (string)jsStackObject["stack"] ?? string.Empty;
            var stackDepth = baseJsStack.Count((c) => c == '\n');
            if (stackDepth > 0)
            {
                var lines = jsStack.Split('\n');
                jsStack = string.Join("\n", lines.Take(Math.Max(0, lines.Length - stackDepth)));
            }
        }

        return jsStack;
    }

    /// <summary>
    /// Gets the JavaScript error that caused this exception, or null if the exception
    /// was not caused by a JavaScript error.
    /// </summary>
    /// <remarks>
    /// The Error object can only be accessed from the JS thread.
    /// </remarks>
    public JSError? Error { get; }

    /// <summary>
    /// Gets a stack trace that may include both .NET and JavaScript stack frames.
    /// </summary>
    public override string? StackTrace
    {
        get
        {
            string? jsStack = _jsStack;
            if (string.IsNullOrEmpty(jsStack))
            {
                // There's no JS stack, so just return the normal .NET stack.
                return base.StackTrace;
            }

            IEnumerable<string> jsStackLines = jsStack!.Split('\n');

            // The first line of the JS stack is the error type name and message,
            // which is redundant when merged with the .NET exception.
            if (jsStackLines.Count() > 0)
            {
                jsStackLines = jsStackLines.Skip(1);
            }

            IEnumerable<string> dotnetStackLines = (base.StackTrace ?? string.Empty).Split('\n');
            return FormatStack(jsStackLines.Concat(CleanupStack(dotnetStackLines)));
        }
    }

    /// <summary>
    /// List of stack frames which will be hidden when formatting combined .NET + JS stack traces,
    /// because they are related to internal .NET/JS interop and are generally not useful when
    /// debugging applications.
    /// </summary>
    /// <remarks>
    /// Some of the methods listed here may be tagged with [StackTraceHidden], but that attribute
    /// doesn't work in some circumstances (and doesn't work at all on .NET Framework).
    /// </remarks>
    private static readonly string[] s_hideFromStackPrefixes =
    [
        $"   at {typeof(NodeApiStatusExtensions).FullName}.{nameof(NodeApiStatusExtensions.ThrowIfFailed)}(",
        $"   at {typeof(JSThreadSafeFunction).FullName}.CustomCallJS(",
        $"   at {typeof(JSThreadSafeFunction).FullName}.DefaultCallJS(",
        $"   at {typeof(JSValue).FullName}.InvokeCallback[TDescriptor](",
        $"   at {typeof(JSValue).FullName}.{nameof(JSValue.Call)}(",
        $"   at {typeof(JSValue).FullName}.{nameof(JSValue.CallAsConstructor)}(",
        $"   at {typeof(JSValue).FullName}.{nameof(JSValue.CallMethod)}(",
        $"   at {typeof(JSFunction).FullName}.{nameof(JSFunction.Call)}(",
        $"   at {typeof(JSFunction).FullName}.{nameof(JSFunction.CallAsConstructor)}(",
        $"   at {typeof(JSFunction).FullName}.{nameof(JSFunction.CallAsStatic)}(",
        $"   at {typeof(JSFunction).FullName}.<>", // JSFunction constructor adapter lambdas
    ];

    /// <summary>
    /// Removes frames related to .NET/JS interop from a list of stack lines.
    /// </summary>
    internal static IEnumerable<string> CleanupStack(IEnumerable<string> stackLines)
    {
        return stackLines.Where((line) => !s_hideFromStackPrefixes.Any((h) => line.StartsWith(h)));
    }

    /// <summary>
    /// Formats a stack trace (which may include JS frames) in .NET style.
    /// </summary>
    private static string FormatStack(IEnumerable<string> stackLines)
    {
        stackLines = stackLines.Select((line) => line.TrimEnd()).Where((line) => line.Length > 0);

        // Normalize indentation to 3 spaces, as used by .NET.
        // (JS traces indent with 4 spaces.)
        stackLines = stackLines
            .Select((line) => line.StartsWith("    at ") ? line.Substring(1) : line);

        // Format JS source file and line number references in the .NET style.
        stackLines = stackLines.Select((line) =>
        {
            var match = s_jsSourceLineRefRegex.Match(line);
            if (!match.Success)
            {
                return line;
            }

            string sourceFile = match.Groups[1].Value;
            string lineNum = match.Groups[2].Value;
            return line.Substring(0, match.Index) + $" in {sourceFile}:line {lineNum}";
        });

        return string.Join("\n", stackLines);
    }
}
