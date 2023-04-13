// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtimes;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Extension methods to support importing JavaScript types at runtime.
/// </summary>
public static class JSRuntimeContextExtensions
{
    /// <summary>
    /// The marshaller instance can be static because it does not hold any JS values,
    /// only expressions and delegates generated from reflection.
    /// </summary>
    private static readonly JSMarshaller s_marshaller = new();

    /// <summary>
    /// Imports a module or module property from JavaScript and converts it to an interface.
    /// </summary>
    /// <typeparam name="T">Type of the value being imported.</typeparam>
    /// <param name="module">Name of the module being imported, or null to import a
    /// global property. This is equivalent to the value provided to <c>import</c> or
    /// <c>require()</c> in JavaScript. Required if <paramref name="property"/> is null.</param>
    /// <param name="property">Name of a property on the module (or global), or null to import
    /// the module object. Required if <paramref name="module"/> is null.</param>
    /// <returns>The imported value.</returns>
    /// <exception cref="ArgumentNullException">Both <paramref cref="module" /> and
    /// <paramref cref="property" /> are null.</exception>
    public static T Import<T>(
        this JSRuntimeContext runtimeContext,
        string? module,
        string? property)
    {
        JSValue jsValue = runtimeContext.Import(module, property);
        return s_marshaller.To<T>(jsValue);
    }

    /// <summary>
    /// Imports a module or module property from JavaScript and converts it to an interface.
    /// </summary>
    /// <typeparam name="T">Type of the value being imported.</typeparam>
    /// <param name="module">Name of the module being imported, or null to import a
    /// global property. This is equivalent to the value provided to <c>import</c> or
    /// <c>require()</c> in JavaScript. Required if <paramref name="property"/> is null.</param>
    /// <param name="property">Name of a property on the module (or global), or null to import
    /// the module object. Required if <paramref name="module"/> is null.</param>
    /// <returns>The imported value.</returns>
    /// <exception cref="ArgumentNullException">Both <paramref cref="module" /> and
    /// <paramref cref="property" /> are null.</exception>
    public static T Import<T>(
        this NodejsEnvironment nodejs,
        string? module,
        string? property)
    {
        JSValueScope scope = nodejs;
        return scope.RuntimeContext.Import<T>(module, property);
    }
}
