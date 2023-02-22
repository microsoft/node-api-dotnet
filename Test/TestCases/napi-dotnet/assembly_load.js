const assert = require('assert');

const dotnet = require(process.env.TEST_DOTNET_HOST_PATH);

// There's a regular .NET assembly .dll file in the same directory as the test .node module.
const assemblyPath = process.env.TEST_DOTNET_MODULE_PATH.replace(/.node$/, 'dll');

const assembly = dotnet.load(assemblyPath);
console.dir(Object.keys(assembly)); // Print all public types in the loaded assembly.

const Hello = assembly['NodeApi.TestCases.Hello'];
console.dir(Object.keys(Hello)); // Print all public static members of the Hello class.

const greeting = Hello.Test('assembly'); // Call a static method.
assert.strictEqual(greeting, 'Hello assembly!');
