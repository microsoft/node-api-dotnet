// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// APIs defined here are implemented by Microsoft.JavaScript.NodeApi.DotNetHost.

// This explicit module declaration enables module members to be merged with imported namespaces.
declare module 'node-api-dotnet' {

/**
 * Gets the current .NET runtime version, for example "8.0.1".
 */
export const runtimeVersion: string;

/**
 * Gets the framework monikier corresponding to the current .NET runtime version,
 * for example "net8.0" or "net472".
 */
export const frameworkMoniker: string;

/**
 * Loads a .NET assembly that was built to be a Node API module, using static binding to
 * the APIs the module specifically exports to JS.
 * @param dotnetAssemblyFilePath Path to the .NET assembly DLL file.
 * @returns The JavaScript module exported by the assembly. (Type information for the module
 * may be available in a separate generated type-definitions file.)
 */
export function require(dotnetAssemblyFilePath: string): any;

/**
 * Loads an arbitrary .NET assembly that isn't necessarily designed as a JS module, enabling
 * dynamic invocation of any APIs in the assembly. After loading, types from the assembly are
 * available via namespaces on the main dotnet module.
 * @param assemblyNameOrFilePath Path to the .NET assembly DLL file, or name of a system assembly.
 */
export function load(assemblyNameOrFilePath: string): void;

/**
 * Adds a listener for the `resolving` event, which is raised when a .NET assembly requires
 * an additional dependent assembly to be resolved and loaded. The listener must call `load()`
 * to load the requested assembly file.
 */
export function addListener(
  event: 'resolving',
  listener: (assemblyName: string, assemblyVersion: string) => void,
): void;

/**
 * Removes a listener for the `resolving` event.
 */
export function removeListener(
  event: 'resolving',
  listener: (assemblyName: string, assemblyVersion: string) => void,
): void;

}
