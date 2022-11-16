using System;

namespace NodeApi;

public class String : Value, IValue<String>
{
	public String() : base() { }

	public String(nint value) : base(value) { }

	internal String(nint value, nint env) : base(value, env) { }

	static String IValue<String>.From(nint value, nint env) => new String(value, env);

	public static implicit operator nint(String v) => v.value;

	public static String From(string value)
	{
		var env = (nint)Env.Current;
		var status = NativeMethods.CreateString(env, value, (nuint)value.Length, out var result);
		NativeMethods.ThrowIfNotOK(status);
		return new String(result, env);
	}

	public static implicit operator string(String value)
	{
		var status = NativeMethods.GetValueString(value.Env, value.value, null, 0, out var size);
		NativeMethods.ThrowIfNotOK(status);

		// There appears to be a bug either in how napi_get_value_string_utf16 handles the buffer
		// size or how the .NET AOT compiler marshals the array. The string gets truncated unless
		// the buffer is double the needed size.
		size = (size + 1) * 2;

		var buf = new char[size];
		status = NativeMethods.GetValueString(value.Env, value.value, buf, size, out _);
		NativeMethods.ThrowIfNotOK(status);

		return new string(buf);
	}
}
