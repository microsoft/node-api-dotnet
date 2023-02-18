
using System;

namespace NodeApi;

/// <summary>
/// Specifies how .NET types are marshalled to and from JavaScript values.
/// </summary>
[AttributeUsage(
    AttributeTargets.Property |
    AttributeTargets.Parameter |
    AttributeTargets.ReturnValue)]
public sealed class JSMarshalAsAttribute : Attribute
{
    /// <summary>
    /// Specifies how a .NET property, parameter, or return value is marshalled to
    /// and from a JavaScript value.
    /// </summary>
    /// <param name="marshal">One of the available marshaling options.</param>
    public JSMarshalAsAttribute(JSMarshal marshal)
    {
        Value = marshal;
    }

    /// <summary>
    /// Gets the marshaling behavior indicated by the attribute.
    /// </summary>
    public JSMarshal Value { get; }

    /// <summary>
    /// When <see cref="Value" /> is <see cref="JSMarshal.CustomMarshaler" />, gets the type
    /// that handles marshalling. The type must implement <see cref="IJSMarshaler{T}" />,
    /// where the type argument matches the type being of the property, parameter, or
    /// return value the <see cref="JSMarshalAsAttribute" /> is applied to.
    /// </summary>
    public Type? CustomMarshalType { get; init; }
}
