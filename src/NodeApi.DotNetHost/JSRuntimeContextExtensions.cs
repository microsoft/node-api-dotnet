// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.JavaScript.NodeApi.Interop;
using Microsoft.JavaScript.NodeApi.Runtime;

namespace Microsoft.JavaScript.NodeApi.DotNetHost;

/// <summary>
/// Extension methods to support importing JavaScript types at runtime.
/// </summary>
public static class JSRuntimeContextExtensions
{
    /// <summary>
    /// Imports a module or module property from JavaScript and converts it to an interface.
    /// </summary>
    /// <typeparam name="T">.NET type that the imported JS value will be marshalled to.</typeparam>
    /// <param name="module">Name of the module being imported, or null to import a
    /// global property. This is equivalent to the value provided to <c>import</c> or
    /// <c>require()</c> in JavaScript. Required if <paramref name="property"/> is null.</param>
    /// <param name="property">Name of a property on the module (or global), or null to import
    /// the module object. Required if <paramref name="module"/> is null.</param>
    /// <param name="marshaller">JS marshaller instance to use to convert the imported value
    /// to a .NET type.</param>
    /// <returns>The imported value, marshalled to the specified .NET type.</returns>
    /// <exception cref="ArgumentNullException">Both <paramref cref="module" /> and
    /// <paramref cref="property" /> are null.</exception>
    public static T Import<T>(
        this JSRuntimeContext runtimeContext,
        string? module,
        string? property,
        bool esModule,
        JSMarshaller marshaller)
    {
        if (marshaller == null) throw new ArgumentNullException(nameof(marshaller));

        JSValue jsValue = runtimeContext.Import(module, property, esModule);
        return marshaller.FromJS<T>(jsValue);
    }

    /// <summary>
    /// Imports a module or module property from JavaScript and converts it to an interface.
    /// </summary>
    /// <typeparam name="T">.NET type that the imported JS value will be marshalled to.</typeparam>
    /// <param name="module">Name of the module being imported, or null to import a
    /// global property. This is equivalent to the value provided to <c>import</c> or
    /// <c>require()</c> in JavaScript. Required if <paramref name="property"/> is null.</param>
    /// <param name="property">Name of a property on the module (or global), or null to import
    /// the module object. Required if <paramref name="module"/> is null.</param>
    /// <param name="marshaller">JS marshaller instance to use to convert the imported value
    /// to a .NET type.</param>
    /// <returns>The imported value, marshalled to the specified .NET type.</returns>
    /// <exception cref="ArgumentNullException">Both <paramref cref="module" /> and
    /// <paramref cref="property" /> are null.</exception>
    public static T Import<T>(
        this NodejsEmbeddingThreadRuntime nodejs,
        string? module,
        string? property,
        bool esModule,
        JSMarshaller marshaller)
    {
        if (marshaller == null) throw new ArgumentNullException(nameof(marshaller));

        JSValueScope scope = nodejs;
        return scope.RuntimeContext.Import<T>(module, property, esModule, marshaller);
    }

    // TODO: ImportAsync()
}
