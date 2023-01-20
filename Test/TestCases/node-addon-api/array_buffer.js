'use strict';

const assert = require('assert');
const testUtil = require('./testUtil');

module.exports = require('./common').runTest(test);

function test (binding) {
  return testUtil.runGCTests([
    'Internal ArrayBuffer',
    () => {
      const test = binding.arrayBuffer.createBuffer();
      binding.arrayBuffer.checkBuffer(test);
      assert.ok(test instanceof ArrayBuffer);

      const test2 = test.slice(0);
      binding.arrayBuffer.checkBuffer(test2);
    },

    'External ArrayBuffer',
    () => {
      const test = binding.arrayBuffer.createExternalBuffer();
      binding.arrayBuffer.checkBuffer(test);
      assert.ok(test instanceof ArrayBuffer);
      assert.strictEqual(0, binding.arrayBuffer.getFinalizeCount());
    },

    () => assert.strictEqual(0, binding.arrayBuffer.getFinalizeCount()),

  //  'External ArrayBuffer with finalizer',
  //  () => {
  //    const test = binding.arrayBuffer.createExternalBufferWithFinalize();
  //    binding.arrayBuffer.checkBuffer(test);
  //    assert.ok(test instanceof ArrayBuffer);
  //    assert.strictEqual(0, binding.arrayBuffer.getFinalizeCount());
  //  },

  //  () => assert.strictEqual(1, binding.arrayBuffer.getFinalizeCount()),

  //  'External ArrayBuffer with finalizer hint',
  //  () => {
  //    const test = binding.arrayBuffer.createExternalBufferWithFinalizeHint();
  //    binding.arrayBuffer.checkBuffer(test);
  //    assert.ok(test instanceof ArrayBuffer);
  //    assert.strictEqual(0, binding.arrayBuffer.getFinalizeCount());
  //  },

  //  () => assert.strictEqual(1, binding.arrayBuffer.getFinalizeCount()),

  //  'ArrayBuffer with constructor',
  //  () => {
  //    assert.strictEqual(true, binding.arrayBuffer.checkEmptyBuffer());
  //    const test = binding.arrayBuffer.createBufferWithConstructor();
  //    binding.arrayBuffer.checkBuffer(test);
  //    assert.ok(test instanceof ArrayBuffer);
  //  },

  //  'ArrayBuffer updates data pointer and length when detached',
  //  () => {
  //    // Detach the ArrayBuffer in JavaScript.
  //    // eslint-disable-next-line no-undef
  //    const mem = new WebAssembly.Memory({ initial: 1 });
  //    binding.arrayBuffer.checkDetachUpdatesData(mem.buffer, () => mem.grow(1));

  //    // Let C++ detach the ArrayBuffer.
  //    const extBuffer = binding.arrayBuffer.createExternalBuffer();
  //    binding.arrayBuffer.checkDetachUpdatesData(extBuffer);
  //  }
  ]);
}
