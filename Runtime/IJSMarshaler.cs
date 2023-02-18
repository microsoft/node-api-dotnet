
namespace NodeApi;

/// <summary>
/// Interface for a custom implementation of marshaling .NET types to and from JavaScript values.
/// A type that implements this interface can be assigned to
/// <see cref="JSMarshalAsAttribute.CustomMarshalType" />.
/// </summary>
public interface IJSMarshaler<T>
{
    JSValue MarshalToJS(T value);

    T MarshalFromJS(JSValue value);
}
