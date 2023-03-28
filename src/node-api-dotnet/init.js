// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

module.exports = initialize;

/**
 * Initializes the Node API .NET host.
 * @param {string} targetFramework Minimum requested .NET version. Must be one of the target
 * framework monikers supported by the Node API .NET package. The actual loaded version of .NET
 * may be higher, if the requested version is not installed.
 * @returns {import('./index')} The Node API .NET host.
 */
function initialize(targetFramework) {
  const assemblyName = 'Microsoft.JavaScript.NodeApi';
  const runtimeIdentifier = getRuntimeIdentifier();
  const nativeHostPath = __dirname + `/${runtimeIdentifier}/${assemblyName}.node`;
  const managedHostPath = __dirname + `/${targetFramework}/${assemblyName}.DotNetHost.dll`

  // The Node API module may need the require() function at initialization time.
  // TODO: Pass this as a parameter to the native host initialize() method instead of a global.
  global.require = require;

  const nativeHost = require(nativeHostPath);
  return nativeHost.initialize(targetFramework, managedHostPath);
}

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
