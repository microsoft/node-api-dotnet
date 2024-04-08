// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');
const testModule = require('./bin/js-cjs-module');

assert.strictEqual(testModule.readOnlyProperty, 'ROProperty');
assert.strictEqual(testModule.readWriteProperty, 'RWProperty');
testModule.readWriteProperty = 'test';
assert.strictEqual(testModule.readWriteProperty, 'test');
assert.strictEqual(typeof testModule.method, 'function');
assert.strictEqual(testModule.method('test'), 'test');

const { ModuleClass } = testModule;
assert.strictEqual(typeof new ModuleClass('test'), 'object');
assert.strictEqual(new ModuleClass('test').property, 'test');
assert.strictEqual(new ModuleClass('test').method('test2'), 'test2');

const { ModuleEnum } = testModule;
assert.strictEqual(typeof ModuleEnum, 'object');
assert.strictEqual(ModuleEnum.None, 0);
assert.strictEqual(ModuleEnum.One, 1);
