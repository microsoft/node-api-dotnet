// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

#pragma warning disable IDE0060 // Unused parameters
#pragma warning disable IDE0301 // Collection initialization can be simplified

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Microsoft.JavaScript.NodeApi.TestCases;

/// <summary>
/// Tests marshalling of various collection types.
/// </summary>
[JSExport]
public static class Collections
{
    public static class Arrays
    {
        public static string[] ArrayOfString { get; set; } = Array.Empty<string>();

        public static byte[] ArrayOfByte { get; set; } = new[] { (byte)0, (byte)1, (byte)2 };

        public static int[] ArrayOfInt { get; set; } = new int[] { 0, 1, 2 };

        public static ClassObject[] ArrayOfClassObject { get; set; } = new ClassObject[]
        {
            new ClassObject { Value = "A" },
            new ClassObject { Value = "B" },
        };
    }

    public static class Memory
    {
        // Memory<T> is marshalled by reference, but only for primitive element types
        // that correspond to JavaScript typed-array element types.

        public static Memory<byte> MemoryOfByte { get; set; }
            = new Memory<byte>(new[] { (byte)0, (byte)1, (byte)2 });

        public static Memory<int> MemoryOfInt { get; set; }
            = new Memory<int>(new int[] { 0, 1, 2 });

        public static Memory<int> Slice(Memory<int> array, int start, int length)
            => array.Slice(start, length);

        [JSExport(false)] // Memory<T> of a struct type is not supported.
        public static Memory<StructObject> MemoryOfStructObject { get; set; }
            = new Memory<StructObject>(new StructObject[] { new StructObject { Value = "A" } });
    }

    public static class GenericInterfaces
    {
        // Collection interfaces in the System.Collections.Generic namespace are
        // marshalled by reference.

        public static IEnumerable<int> IEnumerableOfInt { get; set; } = new int[] { 0, 1, 2 };

        public static ICollection<int> ICollectionOfInt { get; set; }
            = new List<int>(new int[] { 0, 1, 2 });

        public static IReadOnlyCollection<int> IReadOnlyCollectionOfInt { get; set; }
            = new int[] { 0, 1, 2 };

        public static IList<int> IListOfInt { get; set; } = new List<int>(new int[] { 0, 1, 2 });

        public static IReadOnlyList<int> IReadOnlyListOfInt { get; set; }
            = new List<int>(new int[] { 0, 1, 2 }).AsReadOnly();

        public static ISet<int> ISetOfInt { get; set; } = new HashSet<int>(new int[] { 0, 1, 2 });

#if !NETFRAMEWORK
        public static IReadOnlySet<int> IReadOnlySetOfInt { get; set; }
            = new HashSet<int>(new int[] { 0, 1, 2 });
#endif

        public static IDictionary<int, string> IDictionaryOfIntString { get; set; }
            = new Dictionary<int, string> { { 0, "A" }, { 1, "B" }, { 2, "C" } };

        public static IDictionary<string, IList<ClassObject>> IDictionaryOfStringIList { get; set; }
            = new Dictionary<string, IList<ClassObject>>();
    }

    public static class GenericClasses
    {
        // Collection classes in the System.Collections.Generic namespace are sealed,
        // so they must be marshalled by value (unfortunately).

        public static List<int> ListOfInt { get; set; } = new List<int>(new int[] { 0, 1, 2 });
        public static Stack<int> StackOfInt { get; set; } = new Stack<int>(new int[] { 0, 1, 2 });
        public static Queue<int> QueueOfInt { get; set; } = new Queue<int>(new int[] { 0, 1, 2 });

        public static HashSet<int> HashSetOfInt { get; set; }
            = new HashSet<int>(new int[] { 0, 1, 2 });
        public static SortedSet<int> SortedSetOfInt { get; set; }
            = new SortedSet<int>(new int[] { 0, 1, 2 });

        public static Dictionary<int, string> DictionaryOfIntString { get; set; }
            = new Dictionary<int, string> { { 0, "A" }, { 1, "B" }, { 2, "C" } };
        public static SortedDictionary<int, string> SortedDictionaryOfIntString { get; set; }
            = new SortedDictionary<int, string> { { 0, "A" }, { 1, "B" }, { 2, "C" } };

        public static Dictionary<string, List<ClassObject>> DictionaryOfStringList { get; set; }
            = new Dictionary<string, List<ClassObject>>();
    }

    public static class ObjectModelClasses
    {
        // Collection classes in the System.Collections.ObjectModel namespace are not sealed,
        // so they can be marshalled by reference.

        public static Collection<int> CollectionOfInt { get; set; }
            = new Collection<int>(new List<int>(new int[] { 0, 1, 2 }));
        public static ReadOnlyCollection<int> ReadOnlyCollectionOfInt { get; set; }
            = new ReadOnlyCollection<int>(new int[] { 0, 1, 2 });
        public static ReadOnlyDictionary<int, string> ReadOnlyDictionaryOfIntString { get; set; }
            = new ReadOnlyDictionary<int, string>(
                new Dictionary<int, string> { { 0, "A" }, { 1, "B" }, { 2, "C" } });
    }

    [JSExport(false)] // Non-generic collections are not implemented yet.
    public static class NonGenericInterfaces
    {
        public static System.Collections.IEnumerable IEnumerable { get; set; }
            = new System.Collections.ArrayList(new int[] { 0, 1, 2 });

        public static System.Collections.ICollection ICollection { get; set; }
            = new System.Collections.ArrayList(new int[] { 0, 1, 2 });

        public static System.Collections.IList IList { get; set; }
            = new System.Collections.ArrayList(new int[] { 0, 1, 2 });

        public static System.Collections.IDictionary IDictionary { get; set; }
            = new System.Collections.Hashtable();
    }

    [JSExport(false)] // Non-generic collections are not implemented yet.
    public static class NonGenericClasses
    {
        public static System.Collections.ArrayList ArrayList { get; set; }
            = new System.Collections.ArrayList(new int[] { 0, 1, 2 });

        public static System.Collections.Queue Queue { get; set; }
            = new System.Collections.Queue(new int[] { 0, 1, 2 });

        public static System.Collections.Stack Stack { get; set; }
            = new System.Collections.Stack(new int[] { 0, 1, 2 });

        public static System.Collections.Hashtable Hashtable { get; set; }
            = new System.Collections.Hashtable();
    }
}
