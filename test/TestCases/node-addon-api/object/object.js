'use strict';

const assert = require('assert');

module.exports = require('../../common').runTest(test);

function test(binding) {

  function assertPropertyIs(obj, key, attribute) {
    const propDesc = Object.getOwnPropertyDescriptor(obj, key);
    assert.ok(propDesc);
    assert.ok(propDesc[attribute]);
  }

  function assertPropertyIsNot(obj, key, attribute) {
    const propDesc = Object.getOwnPropertyDescriptor(obj, key);
    assert.ok(propDesc);
    assert.ok(!propDesc[attribute]);
  }

  function testDefineProperties() {
    const obj = {};
    binding.object.defineProperties(obj);

    // accessors
    assertPropertyIsNot(obj, 'readonlyAccessor', 'enumerable');
    assertPropertyIsNot(obj, 'readonlyAccessor', 'configurable');
    assert.strictEqual(obj.readonlyAccessor, true);

    assertPropertyIsNot(obj, 'readonlyAccessorWithUserData', 'enumerable');
    assertPropertyIsNot(obj, 'readonlyAccessorWithUserData', 'configurable');
    assert.strictEqual(obj.readonlyAccessorWithUserData, 1234);

    assertPropertyIsNot(obj, 'readWriteAccessor', 'enumerable');
    assertPropertyIsNot(obj, 'readWriteAccessor', 'configurable');
    obj.readWriteAccessor = false;
    assert.strictEqual(obj.readWriteAccessor, false);
    obj.readWriteAccessor = true;
    assert.strictEqual(obj.readWriteAccessor, true);

    assertPropertyIsNot(obj, 'readWriteAccessorWithUserData', 'enumerable');
    assertPropertyIsNot(obj, 'readWriteAccessorWithUserData', 'configurable');
    obj.readWriteAccessorWithUserData = 2;
    assert.strictEqual(obj.readWriteAccessorWithUserData, 2);
    obj.readWriteAccessorWithUserData = -14;
    assert.strictEqual(obj.readWriteAccessorWithUserData, -14);

    // values
    assertPropertyIsNot(obj, 'readonlyValue', 'writable');
    assertPropertyIsNot(obj, 'readonlyValue', 'enumerable');
    assertPropertyIsNot(obj, 'readonlyValue', 'configurable');
    assert.strictEqual(obj.readonlyValue, true);

    assertPropertyIs(obj, 'readWriteValue', 'writable');
    assertPropertyIsNot(obj, 'readWriteValue', 'enumerable');
    assertPropertyIsNot(obj, 'readWriteValue', 'configurable');
    obj.readWriteValue = false;
    assert.strictEqual(obj.readWriteValue, false);
    obj.readWriteValue = true;
    assert.strictEqual(obj.readWriteValue, true);

    assertPropertyIsNot(obj, 'enumerableValue', 'writable');
    assertPropertyIs(obj, 'enumerableValue', 'enumerable');
    assertPropertyIsNot(obj, 'enumerableValue', 'configurable');

    assertPropertyIsNot(obj, 'configurableValue', 'writable');
    assertPropertyIsNot(obj, 'configurableValue', 'enumerable');
    assertPropertyIs(obj, 'configurableValue', 'configurable');

    // functions
    assertPropertyIsNot(obj, 'function', 'writable');
    assertPropertyIsNot(obj, 'function', 'enumerable');
    assertPropertyIsNot(obj, 'function', 'configurable');
    assert.strictEqual(obj.function(), true);
    assert.strictEqual(obj.functionWithUserData(), obj.readonlyAccessorWithUserData);
  }

  testDefineProperties();

  {
    const obj = {};
    const testSym = Symbol('testSym');
    binding.object.defineValueProperty(obj, testSym, 1);
    assert.strictEqual(obj[testSym], 1);
  }

  {
    const testSym = Symbol('testSym');
    const obj = { one: 1, two: 2, three: 3, [testSym]: 4 };
    const arr = binding.object.getPropertyNames(obj);
    assert.deepStrictEqual(arr, ['one', 'two', 'three']);
  }

  {
    const magicObject = binding.object.createObjectUsingMagic();
    assert.deepStrictEqual(magicObject, {
      0: 0,
      42: 120,
      cp_false: false,
      cp_true: true,
      s_true: true,
      s_false: false,
      '0.0f': 0,
      '0.0': 0,
      '-1': -1,
      foo2: 'foo',
      foo4: 'foo',
      circular: magicObject,
      circular2: magicObject
    });
  }

  {
    function Ctor() { }

    assert.strictEqual(binding.object.instanceOf(new Ctor(), Ctor), true);
    assert.strictEqual(binding.object.instanceOf(new Ctor(), Object), true);
    assert.strictEqual(binding.object.instanceOf({}, Ctor), false);
    assert.strictEqual(binding.object.instanceOf(null, Ctor), false);
  }

  if ('sum' in binding.object) {
    {
      const obj = {
        '-forbid': -0x4B1D,
        '-feedcode': -0xFEEDC0DE,
        '+office': +0x0FF1CE,
        '+forbid': +0x4B1D,
        '+deadbeef': +0xDEADBEEF,
        '+feedcode': +0xFEEDC0DE
      };

      let sum = 0;
      for (const key in obj) {
        sum += obj[key];
      }

      assert.strictEqual(binding.object.sum(obj), sum);
    }

    {
      const obj = new Proxy({
        '-forbid': -0x4B1D,
        '-feedcode': -0xFEEDC0DE,
        '+office': +0x0FF1CE,
        '+forbid': +0x4B1D,
        '+deadbeef': +0xDEADBEEF,
        '+feedcode': +0xFEEDC0DE
      }, {
        getOwnPropertyDescriptor(target, p) {
          throw new Error('getOwnPropertyDescriptor error');
        },
        ownKeys(target) {
          throw new Error('ownKeys error');
        }
      });

      assert.throws(() => {
        binding.object.sum(obj);
      }, /ownKeys error/);
    }
  }

  if ('increment' in binding.object) {
    const obj = {
      a: 0,
      b: 1,
      c: 2
    };

    binding.object.increment(obj);

    assert.deepStrictEqual(obj, {
      a: 1,
      b: 2,
      c: 3
    });
  }
}
