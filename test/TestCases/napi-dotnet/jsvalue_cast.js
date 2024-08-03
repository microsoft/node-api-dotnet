// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const JSValueCast = binding.JSValueCast;
assert.strictEqual(typeof JSValueCast, 'object');

assert.strictEqual(typeof JSValueCast.valueAsAbortSignal, 'function');
assert.strictEqual(typeof JSValueCast.valueIsAbortSignal, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToAbortSignal, 'function');
assert.strictEqual(JSValueCast.valueAsAbortSignal(AbortSignal.abort()), "ok");
assert.strictEqual(JSValueCast.valueAsAbortSignal({}), "failed");
assert.strictEqual(JSValueCast.valueIsAbortSignal(AbortSignal.abort()), "ok");
assert.strictEqual(JSValueCast.valueIsAbortSignal({}), "failed");
assert.strictEqual(JSValueCast.valueCastToAbortSignal(AbortSignal.abort()), "ok");
assert.strictEqual(JSValueCast.valueCastToAbortSignal({}), "failed");

assert.strictEqual(typeof JSValueCast.valueAsArray, 'function');
assert.strictEqual(typeof JSValueCast.valueIsArray, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToArray, 'function');
assert.strictEqual(JSValueCast.valueAsArray(["foo"]), "ok");
assert.strictEqual(JSValueCast.valueAsArray({}), "failed");
assert.strictEqual(JSValueCast.valueIsArray(["foo"]), "ok");
assert.strictEqual(JSValueCast.valueIsArray({}), "failed");
assert.strictEqual(JSValueCast.valueCastToArray(["foo"]), "ok");
assert.strictEqual(JSValueCast.valueCastToArray({}), "failed");

const asyncIterable = {
  async *[Symbol.asyncIterator]() {
    yield await Promise.resolve("a");
  },
};
const asyncIterable2 = Object.create(asyncIterable);

assert.strictEqual(typeof JSValueCast.valueAsAsyncIterable, 'function');
assert.strictEqual(typeof JSValueCast.valueIsAsyncIterable, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToAsyncIterable, 'function');
assert.strictEqual(JSValueCast.valueAsAsyncIterable(asyncIterable), "ok");
assert.strictEqual(JSValueCast.valueAsAsyncIterable(asyncIterable2), "ok");
assert.strictEqual(JSValueCast.valueAsAsyncIterable({}), "failed");
assert.strictEqual(JSValueCast.valueIsAsyncIterable(asyncIterable), "ok");
assert.strictEqual(JSValueCast.valueIsAsyncIterable(asyncIterable2), "ok");
assert.strictEqual(JSValueCast.valueIsAsyncIterable({}), "failed");
assert.strictEqual(JSValueCast.valueCastToAsyncIterable(asyncIterable), "ok");
assert.strictEqual(JSValueCast.valueCastToAsyncIterable(asyncIterable2), "ok");
assert.strictEqual(JSValueCast.valueCastToAsyncIterable({}), "failed");

assert.strictEqual(typeof JSValueCast.valueAsBigInt, 'function');
assert.strictEqual(typeof JSValueCast.valueIsBigInt, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToBigInt, 'function');
assert.strictEqual(JSValueCast.valueAsBigInt(42n), "ok");
assert.strictEqual(JSValueCast.valueAsBigInt({}), "failed");
assert.strictEqual(JSValueCast.valueIsBigInt(42n), "ok");
assert.strictEqual(JSValueCast.valueIsBigInt({}), "failed");
assert.strictEqual(JSValueCast.valueCastToBigInt(42n), "ok");
assert.strictEqual(JSValueCast.valueCastToBigInt({}), "failed");

assert.strictEqual(typeof JSValueCast.valueAsDate, 'function');
assert.strictEqual(typeof JSValueCast.valueIsDate, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToDate, 'function');
assert.strictEqual(JSValueCast.valueAsDate(new Date()), "ok");
assert.strictEqual(JSValueCast.valueAsDate({}), "failed");
assert.strictEqual(JSValueCast.valueIsDate(new Date()), "ok");
assert.strictEqual(JSValueCast.valueIsDate({}), "failed");
assert.strictEqual(JSValueCast.valueCastToDate(new Date()), "ok");
assert.strictEqual(JSValueCast.valueCastToDate({}), "failed");

assert.strictEqual(typeof JSValueCast.valueAsFunction, 'function');
assert.strictEqual(typeof JSValueCast.valueIsFunction, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToFunction, 'function');
assert.strictEqual(JSValueCast.valueAsFunction(() => { }), "ok");
assert.strictEqual(JSValueCast.valueAsFunction({}), "failed");
assert.strictEqual(JSValueCast.valueIsFunction(() => { }), "ok");
assert.strictEqual(JSValueCast.valueIsFunction({}), "failed");
assert.strictEqual(JSValueCast.valueCastToFunction(() => { }), "ok");
assert.strictEqual(JSValueCast.valueCastToFunction({}), "failed");
