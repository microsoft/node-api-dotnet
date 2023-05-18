// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;

namespace Microsoft.JavaScript.NodeApi.Interop;

public static class TypeExtensions
{
    public static string FormatName(this Type type)
    {
        static string FormatNameWithoutNamespace(Type type)
        {
            string typeName = type.Name;
            if (type.IsGenericType)
            {
                int nameEnd = typeName.IndexOf('`');
                if (nameEnd >= 0)
                {
                    typeName = typeName.Substring(0, nameEnd);
                }

                Type[] typeArgs = type.GetGenericArguments();
                if (type.IsGenericTypeDefinition)
                {
                    typeName += '<' + string.Join(",", typeArgs.Select((t) => t.Name)) + '>';
                }
                else
                {
                    typeName += '<' + string.Join(",", typeArgs.Select(FormatName)) + '>';
                }
            }
            return typeName;
        }

        // Include the declaring type(s) of nested types.
        string typeName = FormatNameWithoutNamespace(type);
        Type? declaringType = type.DeclaringType;
        while (declaringType != null)
        {
            typeName = FormatNameWithoutNamespace(declaringType) + '.' + typeName;
            declaringType = declaringType.DeclaringType;
        }

        if (type.Namespace != null)
        {
            typeName = type.Namespace + '.' + typeName;
        }

        return typeName;
    }
}
