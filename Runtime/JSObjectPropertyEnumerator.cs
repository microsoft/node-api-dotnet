using System;
using System.Collections;
using System.Collections.Generic;

namespace NodeApi;

public struct JSObjectPropertyEnumerator : IEnumerator<(JSValue name, JSValue value)>, IEnumerator
{
    private readonly JSValue _value;
    private readonly JSValue _names;
    private readonly int _count;
    private int _index;
    private (JSValue name, JSValue value)? _current;

    internal JSObjectPropertyEnumerator(JSValue value)
    {
        _value = value;
        JSValueType valueType = value.TypeOf();
        if (valueType == JSValueType.Object || valueType == JSValueType.Function)
        {
            JSValue names = value.GetPropertyNames();
            _names = names;
            _count = names.GetArrayLength();
        }
        else
        {
            _names = JSValue.Undefined;
            _count = 0;
        }
        _index = 0;
        _current = default;
    }

    public void Dispose()
    {
    }

    public bool MoveNext()
    {
        if (_index < _count)
        {
            JSValue name = _names.GetElement(_index);
            _current = (name, _value.GetProperty(name));
            _index++;
            return true;
        }

        _index = _count + 1;
        _current = default;
        return false;
    }

    public (JSValue name, JSValue value) Current
        => _current ?? throw new InvalidOperationException("Unexpected enumerator state");

    object? IEnumerator.Current
    {
        get
        {
            if (_index == 0 || _index == _count + 1)
            {
                throw new InvalidOperationException("Invalid enumerator state");
            }
            return Current;
        }
    }

    void IEnumerator.Reset()
    {
        _index = 0;
        _current = default;
    }
}
