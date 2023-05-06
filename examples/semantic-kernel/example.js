// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// @ts-check

import { SK, SKOpenAI, Logging } from './semantic-kernel.js';
const LogLevel = Logging.LogLevel;

/** @type {import('./bin/Microsoft.Extensions.Logging.Abstractions').ILogger} */
const logger = {
  Log(logLevel, eventId, state, exception, formatter) {
    console.log(`LOG (${LogLevel[logLevel]}): ${formatter(state, exception)}`);
  },

  IsEnabled(logLevel) { return true; },

  BeginScope(state) { return { dispose() { } }; },
};

const kernel = SK.Kernel.Builder
  .WithLogger(logger)
  .Build();

// The JS marshaller does not yet support extension methods.
SKOpenAI.KernelConfigOpenAIExtensions.AddAzureTextCompletionService(
  kernel.Config,
  'davinci-azure',
  process.env['OPENAI_DEPLOYMENT'] || '',
  process.env['OPENAI_ENDPOINT'] || '',
  process.env['OPENAI_KEY'] || '',
);

const skPrompt = `
{{$input}}

Give me the TLDR in 5 words.
`;

const textToSummarize = `
1) A robot may not injure a human being or, through inaction,
allow a human being to come to harm.

2) A robot must obey orders given it by human beings except where
such orders would conflict with the First Law.

3) A robot must protect its own existence as long as such protection
does not conflict with the First or Second Law.
`;

// The JS marshaller does not yet support extension methods.
const tldrFunction = SK.InlineFunctionsDefinitionExtension
  .CreateSemanticFunction(kernel, skPrompt);

const summary = await tldrFunction.InvokeAsync(textToSummarize);
console.log(summary.toString());
