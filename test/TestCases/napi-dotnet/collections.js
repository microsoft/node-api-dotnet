// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const Collections = binding.Collections;
assert.strictEqual(typeof Collections, 'object');

// C# arrays are copied to/from JS, so modifying the returned array doesn't affect the original.
const arrayOfStringValue = Collections.Arrays.arrayOfString;
assert(Array.isArray(arrayOfStringValue));
assert.strictEqual(arrayOfStringValue.length, 0);
assert.notStrictEqual(Collections.Arrays.arrayOfString, arrayOfStringValue);
assert.deepStrictEqual(Collections.Arrays.arrayOfString, arrayOfStringValue);
Collections.Arrays.arrayOfString = [ 'test' ];
assert.strictEqual(Collections.Arrays.arrayOfString[0], 'test');
Collections.Arrays.arrayOfString[0] = 'test2'; // Does not modify the original
assert.strictEqual(Collections.Arrays.arrayOfString[0], 'test');

const arrayOfByteValue = Collections.Arrays.arrayOfByte;
assert(Array.isArray(arrayOfByteValue));
assert.strictEqual(arrayOfByteValue.length, 3);
assert.notStrictEqual(Collections.Arrays.arrayOfByte, arrayOfByteValue);
assert.deepStrictEqual(Collections.Arrays.arrayOfByte, arrayOfByteValue);
Collections.Arrays.arrayOfByte = [ 1 ];
assert.strictEqual(Collections.Arrays.arrayOfByte[0], 1);
Collections.Arrays.arrayOfByte[0] = 2; // Does not modify the original
assert.strictEqual(Collections.Arrays.arrayOfByte[0], 1);

const arrayOfIntValue = Collections.Arrays.arrayOfInt;
assert(Array.isArray(arrayOfIntValue));
assert.strictEqual(arrayOfIntValue.length, 3);
assert.notStrictEqual(Collections.Arrays.arrayOfInt, arrayOfIntValue);
assert.deepStrictEqual(Collections.Arrays.arrayOfInt, arrayOfIntValue);
Collections.Arrays.arrayOfInt = [ 1 ];
assert.strictEqual(Collections.Arrays.arrayOfInt[0], 1);
Collections.Arrays.arrayOfInt[0] = 2; // Does not modify the original
assert.strictEqual(Collections.Arrays.arrayOfInt[0], 1);

const arrayOfClassObjectValue = Collections.Arrays.arrayOfClassObject;
assert(Array.isArray(arrayOfClassObjectValue));
assert.strictEqual(arrayOfClassObjectValue.length, 2);
assert.notStrictEqual(Collections.Arrays.arrayOfClassObject, arrayOfClassObjectValue);
assert.deepStrictEqual(Collections.Arrays.arrayOfClassObject, arrayOfClassObjectValue);
const ClassObject = binding.ClassObject;
assert.strictEqual(typeof ClassObject, 'function');
const classInstance = new ClassObject();
classInstance.value = 'X';
Collections.Arrays.arrayOfClassObject = [ classInstance ];
assert.strictEqual(Collections.Arrays.arrayOfClassObject.length, 1);
assert.strictEqual(Collections.Arrays.arrayOfClassObject[0], classInstance);

// C# Memory<T> maps to/from JS TypedArray (without copying) for valid typed-array element types.
const memoryOfByteValue = Collections.Memory.memoryOfByte;
assert(memoryOfByteValue instanceof Uint8Array);
assert.strictEqual(memoryOfByteValue.length, 3);
assert.deepStrictEqual(Collections.Memory.memoryOfByte, memoryOfByteValue);
const memoryOfByteValue2 = new Uint8Array([0, 1, 2, 3]);
Collections.Memory.memoryOfByte = memoryOfByteValue2;
assert.strictEqual(Collections.Memory.memoryOfByte.length, 4);
assert.strictEqual(Collections.Memory.memoryOfByte[3], 3);
assert.deepStrictEqual(Collections.Memory.memoryOfByte, memoryOfByteValue2);

