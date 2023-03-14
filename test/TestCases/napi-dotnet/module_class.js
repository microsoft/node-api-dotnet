// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

assert.strictEqual(typeof binding, 'object');

assert.strictEqual(binding.moduleProperty, 'test');
assert.strictEqual(binding.moduleMethod('test'), 'Hello test!');
assert.strictEqual(binding.mergedProperty, 'test');
assert.strictEqual(binding.mergedMethod('test'), 'Hello test!');

/*
// Delete the cached binding. This should invoke the module Dispose() method.
// TODO: With CLR hosting, there should be a way to delete one .NET module.
delete require.cache[dotnetHost];
delete require.cache[dotnetModule];
*/
