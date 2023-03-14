// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');
const { Worker, isMainThread, parentPort } = require('worker_threads');

/** @typedef {import('./napi-dotnet')} Binding */
/** @type Binding */
const binding = require('../common').binding;
const { loadDotnetModule, dotnetHost, dotnetModule } = require('../common');

if (isMainThread) {
  // Increment the static counter to 2.
  const count1 = binding.Counter.count();
  assert.strictEqual(count1, 1);
  assert.strictEqual(binding.Counter.count(), 2);

  // Delete the cached binding.
  // TODO: With CLR hosting, there should be a way to delete one .NET module.
  delete require.cache[dotnetHost];
  delete require.cache[dotnetModule];

  /** @type Binding */
  const rebinding = loadDotnetModule();

  // The static counter should be reinitialized after rebinding.
  const count2 = rebinding.Counter.count();
  assert.notStrictEqual(binding.Counter, rebinding.Counter);

  // AOT modules do not get reloaded when the node module is rebound, so their static data is
  // not isolated. But .NET hosted modules do get reloaded, with isolated static data, so the
  // following assertions only pass for that type of module.
  if (dotnetHost) {
    assert.strictEqual(count2, 1);

    // The static counter should be reinitialized in a worker.
    const worker = new Worker(__filename);
    worker.on('message', (count3) => assert.strictEqual(count3, 1));
  }
} else {
  const count3 = binding.Counter.count();
  parentPort.postMessage(count3)
}
