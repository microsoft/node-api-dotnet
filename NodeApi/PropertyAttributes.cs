using System;

namespace NodeApi;

[Flags]
public enum PropertyAttributes : int
{
	Default = 0,
	Writable = 1 << 0,
	Enumerable = 1 << 1,
	Configurable = 1 << 2,
	Static = 1 << 10,
}
