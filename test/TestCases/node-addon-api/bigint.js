// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

'use strict';
const assert = require('assert');

module.exports = require('../common').runTest(test);

function test (binding) {
  const {
    isLossless,
    isBigInt,
    testInt64,
    testUInt64,
    testWords,
    testWordSpan,
    testBigInteger
  } = binding.bigInt;

  [
    0n,
    -0n,
    1n,
    -1n,
    100n,
    2121n,
    -1233n,
    986583n,
    -976675n,
    98765432213456789876546896323445679887645323232436587988766545658n,
    -4350987086545760976737453646576078997096876957864353245245769809n
  ].forEach((num) => {
    if (num > -(2n ** 63n) && num < 2n ** 63n) {
      assert.strictEqual(testInt64(num), num);
      assert.strictEqual(isLossless(num, true), true);
    } else {
      assert.strictEqual(isLossless(num, true), false);
    }

    if (num >= 0 && num < 2n ** 64n) {
      assert.strictEqual(testUInt64(num), num);
      assert.strictEqual(isLossless(num, false), true);
    } else {
      assert.strictEqual(isLossless(num, false), false);
    }

    assert.strictEqual(isBigInt(num), true);

    assert.strictEqual(num, testWords(num));
    assert.strictEqual(num, testWordSpan(num));
    assert.strictEqual(num, testBigInteger(num, num.toString()));
  });
}
