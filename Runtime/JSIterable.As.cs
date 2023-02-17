using System;
using System.Collections;
using System.Collections.Generic;

namespace NodeApi;

public partial struct JSIterable
{
    /// <summary>
    /// Creates an enumerable adapter for a JS iterable object, without copying.
    /// </summary>
    public IEnumerable<T> AsEnumerable<T>(JSValue.To<T> fromJS) => new Enumerable<T>(_value, fromJS);

    private sealed class Enumerator<T> : IEnumerator<T>, IEnumerator
    {
        private readonly JSValue _iterable;
        private readonly JSValue.To<T> _fromJS;
        private JSValue _iterator;
        private JSValue? _current;

        internal Enumerator(JSValue iterable, JSValue.To<T> fromJS)
        {
            _iterable = iterable;
            _fromJS = fromJS;
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

        public T Current => _current.HasValue ? _fromJS(_current.Value) :
            throw new InvalidOperationException("Invalid enumerator state");

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

    internal class Enumerable<T> : IEnumerable<T>, IDisposable
    {
        internal Enumerable(JSValue iterable, JSValue.To<T> fromJS)
        {
            _iterableReference = new JSReference(iterable);
            FromJS = fromJS;
        }

        private readonly JSReference _iterableReference;

        protected internal JSValue Value => _iterableReference.GetValue()!.Value;

        protected JSValue.To<T> FromJS { get; }

        public IEnumerator<T> GetEnumerator() => new Enumerator<T>(Value, FromJS);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    _iterableReference.Dispose();
                }

                IsDisposed = true;
            }
        }
    }

    internal class Collection<T> : Enumerable<T>, ICollection<T>
    {
        private readonly Func<int> _getCount;

        internal Collection(
            JSValue iterable,
            JSValue.To<T> fromJS,
            Func<int> getCount) : base(iterable, fromJS)
        {
            _getCount = getCount;
        }

        public int Count => _getCount();

        public bool IsReadOnly => true;

        public bool Contains(T item)
        {
            foreach (T value in this)
            {
                if (value?.Equals(item) ?? item == null)
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex) => throw new NotSupportedException();

        public void Add(T item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
        public bool Remove(T item) => throw new NotSupportedException();
    }

    public readonly struct Collection : ICollection<JSValue>, IReadOnlyCollection<JSValue>
    {
        private readonly JSIterable _iterable;
        private readonly Func<int> _getCount;

        internal Collection(JSIterable iterable, Func<int> getCount)
        {
            _iterable = iterable;
            _getCount = getCount;
        }

        public int Count => _getCount();

        public bool IsReadOnly => true;

        public IEnumerator<JSValue> GetEnumerator() => _iterable.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_iterable).GetEnumerator();

        public bool Contains(JSValue item) => throw new NotImplementedException();

        public void CopyTo(JSValue[] array, int arrayIndex)
        {
            int i = arrayIndex;
            foreach (JSValue item in this)
            {
                array[i++] = item;
            }
        }

        public void Add(JSValue item) => throw new NotSupportedException();
        public bool Remove(JSValue item) => throw new NotSupportedException();
        public void Clear() => throw new NotSupportedException();
    }
}
