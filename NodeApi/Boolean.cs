using System;

namespace NodeApi;

public class Boolean : Value
{
	public Boolean() { }

	public Boolean(nint env, nint value) : base(env, value) { }

	public static new Boolean From(nint env, bool value)
	{
		throw new NotImplementedException();
	}

	public static implicit operator bool(Boolean value)
	{
		throw new NotImplementedException();
	}
}
