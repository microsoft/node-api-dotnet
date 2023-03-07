// APIs defined here are implemented by Microsoft.JavaScript.NodeApi.DotNetHost.

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
 * @param dotnetAssemblyFilePath Path to the .NET assembly DLL file.
 * @returns A JS object that represents the loaded assembly; each property of the object
 * is a public type in the assembly.
 */
export declare function load(dotnetAssemblyFilePath: string): Assembly | undefined;
