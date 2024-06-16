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
    /// <remarks>
    /// This method must be called from the JS thread. The returned enumerable object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(
        this JSAsyncIterable iterable, JSValue.To<T> fromJS)
        => ((JSValue)iterable).IsNullOrUndefined() ? null! :
            new JSAsyncIterableEnumerable<T>((JSValue)iterable, fromJS);

    /// <summary>
    /// Creates an enumerable adapter for a JS iterable object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned enumerable object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IEnumerable<T> AsEnumerable<T>(this JSIterable iterable, JSValue.To<T> fromJS)
        => ((JSValue)iterable).IsNullOrUndefined() ? null! :
            new JSIterableEnumerable<T>((JSValue)iterable, fromJS);

    /// <summary>
    /// Creates an enumerable adapter for a JS Array object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned enumerable object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IEnumerable<T> AsEnumerable<T>(this JSArray array, JSValue.To<T> fromJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSIterableEnumerable<T>((JSValue)array, fromJS);

    /// <summary>
    /// Creates a read-only collection adapter for a JS Array object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned collection object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(
        this JSArray array, JSValue.To<T> fromJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSArrayReadOnlyCollection<T>((JSValue)array, fromJS);

    /// <summary>
    /// Creates a collection adapter for a JS Array object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned collection object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static ICollection<T> AsCollection<T>(
        this JSArray array, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSArrayCollection<T>((JSValue)array, fromJS, toJS);

    /// <summary>
    /// Creates a read-only list adapter for a JS Array object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned list object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IReadOnlyList<T> AsReadOnlyList<T>(this JSArray array, JSValue.To<T> fromJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSArrayReadOnlyList<T>((JSValue)array, fromJS);

    /// <summary>
    /// Creates a list adapter for a JS Array object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned list object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IList<T> AsList<T>(this JSArray array, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)array).IsNullOrUndefined() ? null! :
            new JSArrayList<T>((JSValue)array, fromJS, toJS);

    /// <summary>
    /// Creates an enumerable adapter for a JS Set object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned enumerable object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IEnumerable<T> AsEnumerable<T>(this JSSet set, JSValue.To<T> fromJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSIterableEnumerable<T>((JSValue)set, fromJS);

    /// <summary>
    /// Creates a read-only collection adapter for a JS Set object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned collection object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IReadOnlyCollection<T> AsReadOnlyCollection<T>(this JSSet set, JSValue.To<T> fromJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSSetReadOnlyCollection<T>((JSValue)set, fromJS);

    /// <summary>
    /// Creates a collection adapter for a JS Set object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned collection object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static ICollection<T> AsCollection<T>(this JSSet set, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSSetCollection<T>((JSValue)set, fromJS, toJS);

#if READONLY_SET
    /// <summary>
    /// Creates a read-only set adapter for a JS Set object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned set object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IReadOnlySet<T> AsReadOnlySet<T>(this JSSet set, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSSetReadOnlySet<T>((JSValue)set, fromJS, toJS);
#endif

    /// <summary>
    /// Creates a set adapter for a JS Set object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned set object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static ISet<T> AsSet<T>(this JSSet set, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        => ((JSValue)set).IsNullOrUndefined() ? null! :
            new JSSetSet<T>((JSValue)set, fromJS, toJS);

    /// <summary>
    /// Creates a read-only dictionary adapter for a JS Map object, without copying.
    /// </summary>
    /// <remarks>
    /// This method must be called from the JS thread. The returned dictionary object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
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
    /// <remarks>
    /// This method must be called from the JS thread. The returned dictionary object
    /// is thread-safe and may be accessed from threads other than the JS thread
    /// (though accessing from the JS thread is more efficient).
    /// </remarks>
    public static IDictionary<TKey, TValue> AsDictionary<TKey, TValue>(
        this JSMap map,
        JSValue.To<TKey> keyFromJS,
        JSValue.To<TValue> valueFromJS,
        JSValue.From<TKey> keyToJS,
        JSValue.From<TValue> valueToJS)
        => ((JSValue)map).IsNullOrUndefined() ? null! :
            new JSMapDictionary<TKey, TValue>((JSValue)map, keyFromJS, valueFromJS, keyToJS, valueToJS);
}

internal sealed class JSAsyncIterableEnumerator<T> : IAsyncEnumerator<T>, IDisposable
{
    private readonly JSValue.To<T> _fromJS;
    private readonly JSReference _iteratorReference;
    private readonly CancellationToken _cancellation;
    private JSReference? _currentReference;

    internal JSAsyncIterableEnumerator(
        JSValue iterable,
        JSValue.To<T> fromJS,
        CancellationToken cancellation)
    {
        _fromJS = fromJS;
        _iteratorReference = new JSReference(iterable.CallMethod(JSSymbol.AsyncIterator));
        _cancellation = cancellation;
        _currentReference = null;
    }

