// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const dotnet = require('node-api-dotnet/net472');

/** @type {import('./bin/dotnet-module').Example} */
const Example = dotnet.require('./bin/dotnet-module').Example;

// Call a method exported by the .NET module.
const result = Example.hello('.NET');

const assert = require('assert');
assert.strictEqual(result, 'Hello .NET!');
