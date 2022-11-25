using System.Collections.Generic;
using System.Runtime.InteropServices;
using static NodeApi.JSNative.Interop;
using NodeApi.Examples;

namespace NodeApi.Generated;

public static class Module
{
	public static Example? Instance { get; private set; }

	[UnmanagedCallersOnly(EntryPoint = "napi_register_module_v1")]
	public static napi_value Initialize(napi_env env, napi_value exports)
	{
		using var scope = new JSValueScope(env);
		var exportsValue = new JSValue(scope, exports);

		try
		{
			var moduleProperties = new List<PropertyDescriptor>();
			moduleProperties.Add(new PropertyDescriptor(
				"hello",
				method: (_, args) => { Instance!.Hello(args); return default; }));
			moduleProperties.Add(new PropertyDescriptor(
				"value",
				getter: (_) => Instance!.Value,
				setter: (_, value) => Instance!.Value = value));
			var anotherProperties = new List<PropertyDescriptor<Another>>();
			anotherProperties.Add(new PropertyDescriptor<Another>(
				"staticValue",
				getter: (_, _) => Another.StaticValue,
				Enumerable | Writable | Configurable | Static));
			anotherProperties.Add(new PropertyDescriptor<Another>(
				"instanceValue",
				getter: (instance, _) => instance!.InstanceValue));
			anotherProperties.Add(new PropertyDescriptor<Another>(
				"staticMethod",
				method: (_, _, args) => { return Another.StaticMethod(args); },
				Writable | Configurable | Static));
			anotherProperties.Add(new PropertyDescriptor<Another>(
				"instanceMethod",
				method: (instance, _, args) => { return instance!.InstanceMethod(args); }));
			var anotherClass = Object.DefineClass(
				"Another",
				constructor: (args) => new Another(args),
				properties: anotherProperties.ToArray());
			moduleProperties.Add(new PropertyDescriptor(
				"Another",
				getter: (_) => anotherClass.Value));
			exports.DefineProperties(moduleProperties.ToArray());
		}
		catch (System.Exception ex)
		{
			System.Console.Error.WriteLine($"Failed to export module: {ex}");
		}

		Instance = new Example();

		return exports;
	}
}
