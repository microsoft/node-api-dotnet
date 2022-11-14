using System;
using System.Runtime.InteropServices;

namespace NodeApi.Examples;

[NodeApi.Module]
public class Example
{
	public Example()
	{
		Console.WriteLine("Example()");
	}

	public void Hello()
	{
		Console.WriteLine("Example.Hello()");
	}

	public int Value
	{
		get
		{
			Console.WriteLine("Example.Value.get()");
			return 0;
		}
		set
		{
			Console.WriteLine("Example.Value.set()");
		}
	}
}
