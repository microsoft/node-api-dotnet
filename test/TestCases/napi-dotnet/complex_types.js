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
let classInstance = new ClassObject();
assert.strictEqual(classInstance.value, undefined);
classInstance.value = 'test';
assert.strictEqual(classInstance.value, 'test');
classInstance = new ClassObject('test1');
assert.strictEqual(classInstance.value, 'test1');

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
let structInstance = ComplexTypes.structObject;
assert.strictEqual(structInstance.value, 'test');
structInstance = new StructObject();
assert.strictEqual(structInstance.value, undefined);
structInstance.value = 'test';
assert.strictEqual(structInstance.value, 'test');
structInstance = new StructObject('test1');
assert.strictEqual(structInstance.value, 'test1');

// Struct instances are passed by value, so a property change on
// one reference should NOT be reflected on the other.
const structInstance2 = structInstance.thisObject();
assert.notStrictEqual(structInstance2, structInstance);
structInstance2.value = 'test2';
assert.strictEqual(structInstance2.value, 'test2');
assert.strictEqual(structInstance.value, 'test1');

const readonlyStructInstance = ComplexTypes.readonlyStructObject;
assert.strictEqual(readonlyStructInstance.value, 'test');
ComplexTypes.readonlyStructObject = { value: 'test2' };
assert.strictEqual(ComplexTypes.readonlyStructObject.value, 'test2');

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
