'use strict';

const assert = require('assert');
const testUtil = require('../testUtil');

module.exports = require('../common').runTest(test);

function createWeakRef (binding, bindingToTest) {
  return binding.object[bindingToTest]({});
}

function test (binding) {
  let obj1;
  return testUtil.runGCTests([
    'addFinalizer',
    () => {
      obj1 = createWeakRef(binding, 'addFinalizer');
    },
    () => assert.deepStrictEqual(obj1, { finalizerCalled: true })
  ]);
}
