# Runtime model: environments, lifetimes, and threads

This page describes the foundational runtime model that the rest of the library is built on:
how JavaScript environments map to .NET runtime contexts, how those contexts are torn down, and
the rules for safely holding JavaScript values. The per-feature pages
([JS value scopes](../features/js-value-scopes), [JS references](../features/js-references),
[JS threading & async](../features/js-threading-async),
[Node worker threads](../features/node-workers)) assume the model described here.

If you are extending this library or reviewing a change to it, read this first — several parts of
the design only make sense once the environment/module relationship is clear.

## Environments and module instances

**Node.js creates a unique `napi_env` for each native module it loads.** When a module is
registered, `napi_module_register_by_symbol` (in Node's `src/node_api.cc`) calls `NodeApiEnv::New`,
which mints a fresh `napi_env` for that specific module. So the mapping is per-module, not
per-process and not per-isolate.

That gives three deployment shapes:

| Shape | `napi_env` : `JSRuntimeContext` | Notes |
| --- | --- | --- |
| **Native AOT module** | 1 : 1 | The `.node` file *is* the module, so Node makes one env and the module owns one context. |
| **Managed module** (`.node` native host + managed host) | 1 : 2 | The native host and the managed host run in **separate .NET runtimes** but share the **same** env. Each registers its own context. |
| **Embedding** (a .NET app hosting `libnode`) | 1 : 1 per env | The .NET app creates and owns each environment's context. |

The managed-module case is the only one where two contexts share a single `napi_env`. The native
host (`NativeHost`, AOT-compiled into the `.node`) initializes first and hands the same env to the
managed host (`ManagedHost`, loaded into the default .NET runtime); both create a `JSRuntimeContext`
for that one env. This is deliberate and bounded — there are never more than these two.

**A consequence worth stating explicitly:** two independently compiled AOT addons are two separate
native modules, so Node gives them **two different `napi_env` instances**. They never share one
environment, and their per-environment state never collides. The same is true for an AOT addon
loaded alongside the managed host: different modules, different envs.

## `node::Environment` vs `napi_env` vs isolate/worker

These three are easy to conflate, but they nest at different granularities:

- **`node::Environment`** — one per V8 isolate, i.e. one per Node.js **worker thread** (the main
  thread is a worker too). It owns the event loop and the environment-cleanup hook list.
- **`napi_env`** — **zero or more per `node::Environment`**, one for each native module loaded into
  that worker. Node-API objects, references, and instance data all belong to a specific `napi_env`.
- **isolate/worker thread** — the JS execution thread. All JS values and value scopes have affinity
  to it.

Two teardown callbacks live at these different levels, and the difference matters:

- An **environment cleanup hook** (`napi_add_env_cleanup_hook`, backed by
  `node::AddEnvironmentCleanupHook`) is associated with the **`node::Environment`**. It fires once
  when the whole worker shuts down.
- The **instance-data finalizer** (registered with `napi_set_instance_data`) is associated with a
  **single `napi_env`**. It fires when that module's environment is torn down.

Because a `JSRuntimeContext` is scoped to one `napi_env`, this library keys per-context teardown off
the **instance-data finalizer**, not the environment cleanup hook. Using the cleanup hook would be
both too coarse (one worker may host several envs) and wrongly timed for per-module lifetime.

## Instance-data ownership (`JSRuntimeContext`)

Each context roots itself with a `GCHandle` stored in its env's instance-data block. Because the
managed-module case puts two contexts (in two separate .NET runtimes/GC heaps) on one env, the block
has **two slots**:

- **slot 0** — the module context: managed host, AOT module, or embedding.
- **slot 1** — the native host context.

There are exactly two slots because the native-host + managed-host pair is the only case where two
contexts share an env. A runtime **reads and writes only its own slot**, so it never dereferences a
`GCHandle` that belongs to the other runtime's GC heap (which would be undefined behavior).

`JSRuntimeContext.FromEnv(napi_env)` resolves the calling runtime's context from its slot. This is
how callback dispatch and finalizers recover the context when no scope is yet current on the thread.

At environment teardown the instance-data finalizer disposes the owning context, which **clears its
slot and frees the rooting `GCHandle`**. Disposing a host context cascades synchronously to the
other slot's context, so once every context on the env is gone the finalizer **frees the block**.
Freeing it there is no less safe than keeping it: a finalizer that called `FromEnv` after the
instance-data finalizer would already be reading Node's own freed finalizer record (Node does not
null its instance-data pointer), so retaining the block never protected that case. The block is not
nulled out via `napi_set_instance_data` — that would delete the finalizer record Node is running and
then double-free it.

