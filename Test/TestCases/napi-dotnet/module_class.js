const assert = require('assert');

// Load the addon module, using either hosted or native AOT mode.
const dotnetModule = process.env['TEST_DOTNET_MODULE_PATH'];
const dotnetHost = process.env['TEST_DOTNET_HOST_PATH'];
const binding = dotnetHost ? require(dotnetHost).require(dotnetModule) : require(dotnetModule);

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
