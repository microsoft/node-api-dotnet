// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi;

public partial struct JSMap
{
    public struct Enumerator :
        IEnumerator<KeyValuePair<JSValue, JSValue>>,
        System.Collections.IEnumerator
    {
        private readonly JSValue _iterable;
        private JSValue _iterator;
        private KeyValuePair<JSValue, JSValue>? _current;

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
                JSArray currentEntry = (JSArray)nextResult["value"];
                _current = new KeyValuePair<JSValue, JSValue>(currentEntry[0], currentEntry[1]);
                return true;
            }
        }

        public KeyValuePair<JSValue, JSValue> Current
            => _current ?? throw new InvalidOperationException("Unexpected enumerator state");

        object? System.Collections.IEnumerator.Current => Current;

        void System.Collections.IEnumerator.Reset()
        {
            _iterator = _iterable.CallMethod(JSSymbol.Iterator);
            _current = default;
        }

        void IDisposable.Dispose()
        {
        }
    }
}
