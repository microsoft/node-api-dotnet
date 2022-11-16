using System.Threading;

namespace NodeApi;

public readonly struct Env
{
	private static AsyncLocal<Env> current = new();

	public static Env Current
	{
		get => Env.current.Value;
		set => Env.current.Value = value;
	}

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
