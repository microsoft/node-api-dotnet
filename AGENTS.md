# Repository guide for AI agents

This file orients automated coding agents (and new contributors) working in this repository. It is
intentionally short; it points at the authoritative docs rather than duplicating them.

`node-api-dotnet` provides high-performance, in-process interop between .NET and JavaScript, built on
[Node-API](https://nodejs.org/api/n-api.html). It ships a runtime library, a native + managed host,
a C# source generator, and a TypeScript type-definitions generator.

## Read this first: the runtime model

Most recurring misunderstandings in this codebase come from the JavaScript environment / .NET
context lifetime model. **Read [docs/concepts/runtime-model.md](docs/concepts/runtime-model.md)
before reasoning about environments, teardown, threading, or object lifetime.** The facts that are
most often gotten wrong:

- **Node.js creates one `napi_env` per loaded native module.** A Native AOT module is `1 env : 1`
  `JSRuntimeContext`. A managed module runs a native host and a managed host that **share one env**
  (two instance-data slots) — that is the *only* case where two contexts share an env. Two
  independently compiled AOT addons are two separate modules and therefore get **two different
  envs**; they never share one, so their per-environment state cannot collide.
- **`node::Environment` is not `napi_env`.** There is one `node::Environment` per V8 isolate / worker
  thread, and **zero or more `napi_env` per `node::Environment`** (one per native module). An
  environment cleanup hook is associated with the `node::Environment`; the **instance-data finalizer
  is per `napi_env`.** Per-context teardown keys off the instance-data finalizer, not the cleanup
  hook.
- **Finalizers run during environment teardown, where calling into JavaScript is forbidden.** Resolve
  the context with `JSRuntimeContext.FromEnv(env)`, never by dereferencing a finalize hint that may be
  freed, and assume no ordering between wrapped-object finalizers and the instance-data finalizer.
- **`napi_value` / `JSValue` are valid only within their `JSValueScope` and only on the JS thread.**
  To keep a value beyond its scope, hold a `JSReference` (`napi_ref`). There are three scope types —
  runtime-context, handle, and escapable — and a module boundary starts a fresh module holder so each
  loaded module resolves its own module instance.

## Build, format, and test

Full details are in [README-DEV.md](README-DEV.md). The essentials:

```bash
dotnet build
dotnet format --severity info --verbosity detailed   # PR builds FAIL if formatting is non-compliant
dotnet pack                                           # required before tests (the generator is consumed as a local package)
dotnet test
```

- **Run `dotnet format` after code changes and before tests** — formatting is a CI gate.
- **`dotnet pack` is required before `dotnet test`**, and again after any change to the source
  generator, because tests consume the generator through the locally built NuGet package. Use
  `-c Release` for release-configuration testing.
- Most test cases run twice: once in hosted CLR mode and once in Native AOT mode. Test cases are
  derived from the `.js` files under `test/TestCases`.

## Conventions

- Follow the existing code style enforced by `.editorconfig` (American English in code, comments, and
  docs).
- See [docs/contributing.md](docs/contributing.md) for contribution guidelines, and
  [docs/NodeApi-Layers.md](docs/NodeApi-Layers.md) for how the assemblies and namespaces are layered.
- Keep code comments minimal: add one only to explain a non-obvious "why" that the code cannot show.
