# Guidance for Claude

See [AGENTS.md](AGENTS.md) for how to work in this repository, including the runtime model that
underlies environments, teardown, threading, and object lifetime, plus the build/format/test steps.

Key reminder: run `dotnet format --severity info --verbosity detailed` after code changes (PR builds
fail on formatting violations), and run `dotnet pack` before `dotnet test`.
