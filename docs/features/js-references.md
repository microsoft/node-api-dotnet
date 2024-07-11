# JS References

The [`JSReference`](../reference/dotnet/Microsoft.JavaScript.NodeApi/JSReference) class is a strong
or weak reference to a JavaScript value. Use a reference to save a JS value on the .NET heap and
enable it to be accessed later from a different [scope](./js-value-scopes).

::: warning
The example code below might need to be updated after
https://github.com/microsoft/node-api-dotnet/issues/197 is resolved.
:::

## Using strong references

A common practice is to save a reference as a member variable to support using the referenced
JS value in a later callback. A strong reference prevents the JS value from being released
until the reference is disposed.

```C#
[JSExport]
public class ReferenceExample : IDisposable
{
    private readonly JSReference _dataReference;

    public ReferenceExample(JSArray data)
    {
        // The constructor must have been invoked from JS, or from .NET
        // on the JS thread. Save a reference to the JS value parameter.
        _dataReference = new JSReference(data);
    }

    public double GetSum()
    {
        // Access the saved data value via the reference.
        // Since the reference is strong, it never returns null.
        // (It throws ObjectDisposedException if disposed.)
        JSArray data = (JSArray)_dataReference.GetValue()!.Value;

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

A weak reference does not prevent the JS value from being released. Therefore it is
necessary to check for null when getting the referenced value:

```C#
JSValue? value = reference.GetValue();
if (value != null)
{
    // Do something with the value.
}
else
{
    // The JS value was released and is no longer available.
}
```
