# Micro-benchmarks for node-api-dotnet APIs

This project contains a set of micro-benchmarks for .NET + JS interop operations, driven by
[BenchmarkDotNet](https://benchmarkdotnet.org/). Most benchmarks run in both CLR and AOT modes,
though the "Dynamic" benchmarks are CLR-only.

### Run all benchmarks
```
dotnet run -c Release -f net8.0 --filter *
```

### Run only CLR or only AOT benchmarks
```
dotnet run -c Release -f net8.0 --filter *clr.*
dotnet run -c Release -f net8.0 --filter *aot.*
```

### Run a specific benchmark
```
dotnet run -c Release -f net8.0 --filter *clr.CallDotnetFunction
```

### List benchmarks
```
dotnet run -c Release -f net8.0 --list flat
```
