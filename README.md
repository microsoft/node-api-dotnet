## Build Node.js addons in .NET

**Status: Experimental**

This library uses a combination of [C# Source Generators](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview) and [.NET Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/) to produce Node.js native addons written in C# that _do not depend on the .NET runtime_.

### Notes
 - [.NET 7 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) or later is required at build time because this project relies on recent improvements to .NET native AOT capabilities.

 - Tested on Windows and Ubuntu so far. It should work on any platform supported by .NET Native AOT.

 - The built C# `.node` native library is a single file that is somewhat large due to static-linking the necessary .NET AOT libraries. The minimal example is about 3 MB on Windows and 13 MB on Linux (x64 release builds).

### C# example code
```C#
/// <summary>
/// The [Module] attribute causes the class's public instance properties and methods to be
/// exported as a Node.js addon module.
/// </summary>
[NodeApi.Module]
public class Example
{
	private string value;

	/// <summary>
	/// A singleton instance of the class is instantiated when the module is loaded.
	/// </summary>
	public Example() {
		...
	}

	public NodeApi.Value ExampleProperty
	{
		get => NodeApi.String.From(this.value);
		set => this.value = value.As<String>();
	}

	public NodeApi.Value ExampleMethod(NodeApi.Value[] args)
	{
		...
	}

	/// <summary>
	/// Export additional classes from the module by declaring public properties of type `Type`.
	/// </summary>
	public Type Another => typeof(Another);
}

/// <summary>
/// Additional classes can export both static and instance properties and methods.
/// </summary>
public class Another
{
	public Another(NodeApi.Value[] args) { ... }
	public NodeApi.Value StaticProperty { get { ... } }
	public NodeApi.Value InstanceProperty { get}
	public static Value StaticMethod(Value[] args) { ... }
	public Value InstanceMethod(Value[] args) { ... }
}
```

Currently all properties and methods must explicitly convert to/from the `NodeApi.Value` type, as in the `ExampleProperty` above. In the future those conversions can be automated by the source generator, so that the C# code is able to work directly with more natural types.

### JavaScript example code
```JavaScript
// Load the compiled addon binary corresponding to the current platform.
const runtimeIdentifier = 'win-x64';
const example = require(`${runtimeIdentifier}/example.node`);

// Use APIs just like any other JS module. (C# property names are auto-converted to camelCase.)
const example.exampleMethod();
const anotherInstance = new example.Another();
```

It could be possible to auto-generate TypeScript type definitions from the C# code at build time, but that is not implemented yet.

### Build
```
dotnet publish -r win-x64
```
Select an appropriate [.NET runtime identifier](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog) for your platform, or even a different platform for cross-compilation.

### Run
```
node .\Example\Example.js
```
The simple example script uses `require()` to load the C# native addon module that was just built, and calls a method on it.

### How it works
1. At build time, the [Node API module source generator](./NodeApi/ModuleGenerator.cs) finds the class with the `[NodeApi.Module]` attribute and then emits code to:
	- Register the module as a Node-API addon.
	- Export the module class's instance properties and methods.
	- Define JS classes for any additional exported classes.
2. The generated registration method has a special [`[UnmanagedCallersOnly]` attribute](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.unmanagedcallersonlyattribute) that causes it to be exported from the native library as an ordinary native entrypoint by the C# Native AOT compiler.
3. When Node.js loads the module, it calls the registration method like any other native entrypoint.

Helper classes in the .NET `NodeApi` library in this project facilitate interop and marshalling via a Node runtime .NET object model and low-level [`napi_` functions](https://nodejs.org/api/n-api.html) that are called via P/Invoke. The intention here is to follow a similar pattern to the [`node-addon-api` C++ classes](https://github.com/nodejs/node-addon-api/), with some minor differences allowing for .NET conventions.

Error & exception handling is not fully developed yet (among other things). With more work, it can be possible to propagate C# exceptions and JS errors naturally across call boundaries.
