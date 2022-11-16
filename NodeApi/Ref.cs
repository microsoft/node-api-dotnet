using System;

namespace NodeApi;

public class Ref<T> : IDisposable where T : Value, IValue<T>
{
	private readonly nint value;
	private readonly nint env;

	internal Ref(nint value, nint env)
	{
		this.value = value;
		this.env = env;
	}

	public static Ref<T> Create(T value, uint initialRefCount = 1)
	{
		var status = NativeMethods.CreateReference(value.Env, value, initialRefCount, out var result);
		NativeMethods.ThrowIfNotOK(status);

		return new Ref<T>(result, value.Env);
	}

	public void Dispose()
	{
		var status = NativeMethods.DeleteReference(this.env, this.value);
		NativeMethods.ThrowIfNotOK(status);
	}

	public T Value
	{
		get
		{
			var status = NativeMethods.GetReferenceValue(this.env, this.value, out var result);
			NativeMethods.ThrowIfNotOK(status);

			return T.From(result, this.env);
		}
	}
}