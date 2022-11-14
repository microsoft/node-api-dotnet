using System;

namespace NodeApi;

public class Value
{
	private nint env;
	private nint value;

	public Value() { }

	public Value(nint env, nint value)
	{
		this.env = env;
		this.value = value;
	}

	public static Value From(nint env, bool value)
	{
		throw new NotImplementedException();
	}

	public static Value From(nint env, int value)
	{
		throw new NotImplementedException();
	}

	public static Value From(nint env, string value)
	{
		throw new NotImplementedException();
	}

	public static implicit operator nint(Value v)
	{
		return v.value;
	}

	public Env Env => new Env(this.env);

	public bool IsEmpty => this.value == 0;

	public ValueType Type
	{
		get
		{
			throw new NotImplementedException();
		}
	}

	public T As<T>() where T : Value, new()
	{
		var result = new T();
		result.env = this.env;
		result.value = this.value;
		return result;
	}
}
