# Micro-benchmarks for node-api-dotnet APIs

This project contains a set of micro-benchmarks for .NET + JS interop operations, driven by
[BenchmarkDotNet](https://benchmarkdotnet.org/). Most benchmarks run in both CLR and AOT modes,
though the "Dynamic" benchmarks are CLR-only.

> :warning: The benchmarks currently depend on a special branch build of `libnode` being present at
`../bin/<rid>`. This should be resolved with [#107](https://github.com/microsoft/node-api-dotnet/issues/107).

### Run all benchmarks
```
dotnet run -c Release -f net9.0 --filter *
```

### Run only CLR or only AOT benchmarks
```
dotnet run -c Release -f net9.0 --filter *clr.*
dotnet run -c Release -f net9.0 --filter *aot.*
```

### Run a specific benchmark
```
dotnet run -c Release -f net9.0 --filter *clr.CallDotnetFunction
```

### List benchmarks
```
dotnet run -c Release -f net9.0 --list flat
```
