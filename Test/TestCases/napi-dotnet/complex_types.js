const assert = require('assert');

// Load the addon module, using either hosted or native AOT mode.
const dotnetModule = process.env['TEST_DOTNET_MODULE_PATH'];
const dotnetHost = process.env['TEST_DOTNET_HOST_PATH'];

/** @type {import('./napi-dotnet')} */
const binding = dotnetHost ? require(dotnetHost).require(dotnetModule) : require(dotnetModule);

const ComplexTypes = binding.ComplexTypes;
assert.strictEqual(typeof ComplexTypes, 'object');

assert.strictEqual(ComplexTypes.nullableInt, null);
ComplexTypes.nullableInt = 1;
assert.strictEqual(ComplexTypes.nullableInt, 1);
ComplexTypes.nullableInt = null;
assert.strictEqual(ComplexTypes.nullableInt, null);

assert.strictEqual(ComplexTypes.nullableString, null);
ComplexTypes.nullableString = 'test';
assert.strictEqual(ComplexTypes.nullableString, 'test');
ComplexTypes.nullableString = null;
assert.strictEqual(ComplexTypes.nullableString, null);

// Test an exported class.
const ClassObject = binding.ClassObject;
assert.strictEqual(typeof ClassObject, 'function');
const classInstance = new ClassObject();
assert.strictEqual(classInstance.value, null);
classInstance.value = 'test';
assert.strictEqual(classInstance.value, 'test');

// Class instances are passed by reference, so a property change on
// one reference should be reflected on the other.
const classInstance2 = classInstance.thisObject();
assert.strictEqual(classInstance2, classInstance);
classInstance2.value = 'test2';
assert.strictEqual(classInstance2.value, classInstance.value);
assert.strictEqual(classInstance.value, 'test2');

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
