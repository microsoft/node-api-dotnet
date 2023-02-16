const assert = require('assert');
const common = require('../node-addon-api/common');

common.runTest(async function (binding) {
  const result = await binding.async_method('buddy');
  assert.strictEqual(result, 'Hey buddy!');
});

