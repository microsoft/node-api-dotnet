// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Represents a low-level function or method call or callback from JavaScript into .NET.
/// </summary>
/// <param name="args">Provides access to the arguments for the call, along with the `this`
/// argument and an optional context object.</param>
/// <returns>The return value as a JS value.</returns>
public delegate JSValue JSCallback(JSCallbackArgs args);

/// <summary>
/// Represents a low-level void function or method call or callback from JavaScript into .NET.
/// </summary>
/// <param name="args">Provides access to the arguments for the call, along with the `this`
/// argument and an optional context object.</param>
public delegate void JSActionCallback(JSCallbackArgs args);
