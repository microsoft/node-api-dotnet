// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Indicates a class or struct is imported from JavaScript.
/// </summary>
[AttributeUsage(
    AttributeTargets.Interface |
    AttributeTargets.Struct
)]
public sealed class JSImportAttribute : Attribute
{
}
