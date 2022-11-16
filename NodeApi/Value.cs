using System;

namespace NodeApi;

public interface IValue<T> where T : IValue<T>
{
	static abstract T From(nint value, nint env);
	static abstract implicit operator nint(T v);
}

// TODO: Consider making this (and value subclasses) a struct.
public class Value : IValue<Value>
{
	protected readonly nint value;
	private readonly nint env;

	public Value() { }

	public Value(nint value) : this(value, Env.Current) { }

	internal Value(nint value, nint env)
	{
		this.value = value;
		this.env = env;
	}

	static Value IValue<Value>.From(nint value, nint env) => new Value(value, env);

	public static implicit operator nint(Value v) => v.value;

	public T As<T>() where T : IValue<T> => T.From(this.value, this.env);

	public Env Env => new Env(this.env);

	public bool IsEmpty => this.value == 0;

	public ValueType Type
	{
		get
		{
			throw new NotImplementedException();
		}
	}
}