    public async ValueTask<bool> MoveNextAsync()
    {
        return await _iteratorReference.Run(async (iterator) =>
        {
            _currentReference?.Dispose();

            JSPromise nextPromise = (JSPromise)iterator.CallMethod("next");
            JSValue nextResult = await nextPromise.AsTask(_cancellation);
            JSValue done = nextResult["done"];

            if (done.IsBoolean() && (bool)done)
            {
                _currentReference = null;
                return false;
            }
            else
            {
                // Save a reference to the next result object rather than the value, because if
                // the value is a primitive type then it's not possible to directly reference it.
                _currentReference = new JSReference(nextResult);
                return true;
            }
        });
    }

    public T Current => _currentReference != null
        ? _currentReference.Run((result) => _fromJS(result["value"]))
        : throw new InvalidOperationException("Invalid enumerator state");

    public void Dispose()
    {
        _currentReference?.Dispose();
        _iteratorReference.Dispose();
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        _currentReference?.Dispose();
        _iteratorReference.Dispose();
        return default;
    }
}

internal sealed class JSIterableEnumerator<T> :
    IEnumerator<T>,
    System.Collections.IEnumerator,
    IDisposable
{
    private readonly JSReference _iterableReference;
    private readonly JSValue.To<T> _fromJS;
    private JSReference _iteratorReference;
    private JSReference? _currentReference;

    internal JSIterableEnumerator(JSValue iterable, JSValue.To<T> fromJS)
    {
        _iterableReference = new JSReference(iterable);
        _fromJS = fromJS;
        _iteratorReference = new JSReference(iterable.CallMethod(JSSymbol.Iterator));
        _currentReference = null;
    }

    public bool MoveNext()
    {
        return _iteratorReference.Run((iterator) =>
        {
            _currentReference?.Dispose();

            JSValue nextResult = iterator.CallMethod("next");
            JSValue done = nextResult["done"];
            if (done.IsBoolean() && (bool)done)
            {
                _currentReference = null;
                return false;
            }
            else
            {
                // Save a reference to the next result object rather than the value, because if
                // the value is a primitive type then it's not possible to directly reference it.
                _currentReference = new JSReference(nextResult);
                return true;
            }
        });
    }

    public T Current => _currentReference != null
        ? _currentReference.Run((result) => _fromJS(result["value"])) :
        throw new InvalidOperationException("Invalid enumerator state");

    object? System.Collections.IEnumerator.Current => Current;

    void System.Collections.IEnumerator.Reset()
    {
        _iterableReference.Run((iterable) =>
        {
            _currentReference?.Dispose();
            _currentReference = null;
            _iteratorReference.Dispose();
            _iteratorReference = new JSReference(iterable.CallMethod(JSSymbol.Iterator));
        });
    }

    public void Dispose()
    {
        _currentReference?.Dispose();
        _iteratorReference.Dispose();
        _iterableReference.Dispose();
    }
}

internal sealed class JSAsyncIterableEnumerable<T> :
    IAsyncEnumerable<T>,
    IEquatable<JSValue>,
    IAsyncDisposable,
    IDisposable
{
    private readonly JSReference _iterableReference;
    private readonly JSValue.To<T> _fromJS;

    internal JSAsyncIterableEnumerable(JSValue iterable, JSValue.To<T> fromJS)
    {
        _iterableReference = new JSReference(iterable);
        _fromJS = fromJS;
    }

    bool IEquatable<JSValue>.Equals(JSValue other)
        => _iterableReference.Run((iterable) => iterable.Equals(other));

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellation)
    {
        return _iterableReference.Run(
            (iterable) => new JSAsyncIterableEnumerator<T>(iterable, _fromJS, cancellation));
    }

    public void Dispose()
    {
        _iterableReference.Dispose();
    }

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        _iterableReference.Dispose();
        return default;
    }
}

internal class JSIterableEnumerable<T> : IEnumerable<T>, IEquatable<JSValue>, IDisposable
{
    internal JSIterableEnumerable(JSValue iterable, JSValue.To<T> fromJS)
    {
        _iterableReference = new JSReference(iterable);
        FromJS = fromJS;
    }

    private readonly JSReference _iterableReference;

    internal JSValue Value => _iterableReference.GetValue()!.Value;

    protected void Run(Action<JSValue> action) => _iterableReference.Run(action);
    protected TResult Run<TResult>(Func<JSValue, TResult> action) => _iterableReference.Run(action);

    protected JSValue.To<T> FromJS { get; }

    bool IEquatable<JSValue>.Equals(JSValue other) => Run((iterable) => iterable.Equals(other));

