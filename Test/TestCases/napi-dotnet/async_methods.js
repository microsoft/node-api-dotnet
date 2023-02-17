const assert = require('assert');
const common = require('../node-addon-api/common');

/** @type {import('./napi-dotnet')} */
const binding = require('../node-addon-api/common').binding;

common.runTest(async () => {
  const result = await binding.async_method('buddy');
  assert.strictEqual(result, 'Hey buddy!');

  const result2 = await binding.async_method_cs('buddy');
  assert.strictEqual(result, 'Hey buddy!');
});

