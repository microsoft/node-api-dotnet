// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import assert from 'assert';
import dotnet from 'node-api-dotnet/net10.0';

import './bin/System.Runtime.js';
import './bin/System.Console.js';

dotnet.System.Console.WriteLine(`Hello from .NET ${dotnet.runtimeVersion}!`);
assert.strictEqual(dotnet.frameworkMoniker, 'net10.0');
