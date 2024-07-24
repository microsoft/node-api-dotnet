// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');
const common = require('../common');

/** @type {import('./napi-dotnet')} */
const binding = common.binding;

common.runTest(async () => {
  const result = await binding.async_method('buddy');
  assert.strictEqual(result, 'Hey buddy!');

  const result2 = await binding.async_method_cs('buddy');
  assert.strictEqual(result2, 'Hey buddy!');

  const result3 = await binding.async_interface.testAsync('buddy');
  assert.strictEqual(result3, 'Hey buddy!');

  // Invoke a C# method that calls back to a JS object that implements an interface.
  const asyncInterfaceImpl = {
    async testAsync(greeting) { return `Hello, ${greeting}!`; }
  };
  const result4 = await binding.async_interface_reverse(asyncInterfaceImpl, 'buddy');
  assert.strictEqual(result4, 'Hello, buddy!');

  // A JS object that implements an interface can be returned from C#.
  binding.async_interface = asyncInterfaceImpl;
  assert.strictEqual(binding.async_interface, asyncInterfaceImpl);

  // Invoke C# methods that return ValueTask.
  await binding.async_method_valuetask();
  const result5 = await binding.async_method_valuetask_of_string('buddy');
  assert.strictEqual(result5, 'Hey buddy!');
});

