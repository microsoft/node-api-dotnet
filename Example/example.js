// Construct a .NET runtime identifier from the current platform and CPU arch.
// (This needs more logic to make it work across more platforms.)
const rid = require('os').platform().replace('32', '') + '-' + process.arch;

// Load the addon module.
const example = require(`../out/bin/Debug/Example/net7.0/${rid}/native/example`);

// Check the properties that are on the module.
console.log('example keys: ' + JSON.stringify(Object.keys(example)));

// Call a method exported by the addon module.
example.hello();
