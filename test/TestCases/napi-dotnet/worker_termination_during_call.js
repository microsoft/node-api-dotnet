// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Regression test for terminating a Worker while it is executing a .NET call.
//
// The managed method calls back into JS to signal that it has started, then blocks before
// throwing. The parent terminates the Worker while the managed call is blocked. When the
// exception reaches JSError.ThrowError(Exception), the environment is already terminating.
// Attempting to construct another JS error in that state used to let a JSException escape the
// native callback boundary, aborting the entire process on Linux with exit code 134.

const { Worker, isMainThread, parentPort } = require('worker_threads');

if (isMainThread) {
  const worker = new Worker(__filename);
  worker.on('error', (error) => { throw error; });
  worker.once('message', async (message) => {
    if (message !== 'started') {
      throw new Error(`Unexpected worker message: ${message}`);
    }

    await worker.terminate();
  });
} else {
  const binding = require('../common').binding;
  binding.Errors.waitAndThrow(250, {
    started() {
      parentPort.postMessage('started');
    },
  });
}