    public IEnumerator<T> GetEnumerator()
        => Run((iterable) => new JSIterableEnumerator<T>(iterable, FromJS));

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
        return Run((iterable) =>
        {
            JSValue iterator = iterable.CallMethod(JSSymbol.Iterator);
            while (true)
            {
                JSValue nextResult = iterator.CallMethod("next");
                JSValue done = nextResult["done"];
                if (done.IsBoolean() && (bool)done)
                {
                    return false;
                }

                JSValue value = nextResult["value"];
                if (value.Equals(item))
                {
                    return true;
                }
            }
        });
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

    public int Count => Run((collection) => collection.GetArrayLength());
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

    public void Add(T item) => Run((array) => array.CallMethod("push", ToJS(item)));

    public void Clear() => Run((array) => array.CallMethod("splice", 0, Count));

    public bool Contains(T item)
        => Run((array) => (bool)array.CallMethod("includes", ToJS(item)));

    public void CopyTo(T[] array, int arrayIndex)
    {
        Run((jsArray) =>
        {
            int i = arrayIndex;
            JSValue iterator = jsArray.CallMethod(JSSymbol.Iterator);
            while (true)
            {
                JSValue nextResult = iterator.CallMethod("next");
                JSValue done = nextResult["done"];
                if (done.IsBoolean() && (bool)done)
                {
                    break;
                }

                JSValue value = nextResult["value"];
                array[i++] = FromJS(value);
            }
        });
    }

    public bool Remove(T item)
    {
        return Run((array) =>
        {
            int index = (int)array.CallMethod("indexOf", ToJS(item));
            if (index < 0)
            {
                return false;
            }

            array.CallMethod("splice", index, 1);
            return true;
        });
    }
}

internal class JSArrayReadOnlyList<T> : JSArrayReadOnlyCollection<T>, IReadOnlyList<T>
{
    internal JSArrayReadOnlyList(JSValue array, JSValue.To<T> fromJS) : base(array, fromJS)
    {
    }

    public T this[int index] => Run((list) => FromJS(list.GetElement(index)));
}

internal class JSArrayList<T> : JSArrayCollection<T>, IList<T>
{
    internal JSArrayList(JSValue array, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        : base(array, fromJS, toJS)
    {
    }

    public T this[int index]
    {
        get => Run((array) => FromJS(array.GetElement(index)));
        set => Run((array) => array.SetElement(index, ToJS(value)));
    }

    public int IndexOf(T item) => Run((array) => (int)array.CallMethod("indexOf", ToJS(item)));

    public void Insert(int index, T item)
        => Run((array) => array.CallMethod("splice", index, 0, ToJS(item)));

    public void RemoveAt(int index) => Run((array) => array.CallMethod("splice", index, 1));
}

internal class JSSetReadOnlyCollection<T> : JSIterableEnumerable<T>, IReadOnlyCollection<T>
{
    internal JSSetReadOnlyCollection(JSValue value, JSValue.To<T> fromJS) : base(value, fromJS)
    {
    }

    public int Count => Run((@set) => (int)@set["size"]);
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

    public void Add(T item) => Run((@set) => @set.CallMethod("add", ToJS(item)));

    public void Clear() => Run((@set) => @set.CallMethod("clear"));

    public bool Contains(T item) => Run((@set) => (bool)@set.CallMethod("has", ToJS(item)));

    public void CopyTo(T[] array, int arrayIndex)
    {
        Run((@set) =>
        {
            int i = arrayIndex;
            JSValue iterator = @set.CallMethod(JSSymbol.Iterator);
            while (true)
            {
                JSValue nextResult = iterator.CallMethod("next");
                JSValue done = nextResult["done"];
                if (done.IsBoolean() && (bool)done)
                {
                    break;
                }

                JSValue value = nextResult["value"];
                array[i++] = FromJS(value);
            }
        });
    }

    public bool Remove(T item) => Run((@set) => (bool)@set.CallMethod("delete", ToJS(item)));
}

#if READONLY_SET
internal class JSSetReadOnlySet<T> : JSSetReadOnlyCollection<T>, IReadOnlySet<T>
{
    internal JSSetReadOnlySet(JSValue value, JSValue.To<T> fromJS, JSValue.From<T> toJS)
        : base(value, fromJS)
    {
        ToJS = toJS;
    }

    protected JSValue.From<T> ToJS { get; }

    public bool Contains(T item) => Run((@set) => (bool)@set.CallMethod("has", ToJS(item)));

