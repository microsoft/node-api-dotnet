using System;

namespace NodeApi;

[Flags]
public enum JSKeyFilter : int
{
    AllProperties = 0,
    Writable = 1 << 0,
    Enumerable = 1 << 1,
    Configurable = 1 << 2,
    SkipStrings = 1 << 3,
    SkipSymbols = 1 << 4,
}
