// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Regression test for a worker_threads teardown crash.
//
// When the native host is loaded ONLY inside a Worker (so no other reference keeps the
// module mapped) and the Worker is then terminated, Node.js unloads (dlclose) the addon
// while the worker's OS thread is still exiting. For a NativeAOT host that leaves a
// dangling pthread-key destructor, so the process crashes with SIGSEGV as the thread exits.
// NativeHost.PreventModuleUnload() pins the module to prevent that. This test fails (the
// child node process exits non-zero) if the crash regresses.
//
// This validates the hosted host module (Microsoft.JavaScript.NodeApi.node), which is what
// PreventModuleUnload() pins, so it runs under HostedClrTests only (excluded from
// NativeAotTests, whose generated module has a separate entry point).
//
// The binding is intentionally NOT loaded on the main thread: doing so would keep another
// module reference alive and mask the unload crash (which is why multi_instance.js cannot
// cover this case).

const assert = require('assert');
const { Worker, isMainThread, parentPort } = require('worker_threads');

if (isMainThread) {
  const worker = new Worker(__filename);
  worker.on('error', (err) => { throw err; });
  worker.once('message', (message) => {
    assert.strictEqual(message, 'ready');
    // Let the worker settle, then tear it down. An unfixed host crashes during the
    // worker thread's teardown after the module is unloaded.
    setTimeout(async () => {
      await worker.terminate();
      // Keep the process alive briefly so any teardown crash surfaces as a non-zero
      // exit code instead of being skipped by an immediate process exit.
      setTimeout(() => process.exit(0), 300);
    }, 300);
  });
} else {
  // Load the native host ONLY in the worker.
  const binding = require('../common').binding;
  assert.ok(binding);
  parentPort.postMessage('ready');
}
