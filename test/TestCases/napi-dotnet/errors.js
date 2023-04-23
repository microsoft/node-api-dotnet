const assert = require('assert');

Error.stackTraceLimit = 5;

/** @type {import('./napi-dotnet')} */
const binding = require('../common').binding;
const Errors = binding.Errors;

const dotnetNamespacePrefix = "Microsoft.JavaScript.";

function catchDotnetError() {
  let error = undefined;
  try {
    Errors.throwDotnetError('test');
  } catch (e) {
    error = e;
  }

  assert(error instanceof Error);
  assert.strictEqual(error.message, 'test');

  assert(typeof error.stack === 'string');
  console.log(error.stack);

  const stack = error.stack.split('\n').map((line) => line.trim());

  // The stack should be prefixed with the error type and message.
  const firstLine = stack.shift();
  assert.strictEqual(firstLine, 'Error: test');

  // The first line of the stack trace should refer to the .NET method that threw.
  assert(stack[0].startsWith(`at ${dotnetNamespacePrefix}`));
  assert(stack[0].includes('Errors.ThrowDotnetError('));

  // Skip over initial .NET lines in the stack trace. (Native delegates may include !<BaseAddress>)
  while (stack[0].startsWith(`at ${dotnetNamespacePrefix}`) ||
    stack[0].includes('!<')) stack.shift();

  // The first JS line of the stack trace should refer to this JS function.
  assert(stack[0].startsWith(`at ${catchDotnetError.name} `));
}
catchDotnetError();

function throwJSError(message) { throw new Error(message) }

function catchJSError() {
  let error = undefined;
  try {
    Errors.throwJSError('test', { throwJSError });
  } catch (e) {
    error = e;
  }

  assert(error instanceof Error);
  assert.strictEqual(error.message, 'test');

  assert(typeof error.stack === 'string');
  console.log(error.stack);

  const stack = error.stack.split('\n').map((line) => line.trim());

  // The stack should be prefixed with the error type and message.
  const firstLine = stack.shift();
  assert.strictEqual(firstLine, 'Error: test');

  // The first line of the stack trace should refer to the JS function that threw.
  assert(stack[0].startsWith(`at Object.${throwJSError.name} `));

  // Skip over initial non-.NET lines in the stack trace.
  while (!stack[0].startsWith(`at ${dotnetNamespacePrefix}`)) stack.shift();

  // The .NET stack trace should include the .NET method that called the JS thrower.
  while (!stack[0].includes('Errors.ThrowJSError(')) stack.shift();
  assert(stack.length > 0);

  // Skip over .NET lines in the stack trace. (Native delegates may include !<BaseAddress>)
  while (stack[0].startsWith(`at ${dotnetNamespacePrefix}`) ||
    stack[0].includes('!<')) stack.shift();

  // The following JS line of the stack trace should refer to this JS method.
  assert(stack[0].startsWith(`at ${catchJSError.name} `));
}
catchJSError();
