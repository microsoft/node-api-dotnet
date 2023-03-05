
// APIs defined here are implemented by NodeApi.DotNetHost.

export interface Assembly {
  // An assembly object can be incexed by type full name, or (if unambiguous) simple type name.
  [typeName: string]: any;
}

export declare function require(dotnetAssemblyFilePath: string): any;

export declare function load(dotnetAssemblyFilePath: string): Assembly?;
