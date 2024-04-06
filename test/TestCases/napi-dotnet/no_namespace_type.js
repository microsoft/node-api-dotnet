// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

const dotnet = require('../common').dotnet;

// Load the test module using dynamic binding `load()` instead of static binding `require()`.
const assemblyPath = process.env.NODE_API_TEST_MODULE_PATH;
dotnet.load(assemblyPath);

// The unnamespaced type should be skipped.
assert.strictEqual(dotnet.NoNamespaceType, undefined);

assert.notStrictEqual(dotnet.Microsoft.JavaScript.NodeApi.TestCases.NoNamespaceInterfaceImpl, undefined)

assert.notStrictEqual(dotnet.Microsoft.JavaScript.NodeApi.TestCases.NoNamespaceTypeImpl, undefined)

assert.notStrictEqual(dotnet.Microsoft.JavaScript.NodeApi.TestCases.NoNamespaceContainer, undefined)
