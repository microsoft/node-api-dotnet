using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NodeApi;

public readonly partial struct JSObject : IDictionary<JSValue, JSValue>
{
    private readonly JSValue _value;

    public int Count => throw new System.NotImplementedException();

    public bool IsReadOnly => throw new System.NotImplementedException();

    public static explicit operator JSObject(JSValue value) => new(value);
    public static implicit operator JSValue(JSObject obj) => obj._value;

    private JSObject(JSValue value)
    {
        _value = value;
    }

    public JSObject() : this(JSValue.CreateObject())
    {
    }

    public void DefineProperties(params JSPropertyDescriptor[] descriptors)
    {
        _value.DefineProperties(descriptors);
    }

    public JSValue this[JSValue name]
    {
        get => _value.GetProperty(name);
        set => _value.SetProperty(name, value);
    }

    public JSValue this[string name]
    {
        get => _value.GetProperty(name);
        set => _value.SetProperty(name, value);
    }

    public void Add(JSValue key, JSValue value) => _value.SetProperty(key, value);

    public bool ContainsKey(JSValue key) => _value.HasProperty(key);

    public bool Remove(JSValue key) => _value.DeleteProperty(key);

    public bool TryGetValue(JSValue key, [MaybeNullWhen(false)] out JSValue value)
    {
        value = _value.GetProperty(key);
        return !value.IsUndefined();
    }

    public void Add(KeyValuePair<JSValue, JSValue> item) => _value.SetProperty(item.Key, item.Value);

    public bool Contains(KeyValuePair<JSValue, JSValue> item) => _value.HasProperty(item.Key);

    public void CopyTo(KeyValuePair<JSValue, JSValue>[] array, int arrayIndex)
    {
        int index = arrayIndex;
        int maxIndex = array.Length - 1;
        foreach (KeyValuePair<JSValue, JSValue> entry in this)
        {
            if (index <= maxIndex)
            {
                array[index] = entry;
            }
        }
    }

    public bool Remove(KeyValuePair<JSValue, JSValue> item) => _value.DeleteProperty(item.Key);

    public Enumerator GetEnumerator() => new(_value);

    ICollection<JSValue> IDictionary<JSValue, JSValue>.Keys => throw new System.NotImplementedException();

    ICollection<JSValue> IDictionary<JSValue, JSValue>.Values => throw new System.NotImplementedException();

    void ICollection<KeyValuePair<JSValue, JSValue>>.Clear() => throw new System.NotImplementedException();

    IEnumerator<KeyValuePair<JSValue, JSValue>> IEnumerable<KeyValuePair<JSValue, JSValue>>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
