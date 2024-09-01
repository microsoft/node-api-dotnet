// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// When exporting base classes and subclasses to JavaScript, the base members should be available
// on the subclass instances, and the JS prototype chain should be set up correctly.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const BaseClass = binding.BaseClass;
assert.strictEqual(typeof BaseClass, 'function');

const SubClass = binding.SubClass;
assert.strictEqual(typeof SubClass, 'function');

const baseInstance = new BaseClass(1);
assert.strictEqual(baseInstance.value1, 1);
assert.strictEqual(baseInstance.constructor, BaseClass);
assert(baseInstance instanceof BaseClass);
assert.strictEqual(Object.getPrototypeOf(baseInstance), BaseClass.prototype);

const subInstance = new SubClass(2, 3);
assert.strictEqual(subInstance.value1, 2);
assert.strictEqual(subInstance.value2, 3);
assert.strictEqual(subInstance.constructor, SubClass);
assert(subInstance instanceof SubClass);
assert(subInstance instanceof BaseClass);
assert.strictEqual(Object.getPrototypeOf(subInstance), SubClass.prototype);
assert.strictEqual(Object.getPrototypeOf(SubClass.prototype), BaseClass.prototype);

const IBaseInterface = binding.IBaseInterface;
assert.strictEqual(typeof IBaseInterface, 'function');

const ISubInterface = binding.ISubInterface;
assert.strictEqual(typeof ISubInterface, 'function');

// Note there is no JS prototype chain for interfaces, because .NET interfaces can have
// multiple bases, which cannot be represented as JS prototypes.

const baseInstance2 = new BaseClass({ value1: 11 });
assert.strictEqual(baseInstance2.value1, 11);
const subInstance2 = new SubClass({ value1: 12, value2: 13 });
assert.strictEqual(subInstance2.value1, 12);
assert.strictEqual(subInstance2.value2, 13);
