using System;

namespace NodeApi.Examples;

/// <summary>
/// This class defines a Node.js addon module. Public instance properties and methods on a
/// module class are automatically exported -- the equivalent of `module.exports`.
/// </summary>
[JSModule]
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
		value = "hello";
	}

	public void HelloNoParam()
	{
		Console.WriteLine("Example.HelloNoParam()");
	}

  public JSValue Hello(JSCallbackArgs args)
  {
    Console.WriteLine($"Example.Hello({(string)args[0]})");
    return $"Hello {(string)args[0]}!";
  }

  public JSValue Value
  {
    get
    {
      Console.WriteLine("Example.Value.get()");
      return value;
    }
    set
    {
      this.value = (string)value;
      Console.WriteLine($"Example.Value.set({this.value})");
    }
  }

  /// <summary>
  /// Export additional classes from the module by declaring public properties of type `Type`.
  /// </summary>
  public Type Another => typeof(Another);
}
