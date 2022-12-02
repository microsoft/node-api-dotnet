const assert = require('assert');

// Load the addon module.
const test = require(process.env['TEST_NODE_API_MODULE_PATH']);

// Call a method exported by the addon module.
const result = test.hello('world');

assert(result == "Hello world!");
