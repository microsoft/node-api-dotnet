using System;
using System.Collections;
using System.Collections.Generic;

namespace NodeApi;

public partial struct JSIterable
{
    /// <summary>
    /// Creates an enumerable adapter for a JS iterable object.
    /// </summary>
    public IEnumerable<T> AsEnumerable<T>(JSValue.To<T> fromJS) => new Enumerable<T>(_value, fromJS);

    private class Enumerator<T> : IEnumerator<T>, IEnumerator
    {
        private readonly JSValue _array;
        private readonly JSValue.To<T> _fromJS;
        private readonly int _count;
        private int _index;
        private JSValue? _current;

        internal Enumerator(JSValue array, JSValue.To<T> fromJS)
        {
            _array = array;
            _fromJS = fromJS;

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

        public T Current => _current.HasValue ? _fromJS(_current.Value) :
            throw new InvalidOperationException("Unexpected enumerator state");

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

    internal class Enumerable<T> : IEnumerable<T>, IDisposable
    {
        internal Enumerable(JSValue array, JSValue.To<T> fromJS)
        {
            _arrayReference = new JSReference(array);
            FromJS = fromJS;
        }

        private readonly JSReference _arrayReference;

        protected internal JSValue Array => _arrayReference.GetValue()!.Value;

        protected JSValue.To<T> FromJS { get; }

        public IEnumerator<T> GetEnumerator() => new Enumerator<T>(Array, FromJS);

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
                    _arrayReference.Dispose();
                }

                IsDisposed = true;
            }
        }

    }
}
