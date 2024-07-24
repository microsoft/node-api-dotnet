// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// Test the JSRuntimeContext "object map" which keeps track of JS wrapper objects
// for corresponding .NET objects.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const ComplexTypes = binding.ComplexTypes;
assert.strictEqual(typeof ComplexTypes, 'object');

let obj1 = ComplexTypes.classObject;
assert(obj1);

// The same JS wrapper instance should be returned every time.
let obj2 = ComplexTypes.classObject;
assert.strictEqual(obj1, obj2);

// Force the JS wrapper object to be collected.
obj1 = obj2 = undefined;
global.gc();

// A new JS wrapper object should be created.
let obj3 = ComplexTypes.classObject;
assert(obj3);
