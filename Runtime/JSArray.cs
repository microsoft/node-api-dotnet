using System.Collections;
using System.Collections.Generic;

namespace NodeApi;

public partial struct JSArray : IList<JSValue>
{
    private JSValue _value;

    public static explicit operator JSArray(JSValue value) => new() { _value = value };
    public static implicit operator JSValue(JSArray arr) => arr._value;

    public static explicit operator JSArray(JSObject obj) => (JSArray)(JSValue)obj;
    public static implicit operator JSObject(JSArray arr) => (JSObject)arr._value;

    public JSArray()
    {
        _value = JSValue.CreateArray();
    }

    public JSArray(int length)
    {
        _value = JSValue.CreateArray(length);
    }

    public int Length => _value.GetArrayLength();

    public int Count => _value.GetArrayLength();

    public bool IsReadOnly => false;

    public JSValue this[int index]
    {
        get => _value.GetElement(index);
        set => _value.SetElement(index, value);
    }

    public void Add(JSValue item) => _value["push"].Call(_value, item);

    public void CopyTo(JSValue[] array, int arrayIndex)
    {
        int index = arrayIndex;
        int maxIndex = array.Length - 1;
        foreach (JSValue item in this)
        {
            if (index <= maxIndex)
            {
                array[index] = item;
            }
        }
    }

    public Enumerator GetEnumerator() => new(_value);

    IEnumerator<JSValue> IEnumerable<JSValue>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    int IList<JSValue>.IndexOf(JSValue item) => throw new System.NotImplementedException();

    void IList<JSValue>.Insert(int index, JSValue item) => throw new System.NotImplementedException();

    void IList<JSValue>.RemoveAt(int index) => throw new System.NotImplementedException();

    void ICollection<JSValue>.Clear() => throw new System.NotImplementedException();

    bool ICollection<JSValue>.Contains(JSValue item) => throw new System.NotImplementedException();

    bool ICollection<JSValue>.Remove(JSValue item) => throw new System.NotImplementedException();
}