## JavaScript value scopes

Every `JSValue` belongs to a [`JSValueScope`](../features/js-value-scopes). There are three scope
types, each created by a static factory:

- **Runtime-context scope** — `JSValueScope.CreateRuntimeScope(env, context)`. References a
  `JSRuntimeContext` and marks a call/context boundary. It opens no napi handle scope. This is the
  scope opened at a module entry point or a callback into .NET.
- **Handle scope** — `JSValueScope.CreateHandleScope()`. A nested napi handle scope; JS values
  created within it are released when it is disposed, unless held by a `JSReference`. Use it to
  bound the lifetime of values created in a loop.
- **Escapable scope** — `JSValueScope.CreateEscapableScope()`. Like a handle scope, but one value
  may be promoted to the parent scope with `Escape`, so it survives the inner scope's disposal.

A **module boundary** is a runtime-context scope that starts a *fresh module holder* while reusing
the surrounding context, so each loaded module resolves its own module instance via
`JSValueScope.Current.Module`. This matters when a single managed host loads several generated
modules: without a fresh holder per module, the most recently loaded module's instance would be the
one every module's callbacks resolve.

Scopes nest on a **thread-static stack, and every scope on that stack shares one runtime context.**
Each loaded module has its own stack — a native module is compiled with its own copy of this library,
so even the AOT native host and the CoreCLR managed host that share an env are separate modules whose
stacks never mix — and there is one context per environment per module. So a thread running one
module's code always sees exactly one context: a nested runtime-context scope inherits its parent's
context, and creating one for a *different* context throws. Callback dispatch depends on this,
inheriting the current scope's context (or `FromEnv` when no scope is open) rather than reconciling
several.

## Lifetime of `napi_value` and `napi_ref` (`JSValue` / `JSReference`)

- A `napi_value` (wrapped by [`JSValue`](../features/js-value-scopes)) is valid **only within its
  scope**. Using it after the scope closes throws `JSValueScopeClosedException`. Values passed to a
  .NET callback belong to that call's scope and become invalid when it returns.
- JS values and scopes have **thread affinity**: they may be accessed only from the JS thread that
  owns the environment. Access from another thread throws `JSInvalidThreadAccessException`. To marshal
  work back to the JS thread, use the context's synchronization context (see
  [JS threading & async](../features/js-threading-async)).
- To keep a value **beyond its scope**, create a [`JSReference`](../features/js-references) (a
  `napi_ref`). A strong reference keeps the value alive; a weak one lets it be collected and resolves
  to nothing afterward. A `JSReference` is itself owned by a context and released with it.

### Finalizers and teardown — no JS once the context is disposed

A finalizer (for a wrapped .NET object, an external, or a reference) may run during normal GC while
the environment is still alive, or while the environment is being torn down. **Once the context is
disposed at environment teardown, calling into JavaScript is forbidden.** Finalizer code in this
library follows two rules:

1. **Resolve the context from the env**, via `JSRuntimeContext.FromEnv(env)` — never by dereferencing
   a finalize hint that may already be freed. If `FromEnv` returns no live context (the slot was
   cleared at teardown), the finalizer only frees its own native handle and does no JS work. While the
   context is still live, a finalizer action may run — for example `JSValue.CallFinalizeAction` opens a
   runtime scope to invoke the user action — so this rule is what keeps teardown itself JS-free.
2. **Never assume ordering** among the env's finalizers. Node drains wrapped-object finalizers in no
   guaranteed order, so a finalizer must tolerate the context's slot already being cleared (rule 1).
   The instance-data finalizer frees the block only after every context on the env is disposed.

## See also

- [Project layers](../NodeApi-Layers) — how the assemblies and namespaces are organized.
- [JS value scopes](../features/js-value-scopes), [JS references](../features/js-references) —
  the day-to-day API surface built on this model.
- [JS threading & async](../features/js-threading-async),
  [Node worker threads](../features/node-workers) — the threading rules in practice.
