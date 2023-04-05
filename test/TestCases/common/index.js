// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

/* Test helpers ported from test/common/index.js in Node.js project. */
'use strict';
const assert = require('assert');
const path = require('path');
const { access } = require('node:fs/promises');

const noop = () => { };

const mustCallChecks = [];

function runCallChecks(exitCode) {
  if (exitCode !== 0) return;

  const failed = mustCallChecks.filter(function (context) {
    if ('minimum' in context) {
      context.messageSegment = `at least ${context.minimum}`;
      return context.actual < context.minimum;
    } else {
      context.messageSegment = `exactly ${context.exact}`;
      return context.actual !== context.exact;
    }
  });

  failed.forEach(function (context) {
    console.log('Mismatched %s function calls. Expected %s, actual %d.',
      context.name,
      context.messageSegment,
      context.actual);
    console.log(context.stack.split('\n').slice(2).join('\n'));
  });

  if (failed.length) process.exit(1);
}

exports.mustCall = function (fn, exact) {
  return _mustCallInner(fn, exact, 'exact');
};
exports.mustCallAtLeast = function (fn, minimum) {
  return _mustCallInner(fn, minimum, 'minimum');
};

function _mustCallInner(fn, criteria, field) {
  if (typeof fn === 'number') {
    criteria = fn;
    fn = noop;
  } else if (fn === undefined) {
    fn = noop;
  }
  if (criteria === undefined) {
    criteria = 1;
  }

  if (typeof criteria !== 'number') { throw new TypeError(`Invalid ${field} value: ${criteria}`); }

  const context = {
    [field]: criteria,
    actual: 0,
    stack: (new Error()).stack,
    name: fn.name || '<anonymous>'
  };

  // add the exit listener only once to avoid listener leak warnings
  if (mustCallChecks.length === 0) process.on('exit', runCallChecks);

  mustCallChecks.push(context);

  return function () {
    context.actual++;
    return fn.apply(this, arguments);
  };
}

exports.mustNotCall = function (msg) {
  return function mustNotCall() {
    assert.fail(msg || 'function should not have been called');
  };
};

const buildTypes = {
  Release: 'Release',
  Debug: 'Debug'
};

async function checkBuildType(buildType) {
  try {
    await access(path.join(path.resolve('./test/build'), buildType));
    return true;
  } catch {
    return false;
  }
}

async function whichBuildType() {
  let buildType = 'Release';
  const envBuildType = process.env.NODE_API_BUILD_CONFIG;
  if (envBuildType) {
    if (Object.values(buildTypes).includes(envBuildType)) {
      if (await checkBuildType(envBuildType)) {
        buildType = envBuildType;
      } else {
        throw new Error(`The ${envBuildType} build doesn't exists.`);
      }
    } else {
      throw new Error('Invalid value for NODE_API_BUILD_CONFIG environment variable. It should be set to Release or Debug.');
    }
  }
  return buildType;
}

exports.whichBuildType = whichBuildType;

// Load the addon module, using either hosted or native AOT mode.
exports.dotnetHost = process.env.TEST_DOTNET_HOST_PATH;
exports.dotnetModule = process.env.TEST_DOTNET_MODULE_PATH;
exports.dotnetVersion = process.env.TEST_DOTNET_VERSION;

exports.loadDotnetModule = function () {
  let dotnetModule;
  if (exports.dotnetHost) {
    // Normally the init.js script in the npm package takes care of locating the correct
    // native host and managed host binaries for the current environment.
    dotnetModule = require(exports.dotnetHost)
      .initialize(
        exports.dotnetVersion,
        exports.dotnetHost.replace(/\.node$/, '.DotNetHost.dll'),
        require)
      .require(exports.dotnetModule);
  } else {
    // The Node API module may need the require() function at initialization time; passing it via a
    // global is the only solution for AOT, since it cannot be obtained via any `napi_*` function.
    global.node_api_dotnet = { require };

    dotnetModule = require(exports.dotnetModule);
  }

  return dotnetModule;
}

exports.binding = exports.loadDotnetModule();

exports.runTest = async function (test) {
  await Promise.resolve(test(exports.binding))
    .finally(exports.mustCall());
};

exports.runTestWithBindingPath = async function (test, buildType, buildPathRoot = process.env.BUILD_PATH || '') {
  buildType = buildType || await whichBuildType();

  const bindings = [
    path.join(buildPathRoot, `../build/${buildType}/binding.node`),
    path.join(buildPathRoot, `../build/${buildType}/binding_noexcept.node`),
    path.join(buildPathRoot, `../build/${buildType}/binding_noexcept_maybe.node`),
    path.join(buildPathRoot, `../build/${buildType}/binding_custom_namespace.node`)
  ].map(it => require.resolve(it));

  for (const item of bindings) {
    await test(item);
  }
};

exports.runTestWithBuildType = async function (test, buildType) {
  buildType = buildType || await whichBuildType();

  await Promise.resolve(test(buildType))
    .finally(exports.mustCall());
};
