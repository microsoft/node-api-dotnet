// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const ComplexTypes = binding.ComplexTypes;
assert.strictEqual(typeof ComplexTypes, 'object');

assert.strictEqual(ComplexTypes.nullableInt, undefined);
ComplexTypes.nullableInt = 1;
assert.strictEqual(ComplexTypes.nullableInt, 1);
ComplexTypes.nullableInt = null;
assert.strictEqual(ComplexTypes.nullableInt, undefined);

assert.strictEqual(ComplexTypes.nullableString, undefined);
ComplexTypes.nullableString = 'test';
assert.strictEqual(ComplexTypes.nullableString, 'test');
ComplexTypes.nullableString = null;
assert.strictEqual(ComplexTypes.nullableString, undefined);

// Test an exported class.
const ClassObject = binding.ClassObject;
assert.strictEqual(typeof ClassObject, 'function');
const classInstance = new ClassObject();
assert.strictEqual(classInstance.value, undefined);
classInstance.value = 'test';
assert.strictEqual(classInstance.value, 'test');

// Class instances are passed by reference, so a property change on
// one reference should be reflected on the other.
const classInstance2 = classInstance.thisObject();
assert.strictEqual(classInstance2, classInstance);
classInstance2.value = 'test2';
assert.strictEqual(classInstance2.value, classInstance.value);
assert.strictEqual(classInstance.value, 'test2');

// Class instances can be passed via interfaces also.
const iterfaceInstance = ComplexTypes.interfaceObject;
assert.strictEqual(iterfaceInstance.value, 'test');
ComplexTypes.classObject.value = 'test2';
assert.strictEqual(iterfaceInstance.value, 'test2');

// Test an exported struct.
const StructObject = binding.StructObject;
assert.strictEqual(typeof StructObject, 'function');

// Constructing the object in JS does NOT immediately construct a C# instance.
const structInstance = new StructObject();

// This should be null, after struct initialize callback code is generated.
assert.strictEqual(structInstance.value, undefined);
structInstance.value = 'test';
assert.strictEqual(structInstance.value, 'test');

// Struct instances are passed by value, so a property change on
// one reference should NOT be reflected on the other.
const structInstance2 = structInstance.thisObject();
assert.notStrictEqual(structInstance2, structInstance);
structInstance2.value = 'test2';
assert.strictEqual(structInstance2.value, 'test2');
assert.strictEqual(structInstance.value, 'test');

// C# arrays are copied to/from JS, so modifying the returned array doesn't affect the original.
const stringArrayValue = ComplexTypes.stringArray;
assert(Array.isArray(stringArrayValue));
assert.strictEqual(stringArrayValue.length, 0);
assert.notStrictEqual(ComplexTypes.stringArray, stringArrayValue);
assert.deepStrictEqual(ComplexTypes.stringArray, stringArrayValue);
ComplexTypes.stringArray = [ 'test' ];
assert.strictEqual(ComplexTypes.stringArray[0], 'test');
ComplexTypes.stringArray[0] = 'test2';
assert.strictEqual(ComplexTypes.stringArray[0], 'test');

// C# Memory<T> maps to/from JS TypedArray (without copying) for valid typed-array element types.
const uintArrayValue = ComplexTypes.uIntArray;
assert(uintArrayValue instanceof Uint32Array);
assert.strictEqual(uintArrayValue.length, 0);
assert.deepStrictEqual(ComplexTypes.uIntArray, uintArrayValue);
const uintArrayValue2 = new Uint32Array([0, 1, 2]);
ComplexTypes.uIntArray = uintArrayValue2;
assert.strictEqual(ComplexTypes.uIntArray.length, 3);
assert.strictEqual(ComplexTypes.uIntArray[1], 1);
assert.deepStrictEqual(ComplexTypes.uIntArray, uintArrayValue2);
const slicedArray = ComplexTypes.slice(new Uint32Array([0, 1, 2, 3]), 1, 2);
assert.deepStrictEqual(slicedArray, new Uint32Array([1, 2]))

// C# IEnumerable<T> maps to/from JS Iterable<T> (without copying)
const enumerableValue = ComplexTypes.enumerable;
assert.strictEqual(typeof enumerableValue, 'object');
const enumerableResult = [];
for (let value of enumerableValue) enumerableResult.push(value);
assert.deepStrictEqual(enumerableResult, [0, 1, 2]);

// C# Collection<T> maps to JS Iterable<T> and from JS Array<T> (without copying).
const collectionValue = ComplexTypes.collection;
assert.strictEqual(typeof collectionValue, 'object');
assert.strictEqual(collectionValue.length, 3);
const collectionResult = [];
for (let value of collectionValue) collectionResult.push(value);
assert.deepStrictEqual(collectionResult, [0, 1, 2]);
collectionValue.add(3);
assert.strictEqual(collectionValue.length, 4);
collectionValue.delete(0);
const collectionResult2 = [];
for (let value of collectionValue) collectionResult2.push(value);
assert.deepStrictEqual(collectionResult2, [1, 2, 3]);
const collectionArray = [];
ComplexTypes.collection = collectionArray;
assert.strictEqual(ComplexTypes.collection, collectionArray);

