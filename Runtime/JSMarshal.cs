
namespace NodeApi;

/// <summary>
/// Specifies marshalling behavior for <see cref="JSMarshalAsAttribute" />.
/// </summary>
public enum JSMarshal
{
    /// <summary>
    /// Default marshalling behavior.
    /// </summary>
    Default,

    /// <summary>
    /// Marshalling is handled by a custom marshaler type. When this is specified,
    /// <see cref="JSMarshalAsAttribute.CustomMarshalType" /> must also be set.
    /// </summary>
    CustomMarshaler,

    /// <summary>
    /// A .NET object is marshalled as a JS external value. External values may not
    /// be accessed by JS, but may be held and passed back to .NET code intact.
    /// </summary>
    ExternalObject,

    /// <summary>
    /// The object is marshalled by value, meaning all public properties are shallow-copied.
    /// This is the default marshalling behavior for .NET structs.
    /// </summary>
    ByValObject,

    /// <summary>
    /// The object is marshalled by reference, meaning only a proxy to the object is passed.
    /// This is the default marshalling behavior for .NET classes and interfaces.
    /// </summary>
    ByRefObject,

    /// <summary>
    /// Collection items are copied into a new collection in the target environment.
    /// This is the default marshalling behavior for .NET arrays.
    /// </summary>
    ByValCollection = ByValObject,

    /// <summary>
    /// The collection is marshalled by reference, meaning only a proxy to the collection is passed.
    /// This is the default marshalling behavior for .NET generic collections.
    /// </summary>
    ByRefCollection = ByRefObject,
}
