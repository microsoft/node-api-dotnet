// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assemblyName = 'Microsoft.JavaScript.NodeApi';

// TODO: Inspect the system to select the best target framework from among the
// framework(s) carried in this npm package.
const targetFramework = 'net7.0';
const runtimeIdentifier = getRuntimeIdentifier();

const nativeHostPath = __dirname + `/${runtimeIdentifier}/${assemblyName}.node`;
const managedHostPath = __dirname + `/${targetFramework}/${assemblyName}.DotNetHost.dll`

// The Node API module may need the require() function at initialization time; passing it as
// a global is the best (only?) solution, since it cannot be obtained via any `napi_*` function.
global.require = require;

const nativeHost = require(nativeHostPath);
module.exports = nativeHost.initialize(managedHostPath);

function getRuntimeIdentifier() {
  function getRuntimePlatformIdentifier() {
    switch (process.platform) {
      case 'win32': return 'win'
      case 'darwin': return 'osx'
      default: return process.platform;
    }
  }

  function getRuntimeArchIdentifier() {
    switch (process.arch) {
      case 'ia32': return 'x86'
      default: return process.arch
    }
  }

  return `${getRuntimePlatformIdentifier()}-${getRuntimeArchIdentifier()}`;
}
