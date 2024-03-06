// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

// This only tests dynamic invocation because optional parameters
// are not yet implemented for static binding.

const dotnet = require('../common').dotnet;

const assemblyPath = process.env.NODE_API_TEST_MODULE_PATH;
const assembly = dotnet.load(assemblyPath);
const OptionalParameters = dotnet.Microsoft.JavaScript.NodeApi.TestCases.OptionalParameters;

assert.strictEqual('a,(null)', OptionalParameters.DefaultNull('a'));
assert.strictEqual('True,False', OptionalParameters.DefaultFalse(true));
assert.strictEqual('1,0', OptionalParameters.DefaultZero(1));
assert.strictEqual('a,', OptionalParameters.DefaultEmptyString('a'));
assert.strictEqual('a,b,0', OptionalParameters.Multiple('a', 'b'));
