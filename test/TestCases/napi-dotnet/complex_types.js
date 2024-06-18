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
ComplexTypes.stringArray[0] = 'test2'; // Does not modify the original
assert.strictEqual(ComplexTypes.stringArray[0], 'test');

const byteArrayValue = ComplexTypes.byteArray;
assert(Array.isArray(byteArrayValue));
assert.strictEqual(byteArrayValue.length, 3);
assert.notStrictEqual(ComplexTypes.byteArray, byteArrayValue);
assert.deepStrictEqual(ComplexTypes.byteArray, byteArrayValue);
ComplexTypes.byteArray = [ 1 ];
assert.strictEqual(ComplexTypes.byteArray[0], 1);
ComplexTypes.byteArray[0] = 2; // Does not modify the original
assert.strictEqual(ComplexTypes.byteArray[0], 1);

const intArrayValue = ComplexTypes.int32Array;
assert(Array.isArray(intArrayValue));
assert.strictEqual(intArrayValue.length, 3);
assert.notStrictEqual(ComplexTypes.int32Array, intArrayValue);
assert.deepStrictEqual(ComplexTypes.int32Array, intArrayValue);
ComplexTypes.int32Array = [ 1 ];
assert.strictEqual(ComplexTypes.int32Array[0], 1);
ComplexTypes.int32Array[0] = 2; // Does not modify the original
assert.strictEqual(ComplexTypes.int32Array[0], 1);


// C# Memory<T> maps to/from JS TypedArray (without copying) for valid typed-array element types.
const uint8ArrayValue = ComplexTypes.byteMemory;
assert(uint8ArrayValue instanceof Uint8Array);
assert.strictEqual(uint8ArrayValue.length, 3);
assert.deepStrictEqual(ComplexTypes.byteMemory, uint8ArrayValue);
const uint8ArrayValue2 = new Uint8Array([0, 1, 2, 3]);
ComplexTypes.byteMemory = uint8ArrayValue2;
assert.strictEqual(ComplexTypes.byteMemory.length, 4);
assert.strictEqual(ComplexTypes.byteMemory[3], 3);
assert.deepStrictEqual(ComplexTypes.byteMemory, uint8ArrayValue2);

const int32ArrayValue = ComplexTypes.int32Memory;
assert(int32ArrayValue instanceof Int32Array);
assert.strictEqual(int32ArrayValue.length, 3);
assert.deepStrictEqual(ComplexTypes.int32Memory, int32ArrayValue);
const int32ArrayValue2 = new Int32Array([0, 1, 2, 3]);
ComplexTypes.int32Memory = int32ArrayValue2;
assert.strictEqual(ComplexTypes.int32Memory.length, 4);
assert.strictEqual(ComplexTypes.int32Memory[3], 3);
assert.deepStrictEqual(ComplexTypes.int32Memory, int32ArrayValue2);
const slicedInt32Array = ComplexTypes.slice(new Int32Array([0, 1, 2, 3]), 1, 2);
assert.deepStrictEqual(slicedInt32Array, new Int32Array([1, 2]))

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

// DateTime
/** @type {Date | { kind: 'utc' | 'local' | 'unspecified' }} */
const dateValue = ComplexTypes.dateTime;
assert(dateValue instanceof Date);
assert.strictEqual(dateValue.valueOf(), new Date('2023-04-05T06:07:08').valueOf());
assert.strictEqual(dateValue.kind, 'unspecified');
ComplexTypes.dateTime = new Date('2024-03-02T11:00');
assert.strictEqual(ComplexTypes.dateTime.valueOf(), new Date('2024-03-02T11:00').valueOf());
assert.strictEqual(ComplexTypes.dateTime.kind, 'utc');
/** @type {Date | { kind: 'utc' | 'local' | 'unspecified' }} */
const dateValue2 = new Date('2024-03-02T11:00');
dateValue2.kind = 'local';
ComplexTypes.dateTime = dateValue2;
assert.strictEqual(ComplexTypes.dateTime.valueOf(), new Date('2024-03-02T11:00').valueOf());
assert.strictEqual(ComplexTypes.dateTime.kind, 'local');
assert.strictEqual(ComplexTypes.dateTimeLocal.valueOf(), new Date('2023-04-05T06:07:08').valueOf());
assert.strictEqual(ComplexTypes.dateTimeLocal.kind, 'local');
assert.strictEqual(
  ComplexTypes.dateTimeUtc.valueOf(),
  new Date(Date.UTC(2023, 3, 5, 6, 7, 8)).valueOf());
assert.strictEqual(ComplexTypes.dateTimeUtc.kind, 'utc');

// TimeSpan
const timeValue = ComplexTypes.timeSpan;
assert.strictEqual(typeof timeValue, 'number');
assert.strictEqual(timeValue, (36*60*60 + 30*60 + 45) * 1000);
ComplexTypes.timeSpan = (2*24*60*60 + 23*60*60 + 34*60 + 45) * 1000;
assert.strictEqual(ComplexTypes.timeSpan, (2*24*60*60 + 23*60*60 + 34*60 + 45) * 1000);

