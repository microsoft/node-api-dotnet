const assert = require('assert');

process.env['TEST_DOTNET_MODULE_INIT_EXPORT'] = 'test';

const binding = require('../common').binding;

assert.strictEqual(typeof binding, 'string');
assert.strictEqual(binding, 'test');
