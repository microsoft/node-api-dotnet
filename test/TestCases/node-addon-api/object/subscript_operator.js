'use strict';

const assert = require('assert');

module.exports = require('../../common').runTest(test);

function test(binding) {
  function testProperty(obj, key, value, nativeGetProperty, nativeSetProperty) {
    nativeSetProperty(obj, key, value);
    assert.strictEqual(nativeGetProperty(obj, key), value);
  }

  testProperty({}, 'key', 'value', binding.object.subscriptGetWithLatin1StyleString, binding.object.subscriptSetWithLatin1StyleString);
  testProperty({}, 'key', 'value', binding.object.subscriptGetWithUtf8StyleString, binding.object.subscriptSetWithUtf8StyleString);
  testProperty({ key: 'override me' }, 'key', 'value', binding.object.subscriptGetWithCSharpStyleString, binding.object.subscriptSetWithCSharpStyleString);
  testProperty({}, 0, 'value', binding.object.subscriptGetAtIndex, binding.object.subscriptSetAtIndex);
  testProperty({ key: 'override me' }, 0, 'value', binding.object.subscriptGetAtIndex, binding.object.subscriptSetAtIndex);
}
