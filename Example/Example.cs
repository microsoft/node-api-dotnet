using System;

namespace NodeApi.Examples;

/// <summary>
/// This class defines a Node.js addon module. Public instance properties and methods on a
/// module class are automatically exported -- the equivalent of `module.exports`.
/// </summary>
[NodeApi.Module]
public class Example
{
	private string value;

	/// <summary>
	/// The module class must have a public constructor that takes no parameters.
	/// A singleton instance of the class is instantiated when the module is loaded.
	/// </summary>
	public Example()
	{
		Console.WriteLine("Example()");
		this.value = "hello";
	}

	public void Hello(NodeApi.Value[] args)
	{
		Console.WriteLine("Example.Hello()");
	}

	public NodeApi.Value Value
	{
		get
		{
			Console.WriteLine("Example.Value.get()");
			return NodeApi.String.From(this.value);
		}
		set
		{
			this.value = value.As<String>();
			Console.WriteLine($"Example.Value.set({this.value})");
		}
	}

	/// <summary>
	/// Export additional classes from the module by declaring public properties of type `Type`.
	/// </summary>
	public Type Another => typeof(Another);
}
