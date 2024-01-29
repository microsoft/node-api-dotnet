// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import assert from 'assert';
import dotnet from 'node-api-dotnet/net472.js';

import './bin/mscorlib.js';

dotnet.System.Console.WriteLine(`Hello from .NET ${dotnet.runtimeVersion}!`);
assert.strictEqual(dotnet.frameworkMoniker, 'net472');
