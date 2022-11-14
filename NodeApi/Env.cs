using System;

namespace NodeApi;

public struct Env
{
	private readonly nint env;

	public Env(nint env)
	{
		this.env = env;
	}

	public static implicit operator nint(Env e)
	{
		return e.env;
	}
}
