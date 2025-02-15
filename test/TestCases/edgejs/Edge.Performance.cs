// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Ported from https://github.com/agracio/edge-js/blob/master/performance/performance.cs

using System;
using Microsoft.JavaScript.NodeApi;

#pragma warning disable IDE0060 // Unused parameter 'input'

namespace Edge.Performance
{
    [JSExport]
    public static class Startup
    {
        public static Book Invoke(Book input)
        {
            var book = new Book
            {
                Title = "Run .NET and node.js in-process with edge.js",
                Author = new Person
                {
                    First = "Anonymous",
                    Last = "Author"
                },
                Year = 2013,
                Price = 24.99,
                Available = true,
                Description = "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Vivamus posuere tincidunt felis, et mattis mauris ultrices quis. Cras molestie, quam varius tincidunt tincidunt, mi magna imperdiet lacus, quis elementum ante nibh quis orci. In posuere erat sed tellus lacinia luctus. Praesent sodales tellus mauris, et egestas justo. In blandit, metus non congue adipiscing, est orci luctus odio, non sagittis erat orci ac sapien. Proin ut est id enim mattis volutpat. Vivamus ultrices dapibus feugiat. In dictum tincidunt eros, non pretium nisi rhoncus in. Duis a lacus et elit feugiat ullamcorper. Mauris tempor turpis nulla. Nullam nec facilisis elit.",
                Picture = new byte[16000],
                Tags = new[] { ".NET", "node.js", "CLR", "V8", "interop" },
            };

            return book;
        }
    }

    [JSExport]
    public struct Book
    {
        public string Title { get; set; }
        public Person Author { get; set; }
        public int Year { get; set; }
        public double Price { get; set; }
        public bool Available { get; set; }
        public string Description { get; set; }
        public Memory<byte> Picture { get; set; }
        public string[] Tags { get; set; }
    }

    [JSExport]
    public struct Person
    {
        public string First { get; set; }
        public string Last { get; set; }
    }
}