// DateTimeOffset
/** @type {Date | { offset: number }} */
const dateTimeOffsetValue = ComplexTypes.dateTimeOffset;
assert(dateTimeOffsetValue instanceof Date);
// A negative offset means the UTC time is later than the local time,
// so the offset is added to the local time to get the expected UTC time here.
assert.strictEqual(dateTimeOffsetValue.valueOf(), Date.UTC(2023, 3, 5, 6, 7, 8) + 90 * 60 * 1000);
assert.strictEqual(dateTimeOffsetValue.offset, -90);
assert.strictEqual(dateTimeOffsetValue.toString(), '2023-04-05 06:07:08.000 -01:30');
/** @type {Date | { offset: number }} */
const dateTimeOffsetValue2 = new Date(Date.UTC(2024, 2, 2, 1, 0, 0));
dateTimeOffsetValue2.offset = 120;
ComplexTypes.dateTimeOffset = dateTimeOffsetValue2;
assert.strictEqual(ComplexTypes.dateTimeOffset.valueOf(), dateTimeOffsetValue2.valueOf());
assert.strictEqual(ComplexTypes.dateTimeOffset.toString(), '2024-03-02 03:00:00.000 +02:00');

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

// Guid
assert.strictEqual(ComplexTypes.guid, '01234567-89ab-cdef-fedc-ba9876543210');
ComplexTypes.guid = 'fedcba98-7654-3210-0123-456789abcdef';
assert.strictEqual(ComplexTypes.guid, 'fedcba98-7654-3210-0123-456789abcdef');

// BigInteger
assert.strictEqual(typeof ComplexTypes.bigInt, 'bigint');
assert.strictEqual(ComplexTypes.bigInt, 1234567890123456789012345n);
ComplexTypes.bigInt = 987654321098765432109876n;
assert.strictEqual(ComplexTypes.bigInt, 987654321098765432109876n);
ComplexTypes.bigInt = -1234567890123456789012345n;
assert.strictEqual(ComplexTypes.bigInt, -1234567890123456789012345n);
ComplexTypes.bigInt = 0n;
assert.strictEqual(ComplexTypes.bigInt, 0n);

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

// Nested type and type toString
assert.equal(ClassObject.toString(), 'Microsoft.JavaScript.NodeApi.TestCases.ClassObject');
assert.strictEqual(typeof ClassObject.NestedClass, 'function');
assert.equal(ClassObject.NestedClass.toString(),
  'Microsoft.JavaScript.NodeApi.TestCases.ClassObject.NestedClass');
const nestedInstance = new ClassObject.NestedClass('nested');
assert.strictEqual(nestedInstance.value, 'nested');

// Private constructor
const ClassWithPrivateConstructor = binding.ClassWithPrivateConstructor;
let constructorError = undefined;
try {
  new ClassWithPrivateConstructor();
} catch (e) {
  constructorError = e;
}
assert(constructorError);
assert.strictEqual(
  constructorError.message,
  'Class \'ClassWithPrivateConstructor\' does not have a public constructor.');
// It should still be possible to get an instance that was constructed some other way.
const instanceWithPrivateConstructor = ClassWithPrivateConstructor.createInstance('test');
assert.strictEqual(instanceWithPrivateConstructor.value, 'test');

// Check that class and struct instances can be round-tripped and are not mixed up by equality.
const classInstanceA = new ClassObject('test');
const classInstanceB = new ClassObject('test');
assert(classInstanceA !== classInstanceB); // Reference inequality.
assert(classInstanceA.equals(classInstanceB)); // Value equality (C#).
assert.deepStrictEqual(classInstanceA, classInstanceB); // Value equality (JS).
// Classes are marshalled by reference so they maintain both value and reference equality.
ComplexTypes.classObject = classInstanceA;
assert(ComplexTypes.classObject.equals(classInstanceA));
assert(ComplexTypes.classObject.equals(classInstanceB));
assert.deepStrictEqual(ComplexTypes.classObject, classInstanceA);
assert.deepStrictEqual(ComplexTypes.classObject, classInstanceB);
assert(ComplexTypes.classObject === classInstanceA);
assert(ComplexTypes.classObject !== classInstanceB);
ComplexTypes.classObject = classInstanceB;
assert(ComplexTypes.classObject.equals(classInstanceA));
assert(ComplexTypes.classObject.equals(classInstanceB));
assert(ComplexTypes.classObject !== classInstanceA);
assert(ComplexTypes.classObject === classInstanceB);

const structInstanceA = new StructObject();
structInstanceA.value = 'test';
const structInstanceB = new StructObject();
structInstanceB.value = 'test';
assert(structInstanceA !== structInstanceB); // Reference inequality.
assert.deepStrictEqual(structInstanceA, structInstanceB); // Value equality.
ComplexTypes.structObject = structInstanceA;
// Structs are marshalled by value so they maintain value equality but not reference equality.
assert.deepStrictEqual(ComplexTypes.structObject, structInstanceA);
assert.deepStrictEqual(ComplexTypes.structObject, structInstanceB);
