// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { Example } from './bin/aot-module.js';

// Call a method exported by the .NET module.
const result = Example.hello('.NET AOT');

import assert from 'assert';
assert.strictEqual(result, 'Hello .NET AOT!');
