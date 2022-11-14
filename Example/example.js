var example = require('../out/bin/Debug/Example/net7.0/win-x64/native/example');
console.log('example keys: ' + JSON.stringify(Object.keys(example)));
example.hello();
