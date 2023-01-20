using System;
using System.Collections;
using System.Collections.Generic;

namespace NodeApi;

public struct JSArrayItemEnumerator : IEnumerator<JSValue>, IEnumerator
{
    private readonly JSValue _value;
    private readonly int _count;
    private int _index;
    private JSValue? _current;

    internal JSArrayItemEnumerator(JSValue value)
    {
        _value = value;
        if (value.IsArray())
        {
            _count = value.GetArrayLength();
        }
        else
        {
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
            _current = _value.GetElement(_index);
            _index++;
            return true;
        }

        _index = _count + 1;
        _current = default;
        return false;
    }

    public JSValue Current
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

