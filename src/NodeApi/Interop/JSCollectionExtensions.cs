// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.JavaScript.NodeApi.Interop;

public static class JSCollectionExtensions
{
    /// <summary>
    /// Creates an async enumerable adapter for a JS async-iterable object, without copying.
    /// </summary>
    public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(
        this JSAsyncIterable iterable, JSValue.To<T> fromJS)
        => ((JSValue)iterable).IsNullOrUndefined() ? null! :
            new JSAsyncIterableEnumerable<T>((JSValue)iterable, fromJS);

    /// <summary>
    /// Creates an enumerable adapter for a JS iterable object, without copying.
    /// </summary>
    public static IEnumerable<T> AsEnumerable<T>(this JSIterable iterable, JSValue.To<T> fromJS)
        => ((JSValue)iterable).IsNullOrUndefined() ? null! :
            new JSIterableEnumerable<T>((JSValue)iterable, fromJS);

    /// <summary>
    /// Creates an enumerable adapter for a JS Array object, without copying.
    /// </summary>
    public static IEnumerable<T> AsEnumerable<T>(this JSArray array, JSValue.To<T> fromJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSIterableEnumerable<T>((JSValue)array, fromJS);

    /// <summary>
    /// Creates a read-only collection adapter for a JS Array object, without copying.
    /// </summary>
    public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(
        this JSArray array, JSValue.To<T> fromJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSArrayReadOnlyCollection<T>((JSValue)array, fromJS);

    /// <summary>
    /// Creates a collection adapter for a JS Array object, without copying.
    /// </summary>
    public static ICollection<T> AsCollection<T>(
        this JSArray array, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSArrayCollection<T>((JSValue)array, fromJS, toJS);

    /// <summary>
    /// Creates a read-only list adapter for a JS Array object, without copying.
    /// </summary>
    public static IReadOnlyList<T> AsReadOnlyList<T>(this JSArray array, JSValue.To<T> fromJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSArrayReadOnlyList<T>((JSValue)array, fromJS);

    /// <summary>
    /// Creates a list adapter for a JS Array object, without copying.
    /// </summary>
    public static IList<T> AsList<T>(this JSArray array, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSArrayList<T>((JSValue)array, fromJS, toJS);

    /// <summary>
    /// Creates an enumerable adapter for a JS Set object, without copying.
    /// </summary>
    public static IEnumerable<T> AsEnumerable<T>(this JSSet set, JSValue.To<T> fromJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSIterableEnumerable<T>((JSValue)set, fromJS);

    /// <summary>
    /// Creates a read-only collection adapter for a JS Set object, without copying.
    /// </summary>
    public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(this JSSet set, JSValue.To<T> fromJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSSetReadOnlyCollection<T>((JSValue)set, fromJS);

    /// <summary>
    /// Creates a collection adapter for a JS Set object, without copying.
    /// </summary>
    public static ICollection<T> AsCollection<T>(this JSSet set, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSSetCollection<T>((JSValue)set, fromJS, toJS);

#if !NETFRAMEWORK
    /// <summary>
    /// Creates a read-only set adapter for a JS Set object, without copying.
    /// </summary>
    public static IReadOnlySet<T> AsReadOnlySet<T>(this JSSet set, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSSetReadOnlySet<T>((JSValue)set, fromJS, toJS);
#endif // !NETFRAMEWORK

    /// <summary>
    /// Creates a set adapter for a JS Set object, without copying.
    /// </summary>
    public static ISet<T> AsSet<T>(this JSSet set, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSSetSet<T>((JSValue)set, fromJS, toJS);

    /// <summary>
    /// Creates a read-only dictionary adapter for a JS Map object, without copying.
    /// </summary>
    public static IReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TKey, TValue>(
        this JSMap map,
        JSValue.To<TKey> keyFromJS,
        JSValue.To<TValue> valueFromJS,
        JSValue.From<TKey> keyToJS)
        => ((JSValue)map).IsNullOrUndefined() ? null! :
            new JSMapReadOnlyDictionary<TKey, TValue>((JSValue)map, keyFromJS, valueFromJS, keyToJS);

    /// <summary>
    /// Creates a dictionary adapter for a JS Map object, without copying.
    /// </summary>
    public static IDictionary<TKey, TValue> AsDictionary<TKey, TValue>(
        this JSMap map,
        JSValue.To<TKey> keyFromJS,
        JSValue.To<TValue> valueFromJS,
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS)
        => ((JSValue)map).IsNullOrUndefined() ? null! :
            new JSMapDictionary<TKey, TValue>((JSValue)map, keyFromJS, valueFromJS, keyToJS, valueToJS);
}

internal sealed class JSAsyncIterableEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly JSValue _iterable;
    private readonly JSValue.To<T> _fromJS;
    private readonly JSValue _iterator;
    private JSValue? _current;

    internal JSAsyncIterableEnumerator(JSValue iterable, JSValue.To<T> fromJS)
    {
        _iterable = iterable;
        _fromJS = fromJS;
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

    public T Current => _current.HasValue ? _fromJS(_current.Value) :
        throw new InvalidOperationException("Invalid enumerator state");

    ValueTask IAsyncDisposable.DisposeAsync() => default;
}

internal sealed class JSIterableEnumerator<T> : IEnumerator<T>, System.Collections.IEnumerator
{
    private readonly JSValue _iterable;
    private readonly JSValue.To<T> _fromJS;
    private JSValue _iterator;
    private JSValue? _current;

    internal JSIterableEnumerator(JSValue iterable, JSValue.To<T> fromJS)
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

internal class JSAsyncIterableEnumerable<T> : IAsyncEnumerable<T>, IEquatable<JSValue>
{
    internal JSAsyncIterableEnumerable(JSValue iterable, JSValue.To<T> fromJS)
    {
        _iterableReference = new JSReference(iterable);
        FromJS = fromJS;
    }

    private readonly JSReference _iterableReference;

    public JSValue Value => _iterableReference.GetValue()!.Value;

    bool IEquatable<JSValue>.Equals(JSValue other) => Value.Equals(other);

    protected JSValue.To<T> FromJS { get; }

#pragma warning disable IDE0060 // Unused parameter
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken)
        => new JSAsyncIterableEnumerator<T>(Value, FromJS);

    public ValueTask DisposeAsync(CancellationToken cancellationToken)
    {
        _iterableReference.Dispose();
        return default;
    }
#pragma warning restore IDE0060
}

internal class JSIterableEnumerable<T> : IEnumerable<T>, IEquatable<JSValue>, IDisposable
{
    internal JSIterableEnumerable(JSValue iterable, JSValue.To<T> fromJS)
    {
        _iterableReference = new JSReference(iterable);
        FromJS = fromJS;
    }

    private readonly JSReference _iterableReference;

    public JSValue Value => _iterableReference.GetValue()!.Value;

    bool IEquatable<JSValue>.Equals(JSValue other) => Value.Equals(other);

    protected JSValue.To<T> FromJS { get; }

    public IEnumerator<T> GetEnumerator() => new JSIterableEnumerator<T>(Value, FromJS);

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => GetEnumerator();

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _iterableReference.Dispose();
        }
    }
}

internal class JSIterableCollection<T>
    : JSIterableEnumerable<T>, ICollection<T>, IReadOnlyCollection<T>
{
    private readonly Func<int> _getCount;

    internal JSIterableCollection(
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

internal class JSArrayReadOnlyCollection<T> : JSIterableEnumerable<T>, IReadOnlyCollection<T>
{
    internal JSArrayReadOnlyCollection(JSValue array, JSValue.To<T> fromJS) : base(array, fromJS)
    {
    }

    public int Count => Value.GetArrayLength();
}

internal class JSArrayCollection<T> : JSArrayReadOnlyCollection<T>, ICollection<T>
{
    internal JSArrayCollection(JSValue array, JSValue.To<T> fromJS, JSValue.From<T> toJS)
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

internal class JSArrayReadOnlyList<T> : JSArrayReadOnlyCollection<T>, IReadOnlyList<T>
{
    internal JSArrayReadOnlyList(JSValue array, JSValue.To<T> fromJS) : base(array, fromJS)
    {
    }

    public T this[int index] => FromJS(Value.GetElement(index));
}

internal class JSArrayList<T> : JSArrayCollection<T>, IList<T>
{
    internal JSArrayList(JSValue array, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        : base(array, fromJS, toJS)
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

internal class JSSetReadOnlyCollection<T> : JSIterableEnumerable<T>, IReadOnlyCollection<T>
{
    internal JSSetReadOnlyCollection(JSValue value, JSValue.To<T> fromJS) : base(value, fromJS)
    {
    }

    public int Count => (int)Value["size"];
}

internal class JSSetCollection<T> : JSSetReadOnlyCollection<T>, ICollection<T>
{
    internal JSSetCollection(JSValue value, JSValue.To<T> fromJS, JSValue.From<T> toJS)
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

#if !NETFRAMEWORK
internal class JSSetReadOnlySet<T> : JSSetReadOnlyCollection<T>, IReadOnlySet<T>
{
    internal JSSetReadOnlySet(JSValue value, JSValue.To<T> fromJS, JSValue.From<T> toJS)
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
#endif // !NETFRAMEWORK

internal class JSSetSet<T> : JSSetCollection<T>, ISet<T>
{
    internal JSSetSet(JSValue value, JSValue.To<T> fromJS, JSValue.From<T> toJS)
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

internal class JSMapReadOnlyDictionary<TKey, TValue> :
    IReadOnlyDictionary<TKey, TValue>, IEquatable<JSValue>, IDisposable
{
    internal JSMapReadOnlyDictionary(
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

    public JSValue Value => _mapReference.GetValue()!.Value;

    bool IEquatable<JSValue>.Equals(JSValue other) => Value.Equals(other);

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
            value = default!;
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

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        => GetEnumerator();

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mapReference.Dispose();
        }
    }
}

internal class JSMapDictionary<TKey, TValue> :
    JSMapReadOnlyDictionary<TKey, TValue>,
    IDictionary<TKey, TValue>
{
    internal JSMapDictionary(
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
        => new JSIterableCollection<TKey>(
            (JSIterable)Value["keys"], KeyFromJS, GetCount);

    ICollection<TValue> IDictionary<TKey, TValue>.Values
        => new JSIterableCollection<TValue>(
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
