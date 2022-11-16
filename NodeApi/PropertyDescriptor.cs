
namespace NodeApi;

public delegate Value? MethodCallback(Value thisArg, Value[] args);
public delegate Value PropertyGetCallback(Value thisArg);
public delegate void PropertySetCallback(Value thisArg, Value value);
public delegate T ConstructorCallback<T>(Value[] args) where T : class;
public delegate Value? MethodCallback<T>(T? instance, Value thisArg, Value[] args) where T : class;
public delegate Value PropertyGetCallback<T>(T? instance, Value thisArg) where T : class;
public delegate void PropertySetCallback<T>(T? instance, Value thisArg, Value value) where T : class;

public struct PropertyDescriptor
{
	public PropertyDescriptor(
		string name,
		MethodCallback method,
		PropertyAttributes? attributes = null)
	{
		this.Name = name;
		this.Method = method;
		this.Attributes = attributes ??
			PropertyAttributes.Writable | PropertyAttributes.Configurable;
	}

	public PropertyDescriptor(
		string name,
		PropertyGetCallback getter,
		PropertyAttributes? attributes = null)
	{
		this.Name = name;
		this.Getter = getter;
		this.Attributes = attributes ?? PropertyAttributes.Enumerable |
			PropertyAttributes.Writable | PropertyAttributes.Configurable;
	}

	public PropertyDescriptor(
		string name,
		PropertyGetCallback getter,
		PropertySetCallback setter,
		PropertyAttributes? attributes = null)
	{
		this.Name = name;
		this.Getter = getter;
		this.Setter = setter;
		this.Attributes = attributes ?? PropertyAttributes.Enumerable |
			PropertyAttributes.Writable | PropertyAttributes.Configurable;
	}

	public PropertyDescriptor(
		string name,
		Value value,
		PropertyAttributes? attributes = null)
	{
		this.Name = name;
		this.Value = value;
		this.Attributes = attributes ?? PropertyAttributes.Enumerable |
			PropertyAttributes.Writable | PropertyAttributes.Configurable;
	}

	public string Name;

	public MethodCallback? Method;
	public PropertyGetCallback? Getter;
	public PropertySetCallback? Setter;

	public nint Value;

	public PropertyAttributes Attributes;

	public nint Data;
}

public struct PropertyDescriptor<T> where T : class
{
	public PropertyDescriptor(
		string name,
		MethodCallback<T> method,
		PropertyAttributes? attributes = null)
	{
		this.Name = name;
		this.Method = method;
		this.Attributes = attributes ??
			PropertyAttributes.Writable | PropertyAttributes.Configurable;
	}

	public PropertyDescriptor(
		string name,
		PropertyGetCallback<T> getter,
		PropertyAttributes? attributes = null)
	{
		this.Name = name;
		this.Getter = getter;
		this.Attributes = attributes ?? PropertyAttributes.Enumerable |
			PropertyAttributes.Writable | PropertyAttributes.Configurable;
	}

	public PropertyDescriptor(
		string name,
		PropertyGetCallback<T> getter,
		PropertySetCallback<T> setter,
		PropertyAttributes? attributes = null)
	{
		this.Name = name;
		this.Getter = getter;
		this.Setter = setter;
		this.Attributes = attributes ?? PropertyAttributes.Enumerable |
			PropertyAttributes.Writable | PropertyAttributes.Configurable;
	}

	public string Name;

	public MethodCallback<T>? Method;
	public PropertyGetCallback<T>? Getter;
	public PropertySetCallback<T>? Setter;

	public nint Value;

	public PropertyAttributes Attributes;

	public nint Data;
}
