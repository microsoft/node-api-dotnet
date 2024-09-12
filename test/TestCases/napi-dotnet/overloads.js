// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const Overloads = binding.Overloads;
const ClassObject = binding.ClassObject;
const BaseClass = binding.BaseClass;
const SubClass = binding.SubClass;
const StructObject = binding.StructObject;
const TestEnum = binding.TestEnum;

// Overloaded constructor
const emptyObj = new Overloads();
assert.strictEqual(emptyObj.intValue, undefined);
assert.strictEqual(emptyObj.stringValue, undefined);

const intObj = new Overloads(1);
assert.strictEqual(intObj.intValue, 1);
assert.strictEqual(intObj.stringValue, undefined);

const stringObj = new Overloads('two');
assert.strictEqual(stringObj.intValue, undefined);
assert.strictEqual(stringObj.stringValue, 'two');

const comboObj = new Overloads(3, 'three');
assert.strictEqual(comboObj.intValue, 3);
assert.strictEqual(comboObj.stringValue, 'three');

const objValue = new ClassObject();
objValue.value = 'test';
const objFromClass = new Overloads(objValue);
assert.strictEqual(objFromClass.stringValue, 'test');

// Overloaded method with basic resolution by parameter count and JS type.
const obj1 = new Overloads();
obj1.setValue(1);
assert.strictEqual(obj1.intValue, 1);
assert.strictEqual(obj1.stringValue, undefined);

const obj2 = new Overloads();
obj2.setValue('two');
assert.strictEqual(obj2.intValue, undefined);
assert.strictEqual(obj2.stringValue, 'two');

const obj3 = new Overloads();
obj3.setValue(3, 'three');
assert.strictEqual(obj3.intValue, 3);
assert.strictEqual(obj3.stringValue, 'three');

const obj4 = new Overloads();
obj4.setValue(objValue);
assert.strictEqual(obj4.stringValue, 'test');

// Overloaded C# method with explicit JS method name.
const obj5 = new Overloads();
obj5.setDoubleValue(5.0);
assert.strictEqual(obj5.intValue, 5);

// Overloaded method with resolution by matching numeric type.
assert.strictEqual(Overloads.numericMethod(1), '1: int');
assert.strictEqual(Overloads.numericMethod(10000000000), '10000000000: long');
assert.strictEqual(Overloads.numericMethod(1.11), '1.11: double');
assert.strictEqual(Overloads.numericMethod2('test', 2), 'test: string, 2: int');
assert.strictEqual(Overloads.numericMethod2('test', 2.22), 'test: string, 2.22: double');

// Overloaded method with resolution by selecting best numeric type specificity.
assert.strictEqual(Overloads.numericMethod3(1), '1: byte');
assert.strictEqual(Overloads.numericMethod3(-1), '-1: sbyte');
assert.strictEqual(Overloads.numericMethod3(1000), '1000: ushort');
assert.strictEqual(Overloads.numericMethod3(-1000), '-1000: short');
assert.strictEqual(Overloads.numericMethod3(1000000), '1000000: uint');
assert.strictEqual(Overloads.numericMethod3(-1000000), '-1000000: int');
assert.strictEqual(Overloads.numericMethod3(10000000000), '10000000000: ulong');
assert.strictEqual(Overloads.numericMethod3(-10000000000), '-10000000000: long');

// Overloaded method with resolution by matching object type.
assert.strictEqual(Overloads.classMethod(new ClassObject('class')), 'class: ClassObject');
assert.strictEqual(Overloads.classMethod(new BaseClass(1)), '1: BaseClass');
assert.strictEqual(Overloads.classMethod(new SubClass(1, 2)), '2: SubClass');
assert.strictEqual(Overloads.classMethod({ value: 'struct' }), 'struct: StructObject');
assert.strictEqual(Overloads.classMethod(new StructObject('struct2')), 'struct2: StructObject');

// Overloaded method with resolution by matching interface type.
assert.strictEqual(Overloads.interfaceMethod(new BaseClass(1)), '1: IBaseInterface');
assert.strictEqual(Overloads.interfaceMethod(new SubClass(1, 2)), '2: ISubInterface');

