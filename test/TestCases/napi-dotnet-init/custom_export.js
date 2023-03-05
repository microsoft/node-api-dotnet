const assert = require('assert');

process.env['TEST_DOTNET_MODULE_INIT_EXPORT'] = 'test';

// Load the addon module, using either hosted or native AOT mode.
const dotnetModule = process.env['TEST_DOTNET_MODULE_PATH'];
const dotnetHost = process.env['TEST_DOTNET_HOST_PATH'];
const binding = dotnetHost ? require(dotnetHost).require(dotnetModule) : require(dotnetModule);

assert.strictEqual(typeof binding, 'string');
assert.strictEqual(binding, 'test');
