// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

const dotnet = require('../common').dotnet;

dotnet.load(process.env.NODE_API_TEST_MODULE_PATH);

const System = dotnet.System;
const TestCases = dotnet.Microsoft.JavaScript.NodeApi.TestCases;

const ClassObject = TestCases.ClassObject;
assert.strictEqual(typeof ClassObject, 'function');
const classInstance = new ClassObject();
assert.strictEqual(classInstance.Value, undefined);
classInstance.Value = 'test';
assert.strictEqual(classInstance.Value, 'test');
assert.strictEqual(typeof classInstance.AppendValue, 'function');

// The instance methods should include extension methods for the class.
assert.strictEqual(typeof classInstance.GetValueOrDefault, 'function');
assert.strictEqual(classInstance.GetValueOrDefault('default'), 'test');
classInstance.Value = null;
assert.strictEqual(classInstance.GetValueOrDefault('default'), 'default');

// The instance methods should include extension methods for interfaces implemented by the class.
assert.strictEqual(typeof classInstance.ToInteger, 'function');
assert.strictEqual(classInstance.ToInteger(), undefined);
classInstance.Value = '10';
assert.strictEqual(classInstance.ToInteger(), 10);

const interfaceInstance = ClassObject.Create('20');
assert.strictEqual(typeof interfaceInstance.ToInteger, 'function');
assert.strictEqual(interfaceInstance.ToInteger(), 20);

// Generic extension methods!
const GenericClass$ = TestCases.GenericClass$;
const GenericClassOfString = GenericClass$(System.String);
assert.strictEqual(typeof GenericClassOfString, 'function');
const genericInstance = new GenericClassOfString('test');
assert.strictEqual(typeof genericInstance, 'object');

assert.strictEqual(typeof genericInstance.GetGenericValueOrDefault, 'function');
assert.strictEqual(typeof genericInstance.GetGenericStringValueOrDefault, 'function');
assert.strictEqual(typeof genericInstance.GenericToInteger, 'function');
assert.strictEqual(typeof genericInstance.GenericStringToInteger, 'function');

const SubclassOfGenericClass = TestCases.SubclassOfGenericClass;
assert.strictEqual(typeof SubclassOfGenericClass, 'function');
const subclassInstance = new SubclassOfGenericClass('test');
assert.strictEqual(typeof subclassInstance, 'object');

assert.strictEqual(typeof subclassInstance.GetGenericValueOrDefault, 'function');
assert.strictEqual(typeof subclassInstance.GetGenericStringValueOrDefault, 'function');
assert.strictEqual(typeof subclassInstance.GenericToInteger, 'function');
assert.strictEqual(typeof subclassInstance.GenericStringToInteger, 'function');

const genericInterfaceInstance = SubclassOfGenericClass.Create('30');
assert.strictEqual(typeof genericInterfaceInstance, 'object');

assert.strictEqual(typeof genericInterfaceInstance.GenericToInteger, 'function');
assert.strictEqual(typeof genericInterfaceInstance.GenericStringToInteger, 'function');
assert.strictEqual(genericInterfaceInstance.GenericToInteger(), 30);
assert.strictEqual(genericInterfaceInstance.GenericStringToInteger(), 30);