// Overloaded method with resolution by matching collection type.
assert.strictEqual(Overloads.collectionMethod1([1, 2, 3]), '[1, 2, 3]: IList<int>');
assert.strictEqual(Overloads.collectionMethod1(new Set([1, 2, 3])), '[1, 2, 3]: ISet<int>');
assert.strictEqual(Overloads.collectionMethod1(
    new Map([[1, 10], [2, 20], [3, 30]])), '[[1, 10], [2, 20], [3, 30]]: IDictionary<int, int>');

// Overloaded method with resolution by matching read-only collection type.
assert.strictEqual(Overloads.collectionMethod2(
    [1, 2, 3]), '[1, 2, 3]: IReadOnlyList<int>');
assert.strictEqual(Overloads.collectionMethod2(
    new Set([1, 2, 3])), '[1, 2, 3]: IReadOnlyCollection<int>');
assert.strictEqual(Overloads.collectionMethod2(
    new Map([[1, 10], [2, 20], [3, 30]])),
    '[[1, 10], [2, 20], [3, 30]]: IReadOnlyDictionary<int, int>');

// Overloaded method with resolution by matching iterable or collection type.
const testIterable = {
    [Symbol.iterator]: function* () {
        yield 1;
        yield 2;
        yield 3;
    }
};
const testAsyncIterable = {
    [Symbol.asyncIterator]: async function* () {
        yield 1;
        yield 2;
        yield 3;
    }
};
assert.strictEqual(Overloads.collectionMethod3(
    testIterable), '[1, 2, 3]: IEnumerable<int>');
assert.strictEqual(Overloads.collectionMethod3(
    [1, 2, 3]), '[1, 2, 3]: ICollection<int>');
assert.strictEqual(Overloads.collectionMethod3(
    new Set([1, 2, 3])), '[1, 2, 3]: ICollection<int>');
Overloads.collectionMethod4([1, 2, 3]).then(
    (result) => assert.strictEqual(result, '[1, 2, 3]: IEnumerable<int>'));
Overloads.collectionMethod4(testIterable).then(
    (result) => assert.strictEqual(result, '[1, 2, 3]: IEnumerable<int>'));
Overloads.collectionMethod4(testAsyncIterable).then(
    (result) => assert.strictEqual(result, '[1, 2, 3]: IAsyncEnumerable<int>'));

// The following types have special marshalling behaviors:
// DateTime(Offset), TimeSpan, Enums, Guid, BigInteger, Task, Delegate

const dateWithKind = new Date(Date.UTC(2024, 1, 29));
dateWithKind.kind = 'utc';
assert.strictEqual(Overloads.dateTimeMethod(dateWithKind), '2024-02-29T00:00:00: DateTime');
const dateWithOffset = new Date(2024, 1, 29);
dateWithOffset.offset = -10 * 60;
assert.strictEqual(Overloads.dateTimeMethod(dateWithOffset), '2024-02-29T00:00:00: DateTimeOffset');
assert.strictEqual(Overloads.dateTimeMethod(11 * 60 * 1000), '00:11:00: TimeSpan');

assert.strictEqual(Overloads.otherMethod(TestEnum.One), 'One: TestEnum');
assert.strictEqual(Overloads.otherMethod('00000000-0000-0000-0000-000000000000'),
    '00000000-0000-0000-0000-000000000000: Guid');
assert.strictEqual(Overloads.otherMethod(1000000000000000000000000n),
    '1000000000000000000000000: BigInteger');
assert.strictEqual(Overloads.otherMethod(BigInt('1000000000000000000000000')),
    '1000000000000000000000000: BigInteger');
assert.strictEqual(Overloads.otherMethod(Promise.resolve()), 'Task');
assert.strictEqual(Overloads.otherMethod((value) => value.toUpperCase()), 'TEST: TestDelegate');

// Overloaded method with resolution of null / undefined parameters.
assert.strictEqual(Overloads.nullableNumericMethod(null), 'null: int?');
assert.strictEqual(Overloads.nullableNumericMethod(undefined), 'null: int?');
assert.strictEqual(Overloads.nullableNumericMethod(3), '3: int?');
assert.strictEqual(Overloads.nullableNumericMethod(4.4), '4.4: double');
