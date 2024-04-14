// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.JavaScript.NodeApi.Interop;

/// <summary>
/// Designates a non-static class or static method that initializes the JS module.
/// </summary>
/// <remarks>
/// This attribute may optionally be used once (and only once) within a .NET assembly that is
/// exported to JavaScript, to allow for more control over the module lifetime or exports.
/// <para/>
/// If <see cref="JSModuleAttribute"/> is applied to a class, the class must have a public
/// constructor that takes no parameters, and may implement the <see cref="IDisposable"/>
/// interface. An instance of the class will be constructed when the module is loaded, and disposed
/// when the module is unloaded if it implements <see cref="IDisposable"/>. Public non-static
/// properties and methods on the same module class are automatically exported. Those exports are
/// merged with any additional items in the assembly (other classes, static properties and methods,
/// etc) that are tagged with <see cref="JSExportAttribute"/>.
/// <para/>
/// If <see cref="JSModuleAttribute"/> is applied to a public static method, then that module
/// initialization method must take a single <see cref="JSObject"/> "exports" parameter and must
/// return a <see cref="JSValue"/> that is the resulting value to be exported to JavaScript by the
/// module. In this usage, the initializer has complete control over customizing the module exports,
/// and no usage of <see cref="JSExportAttribute"/> is allowed anywhere in the assembly. The
/// initializer can use <see cref="JSModuleBuilder{T}"/> to build the module object to export.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class JSModuleAttribute : Attribute
{
}
