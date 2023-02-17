using System.Collections.Generic;
using static NodeApi.JSIterable;

namespace NodeApi;

public partial struct JSArray
{
    /// <summary>
    /// Creates an enumerable adapter for a JS Array object, without copying.
    /// </summary>
    public IEnumerable<T> AsEnumerable<T>(JSValue.To<T> fromJS) => new Enumerable<T>(_value, fromJS);

    /// <summary>
    /// Creates a read-only collection adapter for a JS Array object, without copying.
    /// </summary>
    public IReadOnlyCollection<T> AsReadOnlyCollection<T>(JSValue.To<T> fromJS) =>
        new ReadOnlyCollection<T>(_value, fromJS);

    /// <summary>
    /// Creates a collection adapter for a JS Array object, without copying.
    /// </summary>
    public ICollection<T> AsCollection<T>(JSValue.To<T> fromJS, JSValue.From<T> toJS) =>
        new Collection<T>(_value, fromJS, toJS);

    /// <summary>
    /// Creates a read-only list adapter for a JS Array object, without copying.
    /// </summary>
    public IReadOnlyList<T> AsReadOnlyList<T>(JSValue.To<T> fromJS) =>
        new ReadOnlyList<T>(_value, fromJS);

    /// <summary>
    /// Creates a list adapter for a JS Array object, without copying.
    /// </summary>
    public IList<T> AsList<T>(JSValue.To<T> fromJS, JSValue.From<T> toJS) =>
        new List<T>(_value, fromJS, toJS);

    internal class ReadOnlyCollection<T> : Enumerable<T>, IReadOnlyCollection<T>
    {
        internal ReadOnlyCollection(JSValue array, JSValue.To<T> fromJS) : base(array, fromJS)
        {
        }

        public int Count => Value.GetArrayLength();
    }

    internal class Collection<T> : ReadOnlyCollection<T>, ICollection<T>
    {
        internal Collection(JSValue array, JSValue.To<T> fromJS, JSValue.From<T> toJS)
            : base(array, fromJS)
        {
            ToJS = toJS;
        }

        protected JSValue.From<T> ToJS { get; }

        public bool IsReadOnly => false;

        public void Add(T item) => Value.CallMethod("push", ToJS(item));

        public void Clear() => Value.CallMethod("splice", 0, Count);

        public bool Contains(T item) => (bool)Value.CallMethod("includes", ToJS(item));

        public void CopyTo(T[] array, int arrayIndex)
        {
            int i = arrayIndex;
            foreach (T item in this)
            {
                array[i++] = item;
            }
        }

        public bool Remove(T item)
        {
            int index = (int)Value.CallMethod("indexOf", ToJS(item));
            if (index < 0)
            {
                return false;
            }

            Value.CallMethod("splice", index, 1);
            return true;
        }
    }

    internal class ReadOnlyList<T> : ReadOnlyCollection<T>, IReadOnlyList<T>
    {
        internal ReadOnlyList(JSValue array, JSValue.To<T> fromJS) : base(array, fromJS)
        {
        }

        public T this[int index] => FromJS(Value.GetElement(index));
    }

    internal class List<T> : Collection<T>, IList<T>
    {
        internal List(JSValue array, JSValue.To<T> fromJS, JSValue.From<T> toJS) : base(array, fromJS, toJS)
        {
        }

        public T this[int index]
        {
            get => FromJS(Value.GetElement(index));
            set => Value.SetElement(index, ToJS(value));
        }

        public int IndexOf(T item) => (int)Value.CallMethod("indexOf", ToJS(item));

        public void Insert(int index, T item) => Value.CallMethod("splice", index, 0, ToJS(item));

        public void RemoveAt(int index) => Value.CallMethod("splice", index, 1);
    }
}

