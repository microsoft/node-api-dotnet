// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

const dotnet = require('../common').dotnet;

const System = dotnet.System;
const ns = 'Microsoft.JavaScript.NodeApi.TestCases';

// Load the test module using dynamic binding `load()` instead of static binding `require()`.
const assemblyPath = process.env.NODE_API_TEST_MODULE_PATH;
dotnet.load(assemblyPath);
const TestCases = dotnet.Microsoft.JavaScript.NodeApi.TestCases;

const GenericClass$ = TestCases.GenericClass$;
assert.strictEqual(typeof GenericClass$, 'function');
assert.strictEqual( GenericClass$.toString(), `${ns}.GenericClass<T>`);

const GenericClassOfInt = GenericClass$(System.Int32);
assert.strictEqual(typeof GenericClassOfInt, 'function');
assert.strictEqual(GenericClassOfInt.toString(), `${ns}.GenericClass<System.Int32>`);
const genericInstanceOfInt = new GenericClassOfInt(1);
assert.strictEqual(genericInstanceOfInt.Value, 1);
assert.strictEqual(genericInstanceOfInt.GetValue(-1), -1);

const GenericClassOfString = GenericClass$(System.String);
assert.strictEqual(typeof GenericClassOfString, 'function');
assert.strictEqual(GenericClassOfString.toString(), `${ns}.GenericClass<System.String>`);
const genericInstanceOfString = new GenericClassOfString('two');
assert.strictEqual(genericInstanceOfString.Value, 'two');
assert.strictEqual(genericInstanceOfString.GetValue('TWO'), 'TWO');

const GenericClassOfClassObject = GenericClass$(TestCases.ClassObject);
assert.strictEqual(typeof GenericClassOfClassObject, 'function');
assert.strictEqual(GenericClassOfClassObject.toString(), `${ns}.GenericClass<${ns}.ClassObject>`);
const obj = new TestCases.ClassObject();
const genericInstanceOfClassObject = new GenericClassOfClassObject(obj);
assert.strictEqual(genericInstanceOfClassObject.Value, obj);
genericInstanceOfClassObject.Value = undefined;
assert.strictEqual(genericInstanceOfClassObject.Value, undefined);
assert.strictEqual(genericInstanceOfClassObject.GetValue(obj), obj);

const GenericClassWithConstraint$ = TestCases.GenericClassWithConstraint$;
assert.strictEqual(typeof GenericClassWithConstraint$, 'function');

let error = undefined;
try { GenericClassWithConstraint$(); } catch (e) { error = e; }
assert(error.message.startsWith(
  `Failed to make generic type ${ns}.GenericClassWithConstraint<T> with supplied type arguments: []`));
try { GenericClassWithConstraint$(System.Int32, System.Int32); } catch (e) { error = e; }
assert(error.message.startsWith(
  `Failed to make generic type ${ns}.GenericClassWithConstraint<T> with supplied type arguments: [System.Int32, System.Int32]`));
try { GenericClassWithConstraint$(System.String); } catch (e) { error = e; }
assert(error.message.startsWith(
  `Failed to make generic type ${ns}.GenericClassWithConstraint<T> with supplied type arguments: [System.String]`));

const GenericClassOfStruct = new GenericClassWithConstraint$(TestCases.StructObject);
assert.strictEqual(typeof GenericClassOfStruct, 'function');
assert.strictEqual(GenericClassOfStruct.toString(), `${ns}.GenericClassWithConstraint<${ns}.StructObject>`);
const obj2 = new TestCases.StructObject();
obj2.Value = 'test';
const genericInstanceOfStruct = new GenericClassOfStruct(obj2);
assert.strictEqual(genericInstanceOfStruct.Value.Value, 'test');
assert.strictEqual(genericInstanceOfStruct.GetValue(obj2).Value, 'test');

const GenericStruct$ = TestCases.GenericStruct$;
assert.strictEqual(typeof GenericStruct$, 'function');
const GenericStructOfInt = GenericStruct$(System.Int32);
assert.strictEqual(typeof GenericStructOfInt, 'function');
assert.strictEqual(GenericStructOfInt.toString(), `${ns}.GenericStruct<System.Int32>`);
const genericStructInstanceOfInt = new GenericStructOfInt();
genericStructInstanceOfInt.Value = 3;
assert.strictEqual(genericStructInstanceOfInt.Value, 3);
assert.strictEqual(genericStructInstanceOfInt.GetValue(-3), -3);

const GenericStructOfInterface = GenericStruct$(TestCases.ITestInterface);
assert.strictEqual(
  GenericStructOfInterface.toString(), `${ns}.GenericStruct<${ns}.ITestInterface>`);

assert.strictEqual(
  typeof TestCases.StaticClassWithGenericMethods.GetValue$(System.Int32), 'function');
assert.strictEqual(TestCases.StaticClassWithGenericMethods.GetValue$(System.Int32)(11), 11);

const nonstaticInstance = new TestCases.NonstaticClassWithGenericMethods();
assert.strictEqual(typeof nonstaticInstance.GetValue$(System.String), 'function');
assert.strictEqual(nonstaticInstance.GetValue$(System.String)('twelve'), 'twelve');
