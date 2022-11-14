using System;

namespace NodeApi;

public class Object : Value
{
	public Object() { }

	public Object(nint env, nint value) : base(env, value) { }

	public Value this[int index]
	{
		get => Get(index);
		set => Set(index, value);
	}

	public Value this[string index]
	{
		get => Get(index);
		set => Set(index, value);
	}

	public Value this[Value index]
	{
		get => Get(index);
		set => Set(index, value);
	}

	public bool Has(int index)
	{
		throw new NotImplementedException();
	}

	public bool HasOwnProperty(int index)
	{
		throw new NotImplementedException();
	}

	public Value Get(int index)
	{
		throw new NotImplementedException();
	}

	public Value Set(int index, Value value)
	{
		throw new NotImplementedException();
	}

	public Value Delete(int index, Value value)
	{
		throw new NotImplementedException();
	}

	public bool Has(string index)
	{
		throw new NotImplementedException();
	}

	public bool HasOwnProperty(string index)
	{
		throw new NotImplementedException();
	}

	public Value Get(string index)
	{
		throw new NotImplementedException();
	}

	public Value Set(string index, Value value)
	{
		throw new NotImplementedException();
	}

	public Value Delete(string index, Value value)
	{
		throw new NotImplementedException();
	}

	public bool Has(Value index)
	{
		throw new NotImplementedException();
	}

	public bool HasOwnProperty(Value index)
	{
		throw new NotImplementedException();
	}

	public Value Get(Value index)
	{
		throw new NotImplementedException();
	}

	public Value Set(Value index, Value value)
	{
		throw new NotImplementedException();
	}

	public Value Delete(Value index, Value value)
	{
		throw new NotImplementedException();
	}

	public string[] GetPropertyNames()
	{
		throw new NotImplementedException();
	}

	public void DefineProperties(params PropertyDescriptor[] properties)
	{
		var nativeProperties = new NativeMethods.PropertyDescriptor[properties.Length];
		for (int i = 0; i < properties.Length; i++)
		{
			var p = properties[i];
			ArgumentException.ThrowIfNullOrEmpty(p.Name, nameof(p.Name));

			nativeProperties[i] = new NativeMethods.PropertyDescriptor
			{
				Utf8Name = p.Name,
				Method = p.Method == null ? null :
					(env, info) => p.Method(new CallbackInfo(env, info)) ?? (nint)0,
				Getter = p.Getter == null ? null :
					(env, info) => p.Getter(new CallbackInfo(env, info)) ?? (nint)0,
				Setter = p.Setter == null ? null :
					(env, info) => p.Setter(new CallbackInfo(env, info)) ?? (nint)0,
				Attributes = p.Attributes,
			};
		}

		var status = NativeMethods.DefineProperties(
			Env, this, (nuint)nativeProperties.Length, nativeProperties);
		NativeMethods.ThrowIfNotOK(status);
	}

	public bool InstanceOf()
	{
		throw new NotImplementedException();
	}

	public struct CallbackInfo
	{
		private readonly nint env;
		private readonly nint info;

		public CallbackInfo(nint env, nint info)
		{
			this.env = env;
			this.info = info;
		}

		public Env Env => new Env(this.env);

		public int Length
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public Value This
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public Value this[int index]
		{
			get
			{
				throw new NotImplementedException();
			}
		}
	}


	// TODO: Consider defining separate callback delegates for void methods, setters, etc.
	public delegate Value? Callback(CallbackInfo info);

	public struct PropertyDescriptor
	{
		public string Name;

		public Callback? Method;
		public Callback? Getter;
		public Callback? Setter;

		public nint Value;

		public PropertyAttributes Attributes;

		public nint Data;
	}
}
