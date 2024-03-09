// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const Overloads = binding.Overloads;
const ClassObject = binding.ClassObject;

// Overloaded constructor
const emptyObj = new Overloads();
assert.strictEqual(emptyObj.intValue, undefined);
assert.strictEqual(emptyObj.stringValue, undefined);

const intObj = new Overloads(1);
assert.strictEqual(intObj.intValue, 1);
assert.strictEqual(intObj.stringValue, undefined);

const stringObj = new Overloads('two');
assert.strictEqual(stringObj.intValue, undefined);
assert.strictEqual(stringObj.stringValue, 'two');

const comboObj = new Overloads(3, 'three');
assert.strictEqual(comboObj.intValue, 3);
assert.strictEqual(comboObj.stringValue, 'three');

const objValue = new ClassObject();
objValue.value = 'test';
const objFromClass = new Overloads(objValue);
assert.strictEqual(objFromClass.stringValue, 'test');

// Overloaded method
const obj1 = new Overloads();
obj1.setValue(1);
assert.strictEqual(obj1.intValue, 1);
assert.strictEqual(obj1.stringValue, undefined);

const obj2 = new Overloads();
obj2.setValue('two');
assert.strictEqual(obj2.intValue, undefined);
assert.strictEqual(obj2.stringValue, 'two');

const obj3 = new Overloads();
obj3.setValue(3, 'three');
assert.strictEqual(obj3.intValue, 3);
assert.strictEqual(obj3.stringValue, 'three');

const obj4 = new Overloads();
obj4.setValue(objValue);
assert.strictEqual(obj4.stringValue, 'test');

const obj5 = new Overloads();
obj5.setDoubleValue(5.0);
assert.strictEqual(obj5.intValue, 5);