const memoryOfIntValue = Collections.Memory.memoryOfInt;
assert(memoryOfIntValue instanceof Int32Array);
assert.strictEqual(memoryOfIntValue.length, 3);
assert.deepStrictEqual(Collections.Memory.memoryOfInt, memoryOfIntValue);
const memoryOfIntValue2 = new Int32Array([0, 1, 2, 3]);
Collections.Memory.memoryOfInt = memoryOfIntValue2;
assert.strictEqual(Collections.Memory.memoryOfInt.length, 4);
assert.strictEqual(Collections.Memory.memoryOfInt[3], 3);
assert.deepStrictEqual(Collections.Memory.memoryOfInt, memoryOfIntValue2);
const slicedArrayOfInt = Collections.Memory.slice(new Int32Array([0, 1, 2, 3]), 1, 2);
assert.deepStrictEqual(slicedArrayOfInt, new Int32Array([1, 2]))

// C# IEnumerable<T> maps to/from JS Iterable<T> (without copying)
const iEnumerableOfIntValue = Collections.GenericInterfaces.iEnumerableOfInt;
assert.strictEqual(typeof iEnumerableOfIntValue, 'object');
const enumerableResult = [];
for (let value of iEnumerableOfIntValue) enumerableResult.push(value);
assert.deepStrictEqual(enumerableResult, [0, 1, 2]);

// C# ICollection<T> maps to JS Iterable<T> and from JS Array<T> (without copying).
const iCollectionOfIntValue = Collections.GenericInterfaces.iCollectionOfInt;
assert.strictEqual(typeof iCollectionOfIntValue, 'object');
assert.strictEqual(iCollectionOfIntValue.length, 3);
const collectionResult = [];
for (let value of iCollectionOfIntValue) collectionResult.push(value);
assert.deepStrictEqual(collectionResult, [0, 1, 2]);
iCollectionOfIntValue.add(3);
assert.strictEqual(iCollectionOfIntValue.length, 4);
iCollectionOfIntValue.delete(0);
const collectionResult2 = [];
for (let value of iCollectionOfIntValue) collectionResult2.push(value);
assert.deepStrictEqual(collectionResult2, [1, 2, 3]);
const emptyArray = [];
Collections.GenericInterfaces.iCollectionOfInt = emptyArray;
assert.strictEqual(Collections.GenericInterfaces.iCollectionOfInt, emptyArray);

// C# IList<T> maps to/from JS Array<T> (without copying).
const iListOfIntValue = Collections.GenericInterfaces.iListOfInt;
assert(Array.isArray(iListOfIntValue));
assert.strictEqual(iListOfIntValue.length, 3);
assert.strictEqual(Collections.GenericInterfaces.iListOfInt, iListOfIntValue);
assert.deepStrictEqual(Collections.GenericInterfaces.iListOfInt, [0, 1, 2]);
assert.strictEqual(Collections.GenericInterfaces.iListOfInt.join(), '0,1,2');
Collections.GenericInterfaces.iListOfInt.splice(0, 3);
Collections.GenericInterfaces.iListOfInt.push(5, 6, 7);
assert.strictEqual(Collections.GenericInterfaces.iListOfInt.join(), '5,6,7');
Collections.GenericInterfaces.iListOfInt = [0, 1, 2];
assert.notStrictEqual(Collections.GenericInterfaces.iListOfInt, iListOfIntValue);
assert.strictEqual(Collections.GenericInterfaces.iListOfInt[0], 0);
assert.deepStrictEqual(Collections.GenericInterfaces.iListOfInt, [0, 1, 2]);
assert.strictEqual(Collections.GenericInterfaces.iListOfInt.join(), '0,1,2');
const iListOfIntValue2 = Collections.GenericInterfaces.iListOfInt;
Collections.GenericInterfaces.iListOfInt[0] = 1;
assert.strictEqual(iListOfIntValue2[0], 1);

