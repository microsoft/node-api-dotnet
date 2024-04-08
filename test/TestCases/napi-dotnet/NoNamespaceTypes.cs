// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections;
using System.Collections.Generic;

#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1050 // Declare types in namespaces
#pragma warning disable CA1822 // Mark members as static

public class NoNamespaceType
{
}

public interface INoNamespaceInterface
{
}

public class NoNamespaceCollection : ICollection<string>
{
    public IEnumerator<string> GetEnumerator() => throw new System.NotImplementedException();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(string item) => throw new System.NotImplementedException();

    public void Clear() => throw new System.NotImplementedException();

    public bool Contains(string item) => throw new System.NotImplementedException();

    public void CopyTo(string[] array, int arrayIndex) => throw new System.NotImplementedException();

    public bool Remove(string item) => throw new System.NotImplementedException();

    public int Count { get; }
    public bool IsReadOnly { get; }
}

public delegate void NoNamespaceDelegate();

namespace Microsoft.JavaScript.NodeApi.TestCases
{
    public class NoNamespaceInterfaceImpl : INoNamespaceInterface
    {
    }

    public class NoNamespaceTypeImpl : NoNamespaceType
    {
    }

    public class NoNamespaceContainer
    {
        public static NoNamespaceDelegate DelegateProperty { get; set; } = null!;

        public List<NoNamespaceType>? GetList(INoNamespaceInterface arg)
        {
            return null;
        }

        public T GetList<T>()
            where T : List<NoNamespaceType>, INoNamespaceInterface
        {
            return default!;
        }
    }
}
