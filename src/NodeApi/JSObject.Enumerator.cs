using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi;

public partial struct JSObject
{
    public struct Enumerator : IEnumerator<KeyValuePair<JSValue, JSValue>>
    {
        private readonly JSValue _value;
        private readonly JSValue _names;
        private readonly int _count;
        private int _index;
        private KeyValuePair<JSValue, JSValue>? _current;

        internal Enumerator(JSValue value)
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
                _current = new KeyValuePair<JSValue, JSValue>(name, _value.GetProperty(name));
                _index++;
                return true;
            }

            _index = _count + 1;
            _current = default;
            return false;
        }

        public KeyValuePair<JSValue, JSValue> Current
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
}