// C# ISet<T> maps to/from JS Set<T> (without copying).
const iSetOfIntValue = Collections.GenericInterfaces.iSetOfInt;
assert(iSetOfIntValue instanceof Set);
assert.strictEqual(iSetOfIntValue.size, 3);
assert.strictEqual(Collections.GenericInterfaces.iSetOfInt, iSetOfIntValue);
Collections.GenericInterfaces.iSetOfInt = new Set([0, 1, 2]);
assert.notStrictEqual(Collections.iSetOfInt, iSetOfIntValue);
assert.strictEqual(Collections.GenericInterfaces.iSetOfInt.size, 3);
assert(Collections.GenericInterfaces.iSetOfInt.has(1));
const iSetOfIntValue2 = Collections.GenericInterfaces.iSetOfInt;
Collections.GenericInterfaces.iSetOfInt.add(3);
assert(iSetOfIntValue2.has(3));
const setEnumerableResult = [];
for (let value of iSetOfIntValue2) setEnumerableResult.push(value);
assert.deepStrictEqual(setEnumerableResult, [0, 1, 2, 3]);

// C# IDictionary<TKey, TValue> maps to/from JS Map<TKey, TValue> (without copying).
const iDictionaryOfIntStringValue = Collections.GenericInterfaces.iDictionaryOfIntString;
assert(iDictionaryOfIntStringValue instanceof Map);
assert.strictEqual(iDictionaryOfIntStringValue.size, 3);
assert.strictEqual(Collections.GenericInterfaces.iDictionaryOfIntString, iDictionaryOfIntStringValue);
iDictionaryOfIntStringValue.set(1, 'one');
assert.strictEqual(iDictionaryOfIntStringValue.get(1), 'one');
iDictionaryOfIntStringValue.set(2, 'two');
assert.strictEqual([...iDictionaryOfIntStringValue.entries()].join(';'), '0,A;1,one;2,two');
Collections.GenericInterfaces.iDictionaryOfIntString = new Map([[0, 'zero'], [1, 'one'], [2, 'two']]);
assert.notStrictEqual(Collections.GenericInterfaces.iDictionaryOfIntString, iDictionaryOfIntStringValue);
assert.strictEqual(Collections.GenericInterfaces.iDictionaryOfIntString.size, 3);
assert(Collections.GenericInterfaces.iDictionaryOfIntString.has(1));
const iDictionaryOfIntStringValue2 = Collections.GenericInterfaces.iDictionaryOfIntString;
Collections.GenericInterfaces.iDictionaryOfIntString.set(3, 'three');
Collections.GenericInterfaces.iDictionaryOfIntString.delete(0);
assert.strictEqual(iDictionaryOfIntStringValue2.get(3), 'three');
const mapEnumerableResult = [];
for (let value of iDictionaryOfIntStringValue2) mapEnumerableResult.push(value);
assert.deepStrictEqual(mapEnumerableResult, [[1, 'one'], [2, 'two'], [3, 'three']]);

// Sealed collection classes are copied to/from JS, so modifying the returned collection doesn't affect the original.
const listOfIntValue = Collections.GenericClasses.listOfInt;
assert(Array.isArray(listOfIntValue));
assert.strictEqual(listOfIntValue.length, 3);
assert.notStrictEqual(Collections.GenericClasses.listOfInt, listOfIntValue);
assert.deepStrictEqual(Collections.GenericClasses.listOfInt, listOfIntValue);
Collections.GenericClasses.listOfInt = [ 1 ];
assert.strictEqual(Collections.GenericClasses.listOfInt[0], 1);
Collections.GenericClasses.listOfInt[0] = 2; // Does not modify the original
assert.strictEqual(Collections.GenericClasses.listOfInt[0], 1);

const stackOfIntValue = Collections.GenericClasses.stackOfInt;
assert(Array.isArray(stackOfIntValue));
assert.strictEqual(stackOfIntValue.length, 3);
assert.notStrictEqual(Collections.GenericClasses.stackOfInt, stackOfIntValue);
assert.deepStrictEqual(Collections.GenericClasses.stackOfInt, stackOfIntValue);
Collections.GenericClasses.stackOfInt = [ 1 ];
assert.strictEqual(Collections.GenericClasses.stackOfInt[0], 1);
Collections.GenericClasses.stackOfInt[0] = 2; // Does not modify the original
assert.strictEqual(Collections.GenericClasses.stackOfInt[0], 1);

