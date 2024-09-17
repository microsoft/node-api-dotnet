// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Provides information about, and control over, the current Node.js process.
/// </summary>
/// <remarks>
/// These APIs are primarily meant for use with <see cref="NodeWorker"/> threads, for which the
/// process APIs are overridden to be specific to the worker thread context.
/// </remarks>
public static class NodeProcess
{
    // Note the Import() function caches a reference to the imported module.
    private static JSValue ProcessModule => JSRuntimeContext.Current.Import("node:process");

    /// <summary>
    /// Gets or sets the command-line arguments for the current process or worker thread.
    /// The first argument (element 0) is the executable path; the second (index 1) is the
    /// path to the main script file.
    /// </summary>
    public static IReadOnlyList<string> Argv
        => ((JSArray)ProcessModule["argv"]).AsReadOnlyList<string>((item) => (string)item);

    /// <summary>
    /// Gets a dictionary that allows getting or setting environment variables for the current
    /// process or worker thread.
    /// </summary>
    public static IDictionary<string, string> Env
        => ((JSObject)ProcessModule["env"]).AsDictionary(
            (value) => (string)value, (value) => (JSValue)value);

    /// <summary>
    /// Gets a stream connected to the current process or worker thread <c>stdin</c>.
    /// </summary>
    public static Stream Stdin => (NodeStream)ProcessModule["stdin"];

    /// <summary>
    /// Gets a stream connected to the current process or worker thread <c>stdout</c>.
    /// </summary>
    public static Stream Stdout => (NodeStream)ProcessModule["stdout"];

    /// <summary>
    /// Gets a stream connected to the current process or worker thread <c>stderr</c>.
    /// </summary>
    public static Stream StdErr => (NodeStream)ProcessModule["stderr"];

    /// <summary>
    /// Exits the current process or worker thread.
    /// </summary>
    /// <param name="exitCode"></param>
    public static void Exit(int exitCode)
    {
        ProcessModule.CallMethod("exit", exitCode);
    }
}
