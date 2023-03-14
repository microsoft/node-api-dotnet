// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const Example = require('./bin/aot-module').Example;

// Call a method exported by the .NET module.
const result = Example.hello('.NET AOT');

const assert = require('assert');
assert.strictEqual(result, 'Hello .NET AOT!');
