'use strict';

const assert = require('assert');

module.exports = require('../../common').runTest(test);

function test(binding) {
  const bool1 = binding.basicTypesBoolean.createBoolean(true);
  assert.strictEqual(bool1, true);

  const bool2 = binding.basicTypesBoolean.createBoolean(false);
  assert.strictEqual(bool2, false);

  const bool3 = binding.basicTypesBoolean.createBooleanFromPrimitive(true);
  assert.strictEqual(bool3, true);

  const bool4 = binding.basicTypesBoolean.createBooleanFromPrimitive(false);
  assert.strictEqual(bool4, false);
}
