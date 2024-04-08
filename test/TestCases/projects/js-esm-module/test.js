// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import assert from 'assert';
import DefaultClass, {
  method,
  readOnlyProperty,
  ModuleClass,
  ModuleEnum,
} from './bin/js-esm-module.js';

assert.strictEqual(typeof DefaultClass, 'object');
assert.strictEqual(DefaultClass.method('test'), 'test');

assert.strictEqual(readOnlyProperty, 'ROProperty');
assert.strictEqual(typeof method, 'function');
assert.strictEqual(method('test'), 'test');

assert.strictEqual(typeof new ModuleClass('test'), 'object');
assert.strictEqual(new ModuleClass('test').property, 'test');
assert.strictEqual(new ModuleClass('test').method('test2'), 'test2');

assert.strictEqual(typeof ModuleEnum, 'object');
assert.strictEqual(ModuleEnum.None, 0);
assert.strictEqual(ModuleEnum.One, 1);
