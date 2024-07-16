# JS References

The [`JSReference`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSReference) class is a strong
or weak reference to a JavaScript value. Use a reference to save a JS value on the .NET heap and
enable it to be accessed later from a different [scope](./js-value-scopes).

## Using strong references

A common practice is to save a reference as a member variable to support using the referenced
JS value in a later callback. A strong reference prevents the JS value from being released
until the reference is disposed. The referenced value can be retrieved later via the
[`JSReference.GetValue()`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSReference/GetValue)
method.

```C#
[JSExport]
public class ReferenceExample : IDisposable
{
    private readonly JSReference _dataReference;

    public ReferenceExample(JSArray data)
    {
        // The constructor must have been invoked from JS, or from .NET on the JS thread.
        // Save a reference to the JS value parameter. DO NOT store the JSArray directly
        // as a member because it will be invalid as soon as this method returns.
        _dataReference = new JSReference(data);
    }

    public double GetSum()
    {
        // Access the saved data value via the reference.
        // (It throws ObjectDisposedException if the reference is disposed.)
        JSArray data = (JSArray)_dataReference.GetValue();

        // JSArray implements IList<JSValue>.
        return data.Sum((JSValue value) => (double)value);
    }

    public void Dispose()
    {
        // Disposing the reference releases the JS value,
        // if there are no other references to the same value.
        _dataReference.Dispose();
    }
}
```

## Using weak references

A weak reference does not prevent the JS value from being released. Use
[`JSReference.TryGetValue(out JSValue)`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSReference/TryGetValue)
to conditionally retrieve a weakly-referenced value if it is still available.

```C#
if (weakReference.TryGetValue(out JSValue value))
{
    // Do something with the value.
}
else
{
    // The JS value was released and is no longer available.
}
```

## Reference limitations

Currently only values of type `object`, `function`, and `symbol` can be referenced. It is not
possible to create a reference to other value types such as `string`, `number`, `boolean`, or
`undefined`.

If the type of a value to be referenced is not known, use
[`JSReference.TryCreateReference()`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSReference/TryCreateReference)
and check the return value.
