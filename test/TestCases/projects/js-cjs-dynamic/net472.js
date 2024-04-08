// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');
const dotnet = require('node-api-dotnet/net472');

require('./bin/mscorlib');

dotnet.System.Console.WriteLine(`Hello from .NET ${dotnet.runtimeVersion}!`);
assert.strictEqual(dotnet.frameworkMoniker, 'net472');
