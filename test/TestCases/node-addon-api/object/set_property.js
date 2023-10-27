// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

'use strict';

const assert = require('assert');

module.exports = require('../../common').runTest(test);

function test(binding) {
  function testSetProperty(nativeSetProperty, key = 'test') {
    const obj = {};
    nativeSetProperty(obj, key, 1);
    assert.strictEqual(obj[key], 1);
  }

  function testShouldThrowErrorIfKeyIsInvalid(nativeSetProperty) {
    assert.throws(() => {
      nativeSetProperty(undefined, 'test', 1);
    }, /Cannot convert undefined or null to object/);
  }

  testSetProperty(binding.object.setPropertyWithNapiValue);
  testSetProperty(binding.object.setPropertyWithNapiWrapperValue);
  testSetProperty(binding.object.setPropertyWithUtf8StyleString);
  testSetProperty(binding.object.setPropertyWithCSharpStyleString);
  testSetProperty(binding.object.setPropertyWithUInt32, 12);

  testShouldThrowErrorIfKeyIsInvalid(binding.object.setPropertyWithNapiValue);
  testShouldThrowErrorIfKeyIsInvalid(binding.object.setPropertyWithNapiWrapperValue);
  testShouldThrowErrorIfKeyIsInvalid(binding.object.setPropertyWithUtf8StyleString);
  testShouldThrowErrorIfKeyIsInvalid(binding.object.setPropertyWithCSharpStyleString);
}
