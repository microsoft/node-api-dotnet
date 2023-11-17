// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// @ts-check

import dotnet from 'node-api-dotnet';
import './bin/Microsoft.Extensions.Logging.Abstractions.js';
import './bin/Microsoft.SemanticKernel.Abstractions.js';
import './bin/Microsoft.SemanticKernel.Core.js';
import './bin/Microsoft.SemanticKernel.Connectors.AI.OpenAI.js';
import './bin/Microsoft.SemanticKernel.TemplateEngine.Basic.js';

const Logging = dotnet.Microsoft.Extensions.Logging;
const SK = dotnet.Microsoft.SemanticKernel;

/** @type {dotnet.Microsoft.Extensions.Logging.ILogger} */
const logger = {
  Log(logLevel, eventId, state, exception, formatter) {
    console.log(`LOG (${Logging.LogLevel[logLevel || 0]}): ${formatter(state, exception)}`);
  },
  IsEnabled(logLevel) { return true; },
  BeginScope(state) { return { dispose() { } }; },
};
/** @type {dotnet.Microsoft.Extensions.Logging.ILoggerFactory} */
const loggerFactory = {
  CreateLogger(categoryName) { return logger; },
  AddProvider(provider) { },
  dispose() {}
};

let kernelBuilder = new SK.KernelBuilder();
kernelBuilder.WithLoggerFactory(loggerFactory);

// The JS marshaller does not yet support extension methods.
SK.OpenAIKernelBuilderExtensions.WithAzureOpenAIChatCompletionService(
  kernelBuilder,
  process.env['OPENAI_DEPLOYMENT'] || '',
  process.env['OPENAI_ENDPOINT'] || '',
  process.env['OPENAI_KEY'] || '',
);

const kernel = kernelBuilder.Build();

const prompt = `{{$input}}

Give me the TLDR in 10 words.
`;

const textToSummarize = `
1) A robot may not injure a human being or, through inaction,
allow a human being to come to harm.

2) A robot must obey orders given it by human beings except where
such orders would conflict with the First Law.

3) A robot must protect its own existence as long as such protection
does not conflict with the First or Second Law.
`;

const requestSettings = new SK.Connectors.AI.OpenAI.OpenAIRequestSettings();
requestSettings.MaxTokens = 100;

// The JS marshaller does not yet support extension methods.
const summaryFunction = SK.OpenAIKernelExtensions
  .CreateSemanticFunction(kernel, prompt, requestSettings);

const summary = await SK.SKFunctionExtensions.InvokeAsync(
  summaryFunction, textToSummarize, kernel);

console.log();
console.log(summary.toString());
