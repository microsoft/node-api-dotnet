# Performance

.NET / JS interop is fast because:
 - Marshaling does not use JSON serialization.
 - Compile-time or runtime [code generation](./js-dotnet-marshalling#marshalling-code-generation)
   avoids reflection.
 - Use of shared memory and proxies minimizes data transfer.
 - Use of modern C# like `struct`, `Span<T>`, and `stackalloc` minimizes heap allocation & copying.

## Performance comparison vs Edge.js
Warm JS to .NET calls are nearly twice as fast when compared to
[`edge-js`](https://github.com/agracio/edge-js) using
[that project's benchmark](https://github.com/tjanczuk/edge/wiki/Performance).

|      |  HTTP | Edge.js | Node API .NET | AOT | JS (baseline) |
|-----:|------:|--------:|--------------:|----:|--------------:|
| Cold | 32039 |   38761 |          9355 | 362 |          1680 |
| Warm |  2003 |      87 |            54 |  47 |            23 |

Numbers are _microseconds_. "Warm" is an average of 10000 .NET -> JS calls (passing a medium-size object).
