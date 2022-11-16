using System;

namespace NodeApi.Examples;

public class Another
{
	public Another(NodeApi.Value[] args)
	{
		Console.WriteLine("Another()");
	}

	public static Value StaticValue
	{
		get
		{
			Console.WriteLine("Another.StaticValue.get()");
			return String.From("static");
		}
	}

	public Value InstanceValue
	{
		get
		{
			Console.WriteLine("Another.InstanceValue.get()");
			return String.From("instance");
		}
	}

	public static Value StaticMethod(Value[] args)
	{
		Console.WriteLine("Another.StaticMethod()");
		return Boolean.From(true);
	}

	public Value InstanceMethod(Value[] args)
	{
		Console.WriteLine("Another.InstanceMethod()");
		return Boolean.From(false);
	}
}
