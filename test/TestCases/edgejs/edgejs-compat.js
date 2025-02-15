
const assert = require('node:assert');
const path = require('node:path');

//process.env.EDGE_USE_CORECLR=1;
const edge = require('edge-js');
assert(edge);

const dotnet = require('node-api-dotnet');
assert(dotnet);
