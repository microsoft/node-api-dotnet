// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const assert = require('assert');

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;

const Delegates = binding.Delegates;
assert.strictEqual(typeof Delegates, 'object');

let actionValue = 0;
Delegates.callAction((value) => actionValue = value, 1);
assert.strictEqual(actionValue, 1);

const funcValue = Delegates.callFunc((value) => value + 1, 1);
assert.strictEqual(funcValue, 2);

const delegateValue = Delegates.callDelegate((value) => value + '!', 'test');
assert.strictEqual(delegateValue, 'test!');

// Pass a function to a .NET method. The function is marshalled to .NET as a delegate.
// The .NET method calls that delegate, passing another delegate as an argument.
// The call with delegate argument is marshalled to JS, with delegate marshalled as a function.
// That function is then called with "test" argument. .NET prepends "#", and the
// return value is marshalled all the way back.
const delegateValue2 = Delegates.callDotnetDelegate((dotnetAction) => dotnetAction('test'));
assert.strictEqual(delegateValue2, '#test');

async function testCancellation() {
  let timeoutHandle;
  let timeout = new Promise((_, reject) => {
    timeoutHandle = setTimeout(() => reject('waitUntilCancelled() timed out.'), 5000);
  });

  let abortController = new AbortController();
  const waitPromise = Delegates.waitUntilCancelled(abortController.signal);
  setTimeout(() => abortController.abort(), 100);
  await Promise.race([waitPromise, timeout]);
  clearTimeout(timeoutHandle);

  timeout = new Promise((_, reject) => {
    timeoutHandle = setTimeout(() => reject('callDelegateAndCancel() timed out.'), 5000);
  });

  const callPromise = Delegates.callDelegateAndCancel((abortSignal) => {
    assert.strictEqual(typeof abortSignal, 'object');
    return new Promise((resolve) => {
      abortSignal.addEventListener('abort', resolve);
    });
  });
  await Promise.race([callPromise, timeout]);
  clearTimeout(timeoutHandle);
}
testCancellation().catch(assert.fail);
