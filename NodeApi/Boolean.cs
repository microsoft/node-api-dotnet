using System;

namespace NodeApi;

public class Boolean : Value, IValue<Boolean>
{
	public Boolean() : base() { }

	public Boolean(nint value) : base(value) { }

	internal Boolean(nint value, nint env) : base(value, env) { }

	static Boolean IValue<Boolean>.From(nint value, nint env) => new Boolean(value, env);

	public static implicit operator nint(Boolean v) => v.value;

	public static Boolean From(bool value)
	{
		var env = (nint)Env.Current;
		var status = NativeMethods.GetBoolean(env, value, out var result);
		NativeMethods.ThrowIfNotOK(status);
		return new Boolean(result, env);
	}

	public static implicit operator bool(Boolean value)
	{
		var status = NativeMethods.GetValueBoolean(value.Env, value.value, out var result);
		NativeMethods.ThrowIfNotOK(status);
		return result;
	}
}
