// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { Example } from 'aot-npm-package';

// Call a method exported by the .NET module.
const result = Example.hello('.NET AOT');

import assert from 'node:assert';
assert.strictEqual(result, 'Hello .NET AOT!');
