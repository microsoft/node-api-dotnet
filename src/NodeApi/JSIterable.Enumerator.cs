using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi;

public partial struct JSIterable
{
    public struct Enumerator : IEnumerator<JSValue>, IEnumerator
    {
        private readonly JSValue _iterable;
        private JSValue _iterator;
        private JSValue? _current;

        internal Enumerator(JSValue iterable)
        {
            _iterable = iterable;
            _iterator = _iterable.CallMethod(JSSymbol.Iterator);
            _current = default;
        }

        public bool MoveNext()
        {
            JSValue nextResult = _iterator.CallMethod("next");
            JSValue done = nextResult["done"];
            if (done.IsBoolean() && (bool)done)
            {
                _current = default;
                return false;
            }
            else
            {
                _current = nextResult["value"];
                return true;
            }
        }

        public JSValue Current
            => _current ?? throw new InvalidOperationException("Unexpected enumerator state");

        object? IEnumerator.Current => Current;

        void IEnumerator.Reset()
        {
            _iterator = _iterable.CallMethod(JSSymbol.Iterator);
            _current = default;
        }

        void IDisposable.Dispose()
        {
        }
    }
}

