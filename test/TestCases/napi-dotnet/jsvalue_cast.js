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

assert.strictEqual(typeof JSValueCast.valueAsIterable, 'function');
assert.strictEqual(typeof JSValueCast.valueIsIterable, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToIterable, 'function');
assert.strictEqual(JSValueCast.valueAsIterable([]), "ok");
assert.strictEqual(JSValueCast.valueAsIterable({}), "failed");
assert.strictEqual(JSValueCast.valueIsIterable([]), "ok");
assert.strictEqual(JSValueCast.valueIsIterable({}), "failed");
assert.strictEqual(JSValueCast.valueCastToIterable([]), "ok");
assert.strictEqual(JSValueCast.valueCastToIterable({}), "failed");

assert.strictEqual(typeof JSValueCast.valueAsMap, 'function');
assert.strictEqual(typeof JSValueCast.valueIsMap, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToMap, 'function');
assert.strictEqual(JSValueCast.valueAsMap(new Map()), "ok");
assert.strictEqual(JSValueCast.valueAsMap({}), "failed");
assert.strictEqual(JSValueCast.valueIsMap(new Map()), "ok");
assert.strictEqual(JSValueCast.valueIsMap({}), "failed");
assert.strictEqual(JSValueCast.valueCastToMap(new Map()), "ok");
assert.strictEqual(JSValueCast.valueCastToMap({}), "failed");

assert.strictEqual(typeof JSValueCast.valueAsObject, 'function');
assert.strictEqual(typeof JSValueCast.valueIsObject, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToObject, 'function');
assert.strictEqual(JSValueCast.valueAsObject({}), "ok");
assert.strictEqual(JSValueCast.valueAsObject(""), "failed");
assert.strictEqual(JSValueCast.valueIsObject({}), "ok");
assert.strictEqual(JSValueCast.valueIsObject(""), "failed");
assert.strictEqual(JSValueCast.valueCastToObject({}), "ok");
assert.strictEqual(JSValueCast.valueCastToObject(""), "failed");

assert.strictEqual(typeof JSValueCast.valueAsPromise, 'function');
assert.strictEqual(typeof JSValueCast.valueIsPromise, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToPromise, 'function');
assert.strictEqual(JSValueCast.valueAsPromise(Promise.resolve(123)), "ok");
assert.strictEqual(JSValueCast.valueAsPromise({}), "failed");
assert.strictEqual(JSValueCast.valueIsPromise(Promise.resolve(123)), "ok");
assert.strictEqual(JSValueCast.valueIsPromise({}), "failed");
assert.strictEqual(JSValueCast.valueCastToPromise(Promise.resolve(123)), "ok");
assert.strictEqual(JSValueCast.valueCastToPromise({}), "failed");

assert.strictEqual(typeof JSValueCast.valueAsProxy, 'function');
assert.strictEqual(typeof JSValueCast.valueIsProxy, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToProxy, 'function');
assert.strictEqual(JSValueCast.valueAsProxy(new Proxy({}, {})), "ok");
assert.strictEqual(JSValueCast.valueAsProxy("1"), "failed");
assert.strictEqual(JSValueCast.valueIsProxy(new Proxy({}, {})), "ok");
assert.strictEqual(JSValueCast.valueIsProxy("1"), "failed");
assert.strictEqual(JSValueCast.valueCastToProxy(new Proxy({}, {})), "ok");
assert.strictEqual(JSValueCast.valueCastToProxy("1"), "failed");

assert.strictEqual(typeof JSValueCast.valueAsSet, 'function');
assert.strictEqual(typeof JSValueCast.valueIsSet, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToSet, 'function');
assert.strictEqual(JSValueCast.valueAsSet(new Set()), "ok");
assert.strictEqual(JSValueCast.valueAsSet({}), "failed");
assert.strictEqual(JSValueCast.valueIsSet(new Set()), "ok");
assert.strictEqual(JSValueCast.valueIsSet({}), "failed");
assert.strictEqual(JSValueCast.valueCastToSet(new Set()), "ok");
assert.strictEqual(JSValueCast.valueCastToSet({}), "failed");

assert.strictEqual(typeof JSValueCast.valueAsSymbol, 'function');
assert.strictEqual(typeof JSValueCast.valueIsSymbol, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToSymbol, 'function');
assert.strictEqual(JSValueCast.valueAsSymbol(Symbol.iterator), "ok");
assert.strictEqual(JSValueCast.valueAsSymbol({}), "failed");
assert.strictEqual(JSValueCast.valueIsSymbol(Symbol.iterator), "ok");
assert.strictEqual(JSValueCast.valueIsSymbol({}), "failed");
assert.strictEqual(JSValueCast.valueCastToSymbol(Symbol.iterator), "ok");
assert.strictEqual(JSValueCast.valueCastToSymbol({}), "failed");

assert.strictEqual(typeof JSValueCast.valueAsTypedArrayInt8, 'function');
assert.strictEqual(typeof JSValueCast.valueIsTypedArrayInt8, 'function');
assert.strictEqual(typeof JSValueCast.valueCastToTypedArrayInt8, 'function');
assert.strictEqual(JSValueCast.valueAsTypedArrayInt8(new Int8Array(8)), "ok");
assert.strictEqual(JSValueCast.valueAsTypedArrayInt8({}), "failed");
assert.strictEqual(JSValueCast.valueIsTypedArrayInt8(new Int8Array(8)), "ok");
assert.strictEqual(JSValueCast.valueIsTypedArrayInt8({}), "failed");
assert.strictEqual(JSValueCast.valueCastToTypedArrayInt8(new Int8Array(8)), "ok");
assert.strictEqual(JSValueCast.valueCastToTypedArrayInt8({}), "failed");
