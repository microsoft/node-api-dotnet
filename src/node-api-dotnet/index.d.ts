// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/**
 * Use the `node-api-dotnet` package to load .NET assemblies into a Node.js application and
 * call public APIs defined in the assemblies.
 * ::: code-group
 * ```JavaScript [ES (TS or JS)]
 * import dotnet from 'node-api-dotnet';
 * ```
 * ```TypeScript [CommonJS (TS)]
 * import * as dotnet from 'node-api-dotnet';
 * ```
 * ```JavaScript [CommonJS (JS)]
 * const dotnet = require('node-api-dotnet');
 * ```
 * :::
 * To load a specific version of .NET, append the target framework moniker to the package name:
 * ::: code-group
 * ```JavaScript [ES (TS or JS)]
 * import dotnet from 'node-api-dotnet/net6.0';
 * ```
 * ```TypeScript [CommonJS (TS)]
 * import * as dotnet from 'node-api-dotnet/net6.0';
 * ```
 * ```JavaScript [CommonJS (JS)]
 * const dotnet = require('node-api-dotnet/net6.0');
 * ```
 * :::
 * Currently the supported target frameworks are `net472`, `net6.0`, and `net8.0`.
 * @module node-api-dotnet
 */
declare module 'node-api-dotnet' {
// APIs defined here are implemented by Microsoft.JavaScript.NodeApi.DotNetHost.
// The explicit module declaration enables module members to be merged with imported namespaces.

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
 * @remarks The .NET assembly must use `[JSExport]` attributes to export selected types
 * and/or members to JavaScript. These exports _do not_ use .NET namespaces.
 */
export function require(dotnetAssemblyFilePath: string): any;

/**
 * Loads an arbitrary .NET assembly that isn't necessarily designed as a JS module, enabling
 * dynamic invocation of any APIs in the assembly. After loading, types from the assembly are
 * available via namespaces on the main dotnet module.
 * @param assemblyNameOrFilePath Path to the .NET assembly DLL file, or name of a system assembly.
 * @remarks After loading an assembly, types in the assembly are merged into the .NET
 * namespace hierarchy, with top-level namespaces available as properties on the .NET module.
 * For example, if the assembly defines a type `Contoso.Business.Component`, it can be accessed as
 * `dotnet.Contoso.Business.Component`. (.NET core library types can be accessed the same way, for
 * example `dotnet.System.Console`.)
 */
export function load(assemblyNameOrFilePath: string): void;

/**
 * Adds a listener for the `resolving` event, which is raised when a .NET assembly requires
 * an additional dependent assembly to be resolved and loaded. The listener may call `resolve()`
 * to load the requested assembly from a resolved file path. If the listener does not call
 * `resolve()`, the runtime will then attempt to resolve the assembly by searching in the same
 * application directory as other already-loaded assemblies, if there were any.
 */
export function addListener(
  event: 'resolving',
  /**
   * Resolving event listener funciton to be invokved when a .NET assembly is being resolved.
   * @param assemblyName Name of the assembly to be resolved.
   * @param assemblyVersion Version of the assembly to be resolved.
   * @param resolve Callback to invoke with the full path to the resolved assembly file.
   */
  listener: (assemblyName: string, assemblyVersion: string, resolve: (string) => void) => void,
): void;

/**
 * Removes a listener for the `resolving` event.
 */
export function removeListener(
  event: 'resolving',
  listener: (assemblyName: string, assemblyVersion: string) => void,
): void;

}