// C# IList<T> maps to/from JS Array<T> (without copying).
const listValue = ComplexTypes.list;
assert(Array.isArray(listValue));
assert.strictEqual(listValue.length, 0);
assert.strictEqual(ComplexTypes.list, listValue);
ComplexTypes.list = [0, 1, 2];
assert.notStrictEqual(ComplexTypes.list, listValue);
assert.strictEqual(ComplexTypes.list[0], 0);
const listValue2 = ComplexTypes.list;
ComplexTypes.list[0] = 1;
assert.strictEqual(listValue2[0], 1);

// C# ISet<T> maps to/from JS Set<T> (without copying).
const setValue = ComplexTypes.set;
assert(setValue instanceof Set);
assert.strictEqual(setValue.size, 0);
assert.strictEqual(ComplexTypes.set, setValue);
ComplexTypes.set = new Set([0, 1, 2]);
assert.notStrictEqual(ComplexTypes.set, setValue);
assert.strictEqual(ComplexTypes.set.size, 3);
assert(ComplexTypes.set.has(1));
const setValue2 = ComplexTypes.set;
ComplexTypes.set.add(3);
assert(setValue2.has(3));
const setEnumerableResult = [];
for (let value of setValue2) setEnumerableResult.push(value);
assert.deepStrictEqual(setEnumerableResult, [0, 1, 2, 3]);

// C# IDictionary<TKey, TValue> maps to/from JS Map<TKey, TValue> (without copying).
const mapValue = ComplexTypes.dictionary;
assert(mapValue instanceof Map);
assert.strictEqual(mapValue.size, 0);
assert.strictEqual(ComplexTypes.dictionary, mapValue);
ComplexTypes.dictionary = new Map([[0, 'zero'], [1, 'one'], [2, 'two']]);
assert.notStrictEqual(ComplexTypes.dictionary, mapValue);
assert.strictEqual(ComplexTypes.dictionary.size, 3);
assert(ComplexTypes.dictionary.has(1));
const mapValue2 = ComplexTypes.dictionary;
ComplexTypes.dictionary.set(3, 'three');
ComplexTypes.dictionary.delete(0);
assert.strictEqual(mapValue2.get(3), 'three');
const mapEnumerableResult = [];
for (let value of mapValue2) mapEnumerableResult.push(value);
assert.deepStrictEqual(mapEnumerableResult, [[1, 'one'], [2, 'two'], [3, 'three']]);

// C# enums are projected as objects with two-way value mappings.
const enumType = binding.TestEnum;
assert.strictEqual(typeof enumType, 'object');
assert.strictEqual(enumType.Zero, 0);
assert.strictEqual(enumType.One, 1);
assert.strictEqual(enumType[enumType.One], 'One');
assert.strictEqual(ComplexTypes.testEnum, enumType.Zero);
ComplexTypes.testEnum = enumType.Two;
assert.strictEqual(ComplexTypes.testEnum, enumType.Two);

// Date
const dateValue = ComplexTypes.date;
assert(dateValue instanceof Date);
assert.deepStrictEqual(dateValue, new Date("2023-02-01"));
ComplexTypes.date = new Date("2024-03-02T11:00");
assert.deepStrictEqual(ComplexTypes.date, new Date("2024-03-02T11:00"));

// Tuples
const pairValue = ComplexTypes.pair;
assert(Array.isArray(pairValue));
assert.deepStrictEqual(pairValue, ['pair', 1]);
ComplexTypes.pair = ['pair', -1];
assert.deepStrictEqual(ComplexTypes.pair, ['pair', -1]);
const tupleValue = ComplexTypes.tuple;
assert(Array.isArray(tupleValue));
assert.deepStrictEqual(tupleValue, ['tuple', 2]);
ComplexTypes.tuple = ['tuple', -2];
assert.deepStrictEqual(ComplexTypes.tuple, ['tuple', -2]);
const valueTupleValue = ComplexTypes.valueTuple;
assert(Array.isArray(valueTupleValue));
assert.deepStrictEqual(valueTupleValue, ['valueTuple', 3]);
ComplexTypes.valueTuple = ['valueTuple', -3];
assert.deepStrictEqual(ComplexTypes.valueTuple, ['valueTuple', -3]);

// Ref / out parameters
const results = classInstance.appendAndGetPreviousValue('!');
assert.strictEqual('object', typeof results);
assert.strictEqual('test2!', results.value);
assert.strictEqual('test2', results.previousValue);

if (ClassObject.callGenericMethod) {
  console.log('Calling generic method on interface implemented by JS.')
  let appendedValue = undefined;
  ClassObject.callGenericMethod({
    // TODO: This method should be camel-case... probably?
    AppendGenericValue(value) { appendedValue = value; }
  }, 10);
  assert.strictEqual(appendedValue, 10);
} else {
  console.log('Skipping generic method on interface implemented by JS.')
}