const queueOfIntValue = Collections.GenericClasses.queueOfInt;
assert(Array.isArray(queueOfIntValue));
assert.strictEqual(queueOfIntValue.length, 3);
assert.notStrictEqual(Collections.GenericClasses.queueOfInt, queueOfIntValue);
assert.deepStrictEqual(Collections.GenericClasses.queueOfInt, queueOfIntValue);
Collections.GenericClasses.queueOfInt = [ 1 ];
assert.strictEqual(Collections.GenericClasses.queueOfInt[0], 1);
Collections.GenericClasses.queueOfInt[0] = 2; // Does not modify the original
assert.strictEqual(Collections.GenericClasses.queueOfInt[0], 1);

const hashSetOfIntValue = Collections.GenericClasses.hashSetOfInt;
assert(Array.isArray(hashSetOfIntValue));
assert.strictEqual(hashSetOfIntValue.length, 3);
assert.notStrictEqual(Collections.GenericClasses.hashSetOfInt, hashSetOfIntValue);
assert.deepStrictEqual(Collections.GenericClasses.hashSetOfInt, hashSetOfIntValue);
Collections.GenericClasses.hashSetOfInt = [ 1 ];
assert.strictEqual(Collections.GenericClasses.hashSetOfInt[0], 1);
Collections.GenericClasses.hashSetOfInt[0] = 2; // Does not modify the original
assert.strictEqual(Collections.GenericClasses.hashSetOfInt[0], 1);

const sortedSetOfIntValue = Collections.GenericClasses.sortedSetOfInt;
assert(Array.isArray(sortedSetOfIntValue));
assert.strictEqual(sortedSetOfIntValue.length, 3);
assert.notStrictEqual(Collections.GenericClasses.sortedSetOfInt, sortedSetOfIntValue);
assert.deepStrictEqual(Collections.GenericClasses.sortedSetOfInt, sortedSetOfIntValue);
Collections.GenericClasses.sortedSetOfInt = [ 1 ];
assert.strictEqual(Collections.GenericClasses.sortedSetOfInt[0], 1);
Collections.GenericClasses.sortedSetOfInt[0] = 2; // Does not modify the original
assert.strictEqual(Collections.GenericClasses.sortedSetOfInt[0], 1);

const dictionaryOfIntStringValue = Collections.GenericClasses.dictionaryOfIntString;
console.dir(Collections.GenericClasses);
assert(dictionaryOfIntStringValue instanceof Map);
assert.strictEqual(dictionaryOfIntStringValue.size, 3);
assert.notStrictEqual(Collections.GenericClasses.dictionaryOfIntString, dictionaryOfIntStringValue);
assert.deepStrictEqual(Collections.GenericClasses.dictionaryOfIntString, dictionaryOfIntStringValue);
Collections.GenericClasses.dictionaryOfIntString = new Map([[0, 'zero']]);
assert.strictEqual(Collections.GenericClasses.dictionaryOfIntString.get(0), 'zero');
Collections.GenericClasses.dictionaryOfIntString.set(0, ''); // Does not modify the original
assert.strictEqual(Collections.GenericClasses.dictionaryOfIntString.get(0), 'zero');

const sortedDictionaryOfIntStringValue = Collections.GenericClasses.sortedDictionaryOfIntString;
assert(sortedDictionaryOfIntStringValue instanceof Map);
assert.strictEqual(sortedDictionaryOfIntStringValue.size, 3);
assert.notStrictEqual(Collections.GenericClasses.sortedDictionaryOfIntString, sortedDictionaryOfIntStringValue);
assert.deepStrictEqual(Collections.GenericClasses.sortedDictionaryOfIntString, sortedDictionaryOfIntStringValue);
Collections.GenericClasses.sortedDictionaryOfIntString = new Map([[0, 'zero']]);
assert.strictEqual(Collections.GenericClasses.sortedDictionaryOfIntString.get(0), 'zero');
Collections.GenericClasses.sortedDictionaryOfIntString.set(0, ''); // Does not modify the original
assert.strictEqual(Collections.GenericClasses.sortedDictionaryOfIntString.get(0), 'zero');

