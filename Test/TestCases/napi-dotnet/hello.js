const assert = require('assert');

// Load the addon module, using either hosted or native AOT mode.
const dotnetModule = process.env['TEST_DOTNET_MODULE_PATH'];
const dotnetHost = process.env['TEST_DOTNET_HOST_PATH'];

/** @type {import('./napi-dotnet')} */
const binding = dotnetHost ? require(dotnetHost).require(dotnetModule) : require(dotnetModule);

// Call a method exported by the addon module.
const result = binding.hello('world');

assert.strictEqual(result, 'Hello world!');
