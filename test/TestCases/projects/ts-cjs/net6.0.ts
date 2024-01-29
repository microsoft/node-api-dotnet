// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import * as assert from 'assert';
import * as dotnet from 'node-api-dotnet/net6.0';

import './bin/System.Runtime';
import './bin/System.Console';

dotnet.System.Console.WriteLine(`Hello from .NET ${dotnet.runtimeVersion}!`);
assert.strictEqual(dotnet.frameworkMoniker, 'net6.0');
