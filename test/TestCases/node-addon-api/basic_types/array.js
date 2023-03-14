// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

'use strict';
const assert = require('assert');

module.exports = require('../../common').runTest(test);

function test(binding) {
  // create empty array
  const array = binding.basicTypesArray.createArray();
  assert.strictEqual(binding.basicTypesArray.getLength(array), 0);

  // create array with length
  const arrayWithLength = binding.basicTypesArray.createArray(10);
  assert.strictEqual(binding.basicTypesArray.getLength(arrayWithLength), 10);

  // set function test
  binding.basicTypesArray.set(array, 0, 10);
  binding.basicTypesArray.set(array, 1, 'test');
  binding.basicTypesArray.set(array, 2, 3.0);

  // check length after set data
  assert.strictEqual(binding.basicTypesArray.getLength(array), 3);

  // get function test
  assert.strictEqual(binding.basicTypesArray.get(array, 0), 10);
  assert.strictEqual(binding.basicTypesArray.get(array, 1), 'test');
  assert.strictEqual(binding.basicTypesArray.get(array, 2), 3.0);

  // overwrite test
  binding.basicTypesArray.set(array, 0, 5);
  assert.strictEqual(binding.basicTypesArray.get(array, 0), 5);

  // out of index test
  assert.strictEqual(binding.basicTypesArray.get(array, 5), undefined);
}