    public bool IsProperSubsetOf(IEnumerable<T> other) => throw new NotImplementedException();
    public bool IsProperSupersetOf(IEnumerable<T> other) => throw new NotImplementedException();
    public bool IsSubsetOf(IEnumerable<T> other) => throw new NotImplementedException();
    public bool IsSupersetOf(IEnumerable<T> other) => throw new NotImplementedException();
    public bool Overlaps(IEnumerable<T> other) => throw new NotImplementedException();
    public bool SetEquals(IEnumerable<T> other) => throw new NotImplementedException();
}
#endif

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
        return Run((@set) =>
        {
            int sizeBeforeAdd = (int)@set["size"];
            @set.CallMethod("add", ToJS(item));
            int sizeAfterAdd = (int)@set["size"];
            return sizeAfterAdd > sizeBeforeAdd;
        });
    }
}

internal class JSMapReadOnlyDictionary<TKey, TValue> :
    IReadOnlyDictionary<TKey, TValue>,
    IEquatable<JSValue>,
    IDisposable
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

    protected void Run(Action<JSValue> action) => _mapReference.Run(action);
    protected TResult Run<TResult>(Func<JSValue, TResult> action) => _mapReference.Run(action);

    internal JSValue Value => _mapReference.GetValue()!.Value;

    bool IEquatable<JSValue>.Equals(JSValue other) => Run((map) => map.Equals(other));

    protected JSValue.To<TKey> KeyFromJS { get; }
    protected JSValue.To<TValue> ValueFromJS { get; }
    protected JSValue.From<TKey> KeyToJS { get; }

    public int Count => Run((map) => (int)map["size"]);

    public IEnumerable<TKey> Keys
        => Run((map) => ((JSIterable)map["keys"]).AsEnumerable<TKey>(KeyFromJS));

    public IEnumerable<TValue> Values
        => Run((map) => ((JSIterable)map["values"]).AsEnumerable<TValue>(ValueFromJS));

    public TValue this[TKey key]
    {
        get => Run((map) =>
        {
            JSValue value = map.CallMethod("get", KeyToJS(key));
            if (value.IsUndefined())
            {
                throw new KeyNotFoundException();
            }
            return ValueFromJS(value);
        });
    }

    public bool ContainsKey(TKey key) => Run((map) => (bool)map.CallMethod("has", KeyToJS(key)));

#if NETFRAMEWORK || NETSTANDARD
    public bool TryGetValue(TKey key, out TValue value)
#else
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
#endif
    {
        bool result;
        (result, value) = Run((map) =>
        {
            JSValue jsValue = map.CallMethod("get", KeyToJS(key));
            if (jsValue.IsUndefined())
            {
                return (false, default(TValue)!);
            }
            return (true, ValueFromJS(jsValue));
        });
        return result;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        return Run((map) => new JSIterableEnumerator<KeyValuePair<TKey, TValue>>(
            map, (pair) => new KeyValuePair<TKey, TValue>(
                KeyFromJS(pair[0]), ValueFromJS(pair[1]))));
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
        get => Run((map) =>
        {
            JSValue value = map.CallMethod("get", KeyToJS(key));
            if (value.IsUndefined())
            {
                throw new KeyNotFoundException();
            }
            return ValueFromJS(value);
        });
        set => Run((map) =>
        {
            map.CallMethod("set", KeyToJS(key), ValueToJS(value));
        });
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

    public bool Remove(TKey key) => Run((map) => (bool)map.CallMethod("delete", KeyToJS(key)));

    public void Clear() => Run((map) => map.CallMethod("clear"));

    ICollection<TKey> IDictionary<TKey, TValue>.Keys
        => Run((map) => new JSIterableCollection<TKey>(
            (JSIterable)map["keys"], KeyFromJS, GetCount));

    ICollection<TValue> IDictionary<TKey, TValue>.Values
        => Run((map) => new JSIterableCollection<TValue>(
            (JSIterable)map["values"], ValueFromJS, GetCount));

    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item)
        => Add(item.Key, item.Value);

    bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item)
        => TryGetValue(item.Key, out TValue? value) &&
            (item.Value?.Equals(value) ?? value == null);

    void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(
        KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        Run((map) =>
        {
            int i = arrayIndex;
            JSValue iterator = map.CallMethod(JSSymbol.Iterator);
            while (true)
            {
                JSValue nextResult = iterator.CallMethod("next");
                JSValue done = nextResult["done"];
                if (done.IsBoolean() && (bool)done)
                {
                    break;
                }

                JSValue pair = nextResult["value"];
                array[i++] = new KeyValuePair<TKey, TValue>(
                    KeyFromJS(pair[0]), ValueFromJS(pair[1]));
            }
        });
    }

    bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item)
    {
        return Run((map) =>
        {
            JSValue jsValue = map.CallMethod("get", KeyToJS(item.Key));
            if (jsValue.IsUndefined())
            {
                return false;
            }

            TValue value = ValueFromJS(jsValue);
            if (value?.Equals(item.Value) != true)
            {
                return false;
            }

            map.CallMethod("delete", KeyToJS(item.Key));
            return true;
        });
    }
}
