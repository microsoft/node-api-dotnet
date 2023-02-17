const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../node-addon-api/common').binding;

// Call a method exported by the addon module.
const result = binding.hello('world');

assert.strictEqual(result, 'Hello world!');
