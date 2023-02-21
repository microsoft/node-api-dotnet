# node-api-dotnet Development Notes

## Build
```bash
dotnet build
```

## Test
```bash
dotnet test
```

Or to run a subset of test cases that match a filter:
```bash
dotnet test --filter "DisplayName~hello"
```

The list of test cases is automatically derived from the set of `.js` files under the `Test/TestCases` directory. Within each subdirectory there, all `.cs` files are compiled into one assembly, then all `.js` test files execute against the assembly.

Most test cases run twice, once for "hosted" CLR mode and once for AOT ahead-of-time compiled mode with no CLR.

## Roadmap

[node-api-dotnet tasks](https://github.com/users/jasongin/projects/1/views/1)