// Non-sealed collection classes in System.Collections.ObjectModel are marshalled
// by reference like the collection interfaces.
const collectionOfIntValue = Collections.ObjectModelClasses.collectionOfInt;
assert(Array.isArray(collectionOfIntValue));
assert.strictEqual(collectionOfIntValue.length, 3);
assert.strictEqual(Collections.ObjectModelClasses.collectionOfInt, collectionOfIntValue);
assert.deepStrictEqual(Collections.ObjectModelClasses.collectionOfInt, [0, 1, 2]);
assert.strictEqual(Collections.ObjectModelClasses.collectionOfInt.join(), '0,1,2');
Collections.ObjectModelClasses.collectionOfInt.splice(0, 3);
Collections.ObjectModelClasses.collectionOfInt.push(5, 6, 7);
assert.strictEqual(Collections.ObjectModelClasses.collectionOfInt.join(), '5,6,7');
Collections.ObjectModelClasses.collectionOfInt = [0, 1, 2];
assert.notStrictEqual(Collections.ObjectModelClasses.collectionOfInt, collectionOfIntValue);
assert.strictEqual(Collections.ObjectModelClasses.collectionOfInt[0], 0);
assert.deepStrictEqual(Collections.ObjectModelClasses.collectionOfInt, [0, 1, 2]);
assert.strictEqual(Collections.ObjectModelClasses.collectionOfInt.join(), '0,1,2');
const collectionOfIntValue2 = Collections.ObjectModelClasses.collectionOfInt;
Collections.ObjectModelClasses.collectionOfInt[0] = 1;
assert.strictEqual(collectionOfIntValue2[0], 1);

const readOnlyCollectionOfIntValue = Collections.ObjectModelClasses.readOnlyCollectionOfInt;
assert(Array.isArray(readOnlyCollectionOfIntValue));
assert.strictEqual(readOnlyCollectionOfIntValue.length, 3);
assert.strictEqual(Collections.ObjectModelClasses.readOnlyCollectionOfInt, readOnlyCollectionOfIntValue);
assert.deepStrictEqual(Collections.ObjectModelClasses.readOnlyCollectionOfInt, [0, 1, 2]);
assert.strictEqual(Collections.ObjectModelClasses.readOnlyCollectionOfInt.join(), '0,1,2');
Collections.ObjectModelClasses.readOnlyCollectionOfInt = [0, 1, 2];
assert.notStrictEqual(Collections.ObjectModelClasses.readOnlyCollectionOfInt, readOnlyCollectionOfIntValue);
assert.strictEqual(Collections.ObjectModelClasses.readOnlyCollectionOfInt[0], 0);
assert.deepStrictEqual(Collections.ObjectModelClasses.readOnlyCollectionOfInt, [0, 1, 2]);
assert.strictEqual(Collections.ObjectModelClasses.readOnlyCollectionOfInt.join(), '0,1,2');

const readOnlyDictionaryOfIntStringValue = Collections.ObjectModelClasses.readOnlyDictionaryOfIntString;
assert(readOnlyDictionaryOfIntStringValue instanceof Map);
assert.strictEqual(readOnlyDictionaryOfIntStringValue.size, 3);
assert.strictEqual(Collections.ObjectModelClasses.readOnlyDictionaryOfIntString, readOnlyDictionaryOfIntStringValue);
assert.strictEqual([...readOnlyDictionaryOfIntStringValue.entries()].join(';'), '0,A;1,B;2,C');
Collections.ObjectModelClasses.readOnlyDictionaryOfIntString = new Map([[0, 'zero'], [1, 'one'], [2, 'two']]);
assert.strictEqual(Collections.ObjectModelClasses.readOnlyDictionaryOfIntString.size, 3);
assert(Collections.ObjectModelClasses.readOnlyDictionaryOfIntString.has(1));
