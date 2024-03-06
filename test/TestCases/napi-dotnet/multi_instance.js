// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');
const { Worker, isMainThread, parentPort } = require('worker_threads');

/** @typedef {import('./napi-dotnet')} Binding */
/** @type Binding */
const binding = require('../common').binding;

const testModulePath = require('../common').testModulePath;
const isAot = /\.node$/.test(testModulePath);

if (isMainThread) {
  // Increment the static counter to 2.
  const count1 = binding.Counter.count();
  assert.strictEqual(count1, 1);
  assert.strictEqual(binding.Counter.count(), 2);

  // AOT modules do not get reloaded when the node module is rebound, so their static data is
  // not isolated across threads. But .NET hosted modules do get reloaded with isolated static data,
  // so in that case the worker-thread counter should be independent.
  const expectedCount = isAot ? 3 : 1;
  const worker = new Worker(__filename);
  worker.on('message', (count3) => assert.strictEqual(count3, expectedCount));
} else {
  const count3 = binding.Counter.count();
  parentPort.postMessage(count3)
}
