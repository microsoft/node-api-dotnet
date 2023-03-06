using System;

namespace Microsoft.JavaScript.NodeApi;

[Flags]
public enum JSPropertyAttributes : int
{
    Default = 0,
    Writable = 1 << 0,
    Enumerable = 1 << 1,
    Configurable = 1 << 2,
    Static = 1 << 10,
    DefaultMethod = Writable | Configurable,
    DefaultProperty = Writable | Enumerable | Configurable,
}
