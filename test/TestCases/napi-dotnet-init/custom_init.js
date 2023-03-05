const assert = require('assert');

// Load the addon module, using either hosted or native AOT mode.
const dotnetModule = process.env['TEST_DOTNET_MODULE_PATH'];
const dotnetHost = process.env['TEST_DOTNET_HOST_PATH'];
const binding = dotnetHost ? require(dotnetHost).require(dotnetModule) : require(dotnetModule);

assert.strictEqual(typeof binding, 'object');
assert.strictEqual(binding.test, true);
