// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// APIs defined here are implemented by Microsoft.JavaScript.NodeApi.DotNetHost.

// This explicit module declaration enables module members to be merged with imported namespaces.
declare module 'node-api-dotnet' {

/**
 * A .NET assembly that was loaded dynamically by the .NET host. Types within the assembly
 * can be accessed via properties on the assembly object.
 */
export interface Assembly {
  /**
   * Gets a type within the assembly.
   * @param typeName Either a type full name or (if unambiguous) simple type name.
   */
  [typeName: string]: any;
}

/**
 * Loads a .NET assembly that was built to be a Node API module, using static binding to
 * the APIs the module specifically exports to JS.
 * @param dotnetAssemblyFilePath Path to the .NET assembly DLL file.
 * @returns The JavaScript module exported by the assembly. (Type information for the module
 * may be available in a separate generated type-definitions file.)
 */
export declare function require(dotnetAssemblyFilePath: string): any;

/**
 * Loads an arbitrary .NET assembly that isn't necessarily designed as a JS module,
 * enabling dynamic invocation of any APIs in the assembly.
 * @param dotnetAssemblyFilePath Path to the .NET assembly DLL file to load.
 * @returns A JS object that represents the loaded assembly; each property of the object
 * is a public type in the assembly.
 */
export declare function load(dotnetAssemblyFilePath: string): Assembly | undefined;

/**
 * Adds a listener for the `resolving` event, which is raised when a .NET assembly requires
 * an additional dependent assembly to be resolved and loaded. The listener must call `load()`
 * to load the requested assembly file.
 */
export declare function addListener(
  event: 'resolving',
  listener: (assemblyName: string, assemblyVersion: string) => void,
): this;

/**
 * Removes a listener for the `resolving` event.
 */
export declare function removeListener(
  event: 'resolving',
  listener: (assemblyName: string, assemblyVersion: string) => void,
): this;

}