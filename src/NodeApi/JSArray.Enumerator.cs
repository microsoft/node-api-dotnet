// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi;

public partial struct JSArray
{
    public struct Enumerator : IEnumerator<JSValue>, IEnumerator
    {
        private readonly JSValue _array;
        private readonly int _count;
        private int _index;
        private JSValue? _current;

        internal Enumerator(JSValue array)
        {
            _array = array;
            if (array.IsArray())
            {
                _count = array.GetArrayLength();
            }
            else
            {
                _count = 0;
            }
            _index = 0;
            _current = default;
        }

        public bool MoveNext()
        {
            if (_index < _count)
            {
                _current = _array.GetElement(_index);
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

        void IDisposable.Dispose()
        {
        }
    }
}

