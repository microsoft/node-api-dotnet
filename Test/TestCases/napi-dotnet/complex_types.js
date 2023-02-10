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

// C# arrays are copied to/from JS, so modifying the returned array doesn't affect the original.
const stringArrayValue = ComplexTypes.stringArray;
assert(Array.isArray(stringArrayValue));
assert.strictEqual(stringArrayValue.length, 0);
ComplexTypes.stringArray = [ 'test' ];
assert.notStrictEqual(ComplexTypes.stringArray, stringArrayValue);
assert.strictEqual(ComplexTypes.stringArray[0], 'test');
ComplexTypes.stringArray[0] = 'test2';
assert.strictEqual(ComplexTypes.stringArray[0], 'test');

// C# Memory<T> maps to/from JS TypedArray (without copying) for valid typed-array element types.
const uintArrayValue = ComplexTypes.uIntArray;
assert(uintArrayValue instanceof Uint32Array);
assert.strictEqual(uintArrayValue.length, 0);
const uintArrayValue2 = new Uint32Array([0, 1, 2]);
ComplexTypes.uIntArray = uintArrayValue2;
assert.strictEqual(ComplexTypes.uIntArray.length, 3);
assert.strictEqual(ComplexTypes.uIntArray[1], 1);

/*
// C# IList<T> maps to/from JS Array<T> (without copying).
const listValue = ComplexTypes.list;
assert(Array.isArray(listValue));
assert.strictEqual(listValue.length, 0);
ComplexTypes.list = [0];
assert.notStrictEqual(ComplexTypes.list, listValue);
assert.strictEqual(ComplexTypes.list[0], 0);
listValue = ComplexTypes.list;
ComplexTypes.list[0] = 1;
assert.strictEqual(listValue[0], 1);
*/
