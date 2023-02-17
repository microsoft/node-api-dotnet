using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NodeApi;

public partial struct JSMap
{
    /// <summary>
    /// Creates a read-only dictionary adapter for a JS Map object, without copying.
    /// </summary>
    public IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TKey, TValue>(
        JSValue.To<TKey> keyFromJS,
        JSValue.To<TValue> valueFromJS,
        JSValue.From<TKey> keyToJS) =>
        new ReadOnlyDictionary<TKey, TValue>(_value, keyFromJS, valueFromJS, keyToJS);

    /// <summary>
    /// Creates a dictionary adapter for a JS Map object, without copying.
    /// </summary>
    public IDictionary<TKey, TValue> AsDictionary<TKey, TValue>(
        JSValue.To<TKey> keyFromJS,
        JSValue.To<TValue> valueFromJS,
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS) =>
        new Dictionary<TKey, TValue>(_value, keyFromJS, valueFromJS, keyToJS, valueToJS);

    internal class ReadOnlyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        internal ReadOnlyDictionary(
            JSValue map,
            JSValue.To<TKey> keyFromJS,
            JSValue.To<TValue> valueFromJS,
            JSValue.From<TKey> keyToJS)
        {
            _mapReference = new JSReference(map);
            KeyFromJS = keyFromJS;
            ValueFromJS = valueFromJS;
            KeyToJS = keyToJS;
        }

        private readonly JSReference _mapReference;

        protected internal JSValue Value => _mapReference.GetValue()!.Value;

        protected JSValue.To<TKey> KeyFromJS { get; }
        protected JSValue.To<TValue> ValueFromJS { get; }
        protected JSValue.From<TKey> KeyToJS { get; }

        public int Count => (int)Value["size"];

        public IEnumerable<TKey> Keys
            => ((JSIterable)Value["keys"]).AsEnumerable<TKey>(KeyFromJS);

        public IEnumerable<TValue> Values
            => ((JSIterable)Value["values"]).AsEnumerable<TValue>(ValueFromJS);

        public TValue this[TKey key]
        {
            get
            {
                JSValue value = Value.CallMethod("get", KeyToJS(key));
                if (value.IsUndefined())
                {
                    throw new KeyNotFoundException();
                }
                return ValueFromJS(value);
            }
        }

        public bool ContainsKey(TKey key) => (bool)Value.CallMethod("has", KeyToJS(key));

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            JSValue jsValue = Value.CallMethod("get", KeyToJS(key));
            if (jsValue.IsUndefined())
            {
                value = default;
                return false;
            }
            value = ValueFromJS(jsValue);
            return true;
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (KeyValuePair<JSValue, JSValue> pair in (JSMap)Value)
            {
                yield return new KeyValuePair<TKey, TValue>(
                    KeyFromJS(pair.Key), ValueFromJS(pair.Value));
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal class Dictionary<TKey, TValue> :
        ReadOnlyDictionary<TKey, TValue>,
        IDictionary<TKey, TValue>
    {
        internal Dictionary(
            JSValue map,
            JSValue.To<TKey> keyFromJS,
            JSValue.To<TValue> valueFromJS,
            JSValue.From<TKey> keyToJS,
            JSValue.From<TValue> valueToJS)
            : base(map, keyFromJS, valueFromJS, keyToJS)
        {
            ValueToJS = valueToJS;
        }

        public new TValue this[TKey key]
        {
            get
            {
                JSValue value = Value.CallMethod("get", KeyToJS(key));
                if (value.IsUndefined())
                {
                    throw new KeyNotFoundException();
                }
                return ValueFromJS(value);
            }
            set
            {
                Value.CallMethod("set", KeyToJS(key), ValueToJS(value));
            }
        }

        protected JSValue.From<TValue> ValueToJS { get; }

        private int GetCount() => Count;

        public void Add(TKey key, TValue value)
        {
            if (ContainsKey(key))
            {
                throw new ArgumentException("An item with the same key already exists.");
            }

            this[key] = value;
        }

        public bool Remove(TKey key) => (bool)Value.CallMethod("delete", KeyToJS(key));

        public void Clear() => Value.CallMethod("clear");

        ICollection<TKey> IDictionary<TKey, TValue>.Keys
            => new JSIterable.Collection<TKey>(
                (JSIterable)Value["keys"], KeyFromJS, GetCount);

        ICollection<TValue> IDictionary<TKey, TValue>.Values
            => new JSIterable.Collection<TValue>(
                (JSIterable)Value["values"], ValueFromJS, GetCount);

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
            => Add(item.Key, item.Value);

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
            => TryGetValue(item.Key, out TValue? value) &&
                (item.Value?.Equals(value) ?? value == null);

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(
            KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            int i = arrayIndex;
            foreach (KeyValuePair<TKey, TValue> pair in this)
            {
                array[i++] = pair;
            }
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
            => TryGetValue(item.Key, out TValue? value) &&
                (item.Value?.Equals(value) ?? value == null) && Remove(item.Key);
    }
}
