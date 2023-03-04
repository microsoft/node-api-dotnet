using System;
using System.Collections.Generic;

namespace NodeApi;

public partial struct JSSet
{
    /// <summary>
    /// Creates an enumerable adapter for a JS Set object, without copying.
    /// </summary>
    public IEnumerable<T> AsEnumerable<T>(JSValue.To<T> fromJS)
        => new JSIterable.Enumerable<T>(_value, fromJS);

    /// <summary>
    /// Creates a read-only collection adapter for a JS Set object, without copying.
    /// </summary>
    public IReadOnlyCollection<T> AsReadOnlyCollection<T>(JSValue.To<T> fromJS) =>
        new ReadOnlyCollection<T>(_value, fromJS);

    /// <summary>
    /// Creates a collection adapter for a JS Set object, without copying.
    /// </summary>
    public ICollection<T> AsCollection<T>(JSValue.To<T> fromJS, JSValue.From<T> toJS) =>
        new Collection<T>(_value, fromJS, toJS);

    /// <summary>
    /// Creates a read-only set adapter for a JS Set object, without copying.
    /// </summary>
    public IReadOnlySet<T> AsReadOnlySet<T>(JSValue.To<T> fromJS, JSValue.From<T> toJS) =>
        new ReadOnlySet<T>(_value, fromJS, toJS);

    /// <summary>
    /// Creates a set adapter for a JS Set object, without copying.
    /// </summary>
    public ISet<T> AsSet<T>(JSValue.To<T> fromJS, JSValue.From<T> toJS) =>
        new Set<T>(_value, fromJS, toJS);

    internal class ReadOnlyCollection<T> : JSIterable.Enumerable<T>, IReadOnlyCollection<T>
    {
        internal ReadOnlyCollection(JSValue value, JSValue.To<T> fromJS) : base(value, fromJS)
        {
        }

        public int Count => (int)Value["size"];
    }

    internal class Collection<T> : ReadOnlyCollection<T>, ICollection<T>
    {
        internal Collection(JSValue value, JSValue.To<T> fromJS, JSValue.From<T> toJS)
            : base(value, fromJS)
        {
            ToJS = toJS;
        }

        protected JSValue.From<T> ToJS { get; }

        bool ICollection<T>.IsReadOnly => false;

        public void Add(T item) => Value.CallMethod("add", ToJS(item));

        public void Clear() => Value.CallMethod("clear");

        public bool Contains(T item) => (bool)Value.CallMethod("has", ToJS(item));

        public void CopyTo(T[] array, int arrayIndex)
        {
            int i = arrayIndex;
            foreach (T item in this)
            {
                array[i++] = item;
            }
        }

        public bool Remove(T item) => (bool)Value.CallMethod("delete", ToJS(item));
    }

    internal class ReadOnlySet<T> : ReadOnlyCollection<T>, IReadOnlySet<T>
    {
        internal ReadOnlySet(JSValue value, JSValue.To<T> fromJS, JSValue.From<T> toJS)
            : base(value, fromJS)
        {
            ToJS = toJS;
        }

        protected JSValue.From<T> ToJS { get; }

        public bool Contains(T item) => (bool)Value.CallMethod("has", ToJS(item));

        public bool IsProperSubsetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsProperSupersetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsSubsetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsSupersetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool Overlaps(IEnumerable<T> other) => throw new NotImplementedException();
        public bool SetEquals(IEnumerable<T> other) => throw new NotImplementedException();
    }

    internal class Set<T> : Collection<T>, ISet<T>
    {
        internal Set(JSValue value, JSValue.To<T> fromJS, JSValue.From<T> toJS)
            : base(value, fromJS, toJS)
        {
        }

        public void ExceptWith(IEnumerable<T> other) => throw new NotImplementedException();
        public void IntersectWith(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsProperSubsetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsProperSupersetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsSubsetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool IsSupersetOf(IEnumerable<T> other) => throw new NotImplementedException();
        public bool Overlaps(IEnumerable<T> other) => throw new NotImplementedException();
        public bool SetEquals(IEnumerable<T> other) => throw new NotImplementedException();
        public void SymmetricExceptWith(IEnumerable<T> other) => throw new NotImplementedException();
        public void UnionWith(IEnumerable<T> other) => throw new NotImplementedException();

        public new bool Add(T item)
        {
            int countBeforeAdd = Count;
            Value.CallMethod("add", ToJS(item));
            return Count > countBeforeAdd;
        }
    }
}
