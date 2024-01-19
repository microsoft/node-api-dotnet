// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// @ts-check

import dotnet from 'node-api-dotnet';
import './bin/System.Text.Encodings.Web.js';
import './bin/Microsoft.Extensions.DependencyInjection.js';
import './bin/Microsoft.Extensions.Logging.Abstractions.js';
import './bin/Microsoft.SemanticKernel.Abstractions.js';
import './bin/Microsoft.SemanticKernel.Core.js';
import './bin/Microsoft.SemanticKernel.Connectors.OpenAI.js';

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

const kernelBuilder = SK.Kernel.CreateBuilder();
//kernelBuilder.WithLoggerFactory(loggerFactory);

// The JS marshaller does not yet support extension methods.
SK.OpenAIServiceCollectionExtensions.AddAzureOpenAIChatCompletion(
  kernelBuilder,
  process.env['OPENAI_DEPLOYMENT'] || '',
  process.env['OPENAI_ENDPOINT'] || '',
  process.env['OPENAI_KEY'] || '',
  // Include optional parameters to disambiguate the overload.
  undefined,
  undefined,
  undefined,
);

const kernel = SK.KernelExtensions.Build(kernelBuilder);

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

const executionSettings = new SK.Connectors.OpenAI.OpenAIPromptExecutionSettings();
executionSettings.MaxTokens = 100;

// The JS marshaller does not yet support extension methods.
const summaryFunction = SK.KernelExtensions.CreateFunctionFromPrompt(
  kernel, prompt, executionSettings);

const summarizeArguments = new Map();
summarizeArguments.set('input', textToSummarize);

const summary = await kernel.InvokeAsync(
  summaryFunction, new SK.KernelArguments(summarizeArguments, undefined));

console.log();
console.log(summary.toString());
