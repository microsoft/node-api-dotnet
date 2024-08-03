// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const JSValueCast = binding.JSValueCast;
assert.strictEqual(typeof JSValueCast, 'object');

assert.strictEqual(typeof JSValueCast.testAbortSignalAs, 'function');
assert.strictEqual(typeof JSValueCast.testAbortSignalIs, 'function');
assert.strictEqual(typeof JSValueCast.testAbortSignalCast, 'function');
assert.strictEqual(JSValueCast.testAbortSignalAs(AbortSignal.abort()), "ok");
assert.strictEqual(JSValueCast.testAbortSignalAs({}), "fail");
assert.strictEqual(JSValueCast.testAbortSignalIs(AbortSignal.abort()), "ok");
assert.strictEqual(JSValueCast.testAbortSignalIs({}), "fail");
assert.strictEqual(JSValueCast.testAbortSignalCast(AbortSignal.abort()), "ok");
assert.strictEqual(JSValueCast.testAbortSignalCast({}), "fail");
