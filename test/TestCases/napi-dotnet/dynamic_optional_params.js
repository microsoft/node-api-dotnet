// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

// This only tests dynamic invocation because optional parameters
// are not yet implemented for static binding.

const dotnetHost = process.env.TEST_DOTNET_HOST_PATH;
const dotnetVersion = process.env.TEST_DOTNET_VERSION;
const dotnet = require(dotnetHost)
  .initialize(dotnetVersion, dotnetHost.replace(/\.node$/, '.DotNetHost.dll'));

const assemblyPath = process.env.TEST_DOTNET_MODULE_PATH;
const assembly = dotnet.load(assemblyPath);
const OptionalParameters = dotnet.Microsoft.JavaScript.NodeApi.TestCases.OptionalParameters;

assert.strictEqual('a,(null)', OptionalParameters.DefaultNull('a'));
assert.strictEqual('True,False', OptionalParameters.DefaultFalse(true));
assert.strictEqual('1,0', OptionalParameters.DefaultZero(1));
assert.strictEqual('a,', OptionalParameters.DefaultEmptyString('a'));
assert.strictEqual('a,b,0', OptionalParameters.Multiple('a', 'b'));
