// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.JavaScript.NodeApi.Examples.Fluid;

// This ISharedMap interface is not currently used, because the JS marshaller has
// better support for IDictionary.
public interface ISharedMap<T> : IDictionary<string, T>
{
}

public interface ISharedMapValueChangedEvent
{
    public string Key { get; }

    public JSValue PreviousValue { get; }
}
