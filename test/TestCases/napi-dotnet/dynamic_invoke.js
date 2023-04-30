// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

const dotnetHost = process.env.TEST_DOTNET_HOST_PATH;
const dotnetVersion = process.env.TEST_DOTNET_VERSION;
const dotnet = require(dotnetHost)
  .initialize(dotnetVersion, dotnetHost.replace(/\.node$/, '.DotNetHost.dll'));

const Console = dotnet.Console;
Console.WriteLine('Hello from .NET!');

const version = new dotnet.Version(1, 2, 3); // Invoke overloaded constructor with args.
assert.strictEqual(version.ToString(), '1.2.3');
assert.strictEqual(version + '.4', '1.2.3.4'); // Implicit call to .NET ToString()

// Load the test module using dynamic binding `load()` instead of static binding `require()`.
const assemblyPath = process.env.TEST_DOTNET_MODULE_PATH;
const assembly = dotnet.load(assemblyPath);
console.dir(Object.keys(assembly)); // Print all public types in the loaded assembly.

const Hello = assembly['Microsoft.JavaScript.NodeApi.TestCases.Hello'];
console.dir(Object.keys(Hello)); // Print all public static members of the Hello class.

const greeting = Hello.Test('assembly'); // Call a static method.
assert.strictEqual(greeting, 'Hello assembly!');

const ClassObject = assembly.ClassObject;
assert.strictEqual(typeof ClassObject, 'function');
const instance = new ClassObject(); // Construct an instance of a class
assert.strictEqual(instance.Value, undefined);
instance.Value = 'test';
assert.strictEqual(instance.Value, 'test');

const StructObject = assembly.StructObject;
assert.strictEqual(typeof StructObject, 'function');
const instance2 = new StructObject(); // Construct an instance of a struct
assert.strictEqual(instance2.Value, undefined);
instance2.Value = 'test';
assert.strictEqual(instance2.Value, 'test');

const TestEnum = assembly.TestEnum;
assert.strictEqual(TestEnum.Two, 2);
assert.strictEqual(TestEnum[2], 'Two');

const Delegates = assembly.Delegates;
assert.strictEqual(typeof Delegates, 'object');
const funcValue = Delegates.CallFunc((value) => value + 1, 1);
assert.strictEqual(funcValue, 2);
const delegateValue = Delegates.CallDelegate((value) => value + '!', 'test');
assert.strictEqual(delegateValue, 'test!');
const delegateValue2 = Delegates.CallDotnetDelegate((dotnetAction) => dotnetAction('test'));
assert.strictEqual(delegateValue2, '#test');

async function test() {
  const interfaceObj = assembly.AsyncMethods.InterfaceTest;
  assert.strictEqual(typeof interfaceObj, 'object');

  const interfaceResult = await interfaceObj.TestAsync('buddy');
  assert.strictEqual(interfaceResult, 'Hey buddy!');

  // Invoke a C# method that calls back to a JS object that implements an interface.
  const asyncInterfaceImpl = {
    async TestAsync(greeting) { return `Hello, ${greeting}!`; }
  };
  const reverseInterfaceResult =
    await assembly.AsyncMethods.ReverseInterfaceTest(asyncInterfaceImpl, 'buddy');
  assert.strictEqual(reverseInterfaceResult, 'Hello, buddy!');
}
test().catch(assert.fail);
