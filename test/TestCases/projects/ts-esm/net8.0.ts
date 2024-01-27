// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import * as assert from 'assert';
import dotnet from 'node-api-dotnet/net8.0.js';

import './bin/System.Runtime.js';
import './bin/System.Console.js';

dotnet.System.Console.WriteLine(`Hello from .NET ${dotnet.runtimeVersion}!`);
assert.strictEqual(dotnet.frameworkMoniker, 'net8.0');
