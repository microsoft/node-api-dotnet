// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi;

public partial struct JSAsyncIterable
{
    public struct Enumerator : IAsyncEnumerator<JSValue>
    {
        private readonly JSValue _iterable;
        private readonly JSValue _iterator;
        private JSValue? _current;

        internal Enumerator(JSValue iterable)
        {
            _iterable = iterable;
            _iterator = _iterable.CallMethod(JSSymbol.AsyncIterator);
            _current = default;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
            var nextPromise = (JSPromise)_iterator.CallMethod("next");
            JSValue nextResult = await nextPromise.AsTask();
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

        public readonly JSValue Current
            => _current ?? throw new InvalidOperationException("Unexpected enumerator state");

        readonly ValueTask IAsyncDisposable.DisposeAsync() => default;
    }
}

