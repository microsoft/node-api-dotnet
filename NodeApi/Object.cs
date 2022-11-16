using System;
using System.Runtime.InteropServices;

namespace NodeApi;

public class Object : Value, IValue<Object>
{
	public Object() : base() { }

	public Object(nint value) : base(value) { }

	internal Object(nint value, nint env) : base(value, env) { }

	static Object IValue<Object>.From(nint value, nint env) => new Object(value, env);

	public static implicit operator nint(Object v) => v.value;

	public bool InstanceOf()
	{
		throw new NotImplementedException();
	}

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
				Method = p.Method == null ? null : (env, args) => InvokeCallback(env, args, p.Method),
				Getter = p.Getter == null ? null : (env, args) => InvokeCallback(env, args, p.Getter),
				Setter = p.Setter == null ? null : (env, args) => InvokeCallback(env, args, p.Setter),
				Attributes = p.Attributes,
			};
		}

		var status = NativeMethods.DefineProperties(
			Env, this, (nuint)nativeProperties.Length, nativeProperties);
		NativeMethods.ThrowIfNotOK(status);
	}

	public static Ref<Object> DefineClass<T>(
		string name,
		ConstructorCallback<T> constructor,
		params PropertyDescriptor<T>[] properties) where T : class
	{
		var nativeProperties = new NativeMethods.PropertyDescriptor[properties.Length];
		for (int i = 0; i < properties.Length; i++)
		{
			var p = properties[i];
			ArgumentException.ThrowIfNullOrEmpty(p.Name, nameof(p.Name));
			bool isStatic = p.Attributes.HasFlag(PropertyAttributes.Static);

			nativeProperties[i] = new NativeMethods.PropertyDescriptor
			{
				Utf8Name = p.Name,
				Method = p.Method == null ? null :
					(env, args) => InvokeCallback(env, args, isStatic, p.Method),
				Getter = p.Getter == null ? null :
					(env, args) => InvokeCallback(env, args, isStatic, p.Getter),
				Setter = p.Setter == null ? null :
					(env, args) => InvokeCallback(env, args, isStatic, p.Setter),
				Attributes = p.Attributes,
			};
		}

		var env = Env.Current;
		var status = NativeMethods.DefineClass(
			env,
			name,
			(nuint)name.Length,
			(env, args) => InvokeCallback(env, args, constructor),
			default,
			(nuint)nativeProperties.Length,
			nativeProperties,
			out var result);
		NativeMethods.ThrowIfNotOK(status);


		return Ref<Object>.Create(new Object(result, env));
	}

	private static nint InvokeCallback(nint env, nint args, MethodCallback callback)
	{
		Env.Current = new Env(env);
		try
		{
			var (thisArg, arguments) = GetArguments(args, env);
			return callback.Invoke(thisArg, arguments) ?? (nint)0;
		}
		catch (Exception)
		{
			// TODO: throw JS exception.
			return default;
		}
	}

	private static nint InvokeCallback(nint env, nint args, PropertyGetCallback callback)
	{
		Env.Current = new Env(env);
		try
		{
			var (thisArg, arguments) = GetArguments(args, env);
			return callback.Invoke(thisArg);
		}
		catch (Exception)
		{
			// TODO: throw JS exception.
			return default;
		}
	}

	private static nint InvokeCallback(nint env, nint args, PropertySetCallback callback)
	{
		Env.Current = new Env(env);
		try
		{
			var (thisArg, arguments) = GetArguments(args, env);
			callback.Invoke(thisArg, arguments[0]);
			return (nint)0;
		}
		catch (Exception)
		{
			// TODO: throw JS exception.
			return default;
		}
	}


	private static nint InvokeCallback<T>(
		nint env, nint args, ConstructorCallback<T> callback) where T : class
	{
		Env.Current = new Env(env);
		try
		{
			var (thisArg, arguments) = GetArguments(args, env);

			var obj = callback.Invoke(arguments);
			var objHandle = GCHandle.ToIntPtr(GCHandle.Alloc(obj));
			var status = NativeMethods.Wrap(
				env, thisArg, objHandle, default, default, default);
			NativeMethods.ThrowIfNotOK(status);
		}
		catch (Exception)
		{
			// TODO: throw JS exception.
			return default;
		}

		return default;
	}

	private static nint InvokeCallback<T>(
		nint env, nint args, bool isStatic, MethodCallback<T> callback) where T : class
	{
		Env.Current = new Env(env);
		try
		{
			var (thisArg, arguments) = GetArguments(args, env);
			var instance = GetInstance<T>(env, thisArg, isStatic);
			return callback.Invoke(instance, thisArg, arguments) ?? (nint)0;
		}
		catch (Exception)
		{
			// TODO: throw JS exception.
			return default;
		}
	}

	private static nint InvokeCallback<T>(
		nint env, nint args, bool isStatic, PropertyGetCallback<T> callback) where T : class
	{
		Env.Current = new Env(env);
		try
		{
			var (thisArg, arguments) = GetArguments(args, env);
			var instance = GetInstance<T>(env, thisArg, isStatic);
			return callback.Invoke(instance, thisArg);
		}
		catch (Exception)
		{
			// TODO: throw JS exception.
			return default;
		}
	}

	private static nint InvokeCallback<T>(
		nint env, nint args, bool isStatic, PropertySetCallback<T> callback) where T : class
	{
		Env.Current = new Env(env);
		try
		{
			var (thisArg, arguments) = GetArguments(args, env);
			var instance = GetInstance<T>(env, thisArg, isStatic);
			callback.Invoke(instance, thisArg, arguments[0]);
			return (nint)0;
		}
		catch (Exception)
		{
			// TODO: throw JS exception.
			return default;
		}
	}

	private static T? GetInstance<T>(nint env, nint thisArg, bool isStatic) where T : class
	{
		if (isStatic)
		{
			return null;
		}

		var status = NativeMethods.Unwrap(env, thisArg, out var objHandle);
		NativeMethods.ThrowIfNotOK(status);
		var instance = (T?)GCHandle.FromIntPtr(objHandle).Target;
		return instance ?? throw new Exception("Failed to unwrap instance of class " + typeof(T).Name);
	}

	private static (Value This, Value[] Arguments) GetArguments(nint args, nint env)
	{
		nuint count = 0;
		nint[] buf = Array.Empty<nint>();
		var status = NativeMethods.GetArguments(env, args, ref count, buf, out _, out _);
		NativeMethods.ThrowIfNotOK(status);

		buf = new nint[count];
		status = NativeMethods.GetArguments(env, args, ref count, buf, out var thisArg, out _);
		NativeMethods.ThrowIfNotOK(status);

		var arguments = new Value[buf.Length];
		for (int i = 0; i < buf.Length; i++)
		{
			arguments[i] = new Value(buf[i], env);
		}

		return (new Value(thisArg, env), arguments);
	}
}
