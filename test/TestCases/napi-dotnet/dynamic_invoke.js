// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

const dotnet = require('../common').dotnet;

console.dir(Object.keys(dotnet));

const Console = dotnet.System.Console;
Console.WriteLine('Hello from .NET!');

const Version = dotnet.System.Version;
const version = new Version(1, 2, 3); // Invoke overloaded constructor with args.
assert.strictEqual(version.ToString(), '1.2.3');
assert.strictEqual(version + '.4', '1.2.3.4'); // Implicit call to .NET ToString()

const parsedVersion = Version.TryParse('1.2.3'); // Try* pattern returns result or undefined.
assert.deepStrictEqual(version, parsedVersion);
assert.strictEqual(undefined, Version.TryParse('invalid'));

// Load the test module using dynamic binding `load()` instead of static binding `require()`.
const assemblyPath = process.env.NODE_API_TEST_MODULE_PATH;
dotnet.load(assemblyPath);
const TestCases = dotnet.Microsoft.JavaScript.NodeApi.TestCases;
assert.strictEqual(TestCases.toString(), 'Microsoft.JavaScript.NodeApi.TestCases');
console.dir(Object.keys(TestCases)); // Print all child types and namespace in the namespace.

const Hello = TestCases.Hello;
console.dir(Object.keys(Hello)); // Print all public static members of the Hello class.

const greeting = Hello.Test('assembly'); // Call a static method.
assert.strictEqual(greeting, 'Hello assembly!');

const ClassObject = TestCases.ClassObject;
assert.strictEqual(typeof ClassObject, 'function');
const instance = new ClassObject(); // Construct an instance of a class
assert.strictEqual(instance.Value, undefined);
instance.Value = 'test';
assert.strictEqual(instance.Value, 'test');

const results = instance.AppendAndGetPreviousValue('2');
console.dir(Object.getOwnPropertyNames(results));
assert.strictEqual('object', typeof results);
assert.strictEqual('test2', results.value);
assert.strictEqual('test', results.previousValue);

const StructObject = TestCases.StructObject;
assert.strictEqual(typeof StructObject, 'function');
const instance2 = new StructObject(); // Construct an instance of a struct
assert.strictEqual(instance2.Value, undefined);
instance2.Value = 'test';
assert.strictEqual(instance2.Value, 'test');

const TestEnum = TestCases.TestEnum;
assert.strictEqual(TestEnum.Two, 2);
assert.strictEqual(TestEnum[2], 'Two');

const Delegates = TestCases.Delegates;
assert.strictEqual(typeof Delegates, 'object');
const funcValue = Delegates.CallFunc((value) => value + 1, 1);
assert.strictEqual(funcValue, 2);
const delegateValue = Delegates.CallDelegate((value) => value + '!', 'test');
assert.strictEqual(delegateValue, 'test!');
const delegateValue2 = Delegates.CallDotnetDelegate((dotnetAction) => dotnetAction('test'));
assert.strictEqual(delegateValue2, '#test');

// Nested type and type toString
assert.equal(TestCases.ClassObject.toString(), 'Microsoft.JavaScript.NodeApi.TestCases.ClassObject');
assert.strictEqual(typeof TestCases.ClassObject.NestedClass, 'function');
assert.equal(TestCases.ClassObject.NestedClass.toString(),
  'Microsoft.JavaScript.NodeApi.TestCases.ClassObject.NestedClass');
const nestedInstance = new TestCases.ClassObject.NestedClass('nested');
assert.strictEqual(nestedInstance.Value, 'nested');

async function test() {
  const interfaceObj = TestCases.AsyncMethods.InterfaceTest;
  assert.strictEqual(typeof interfaceObj, 'object');

  const interfaceResult = await interfaceObj.TestAsync('buddy');
  assert.strictEqual(interfaceResult, 'Hey buddy!');

  // Invoke a C# method that calls back to a JS object that implements an interface.
  const asyncInterfaceImpl = {
    async TestAsync(greeting) { return `Hello, ${greeting}!`; }
  };
  const reverseInterfaceResult =
    await TestCases.AsyncMethods.ReverseInterfaceTest(asyncInterfaceImpl, 'buddy');
  assert.strictEqual(reverseInterfaceResult, 'Hello, buddy!');
}
test().catch(assert.fail);
