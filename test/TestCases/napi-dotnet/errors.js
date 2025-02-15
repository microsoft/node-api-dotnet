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
  console.log('catchDotnetError:');
  console.log(error.stack);
  console.log();

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
  console.log('catchJSError:');
  console.log(error.stack);
  console.log();

  const stack = error.stack.split('\n').map((line) => line.trim());

  // The stack should be prefixed with the error type and message.
  const firstLine = stack.shift();
  assert.strictEqual(firstLine, 'Error: test');

  // The first line of the stack trace should refer to the JS function that threw.
  assert(stack[0].startsWith(`at Object.${throwJSError.name} `));

  // Skip over initial non-.NET lines in the stack trace.
  while (stack.length > 0 && !stack[0].startsWith(`at ${dotnetNamespacePrefix}`)) stack.shift();

  // The .NET stack trace should include the .NET method that called the JS thrower.
  while (stack.length > 0 && !stack[0].includes('ThrowJSError(')) stack.shift();
  assert(stack.length > 0);

  // Skip over .NET lines in the stack trace. (Native delegates may include !<BaseAddress>)
  while (stack.length > 0 && (stack[0].startsWith(`at ${dotnetNamespacePrefix}`) ||
    stack[0].includes('!<'))) stack.shift();
  assert(stack.length > 0);

  // The following JS line of the stack trace should refer to this JS method.
  assert(stack[0].startsWith(`at ${catchJSError.name} `));
}
catchJSError();

function catchDotnetToJSToDotnetError() {
  let error = undefined;
  try {
    Errors.throwJSError('test', { throwJSError: (message) => Errors.throwDotnetError(message) });
  } catch (e) {
    error = e;
  }

  assert(error instanceof Error);
  assert.strictEqual(error.message, 'test');

  assert(typeof error.stack === 'string');
  console.log('catchDotnetToJSToDotnetError:');
  console.log(error.stack);
  console.log();

  const stack = error.stack.split('\n').map((line) => line.trim());

  // The stack should be prefixed with the error type and message.
  const firstLine = stack.shift();
  assert.strictEqual(firstLine, 'Error: test');

  // The first line of the stack trace should refer to the .NET method that threw.
  assert(stack[0].startsWith(`at ${dotnetNamespacePrefix}`));
  assert(stack[0].includes('Errors.ThrowDotnetError('));

  // Skip over following .NET lines in the stack trace. (Native delegates may include !<BaseAddress>)
  while (stack[0].startsWith(`at ${dotnetNamespacePrefix}`) ||
    stack[0].includes('!<')) stack.shift();

  // The first JS line of the stack trace should refer to the JS interface callback method.
  assert(stack[0].startsWith('at Object.throwJSError'));
  stack.shift();

  // The next JS line of the stack trace should refer to this JS function.
  assert(stack[0].startsWith(`at ${catchDotnetToJSToDotnetError.name} `));
  stack.shift();

  // Skip over following JS lines in the stack trace.
  while (!stack[0].startsWith(`at ${dotnetNamespacePrefix}`))
    stack.shift();

  // The .NET stack trace should include the .NET method that called the JS thrower.
  while (stack.length > 0 && !stack[0].includes('ThrowJSError(')) stack.shift();
  assert(stack.length > 0);

  // Skip over .NET following lines in the stack trace. (Native delegates may include !<BaseAddress>)
  while (stack.length > 0 && (stack[0].startsWith(`at ${dotnetNamespacePrefix}`) ||
    stack[0].includes('!<'))) stack.shift();
  assert(stack.length > 0);

  // The next JS line of the stack trace should refer to this JS method.
  assert(stack[0].startsWith(`at ${catchDotnetToJSToDotnetError.name} `));
}
catchDotnetToJSToDotnetError();

async function catchAsyncDotnetError() {
  let error = undefined;
  try {
    await Errors.throwAsyncDotnetError('test');
  } catch (e) {
    error = e;
  }

  assert(error instanceof Error);
  assert.strictEqual(error.message, 'test');

  assert(typeof error.stack === 'string');
  console.log('catchAsyncDotnetError:');
  console.log(error.stack);
  console.log();

  const stack = error.stack.split('\n').map((line) => line.trim());

  // The stack should be prefixed with the error type and message.
  const firstLine = stack.shift();
  assert.strictEqual(firstLine, 'Error: test');

  // The first line of the stack trace should refer to the .NET method that threw.
  assert(stack[0].startsWith(`at ${dotnetNamespacePrefix}`));
  assert(stack[0].includes('ThrowAsyncDotnetError'));

  // Unfortunately the JS stack trace is not available for errors thrown by a .NET async method.
  // That is because the Task to Promise conversion uses Promise APIs which do not preserve
  // the JS stack. See https://v8.dev/blog/fast-async#improved-developer-experience
}
catchAsyncDotnetError();
