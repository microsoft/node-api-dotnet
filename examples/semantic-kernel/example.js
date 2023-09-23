// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// @ts-check

import dotnet from 'node-api-dotnet';
import './bin/Microsoft.SemanticKernel.Core.js';
import './bin/Microsoft.SemanticKernel.Connectors.AI.OpenAI.js';

// The PromptTemplateEngine assembly must be explicitly loaded here, because
// SK KernelBuilder uses Assembly.Load() to load it, and that is not detected
// by the JS exporter.
import './bin/Microsoft.SemanticKernel.TemplateEngine.PromptTemplateEngine.js';

const SK = dotnet.Microsoft.SemanticKernel;
const Logging = dotnet.Microsoft.Extensions.Logging;

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

// The JS marshaller does not yet support extension methods.
const kernelBuilder = SK.OpenAIKernelBuilderExtensions.WithAzureChatCompletionService(
  SK.Kernel.Builder.WithLoggerFactory(loggerFactory),
  process.env['OPENAI_DEPLOYMENT'] || '',
  process.env['OPENAI_ENDPOINT'] || '',
  process.env['OPENAI_KEY'] || '',
);
const kernel = kernelBuilder.Build();

const skPrompt = `{{$input}}

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

// The JS marshaller does not yet support extension methods.
const summaryFunction = SK.InlineFunctionsDefinitionExtension
  .CreateSemanticFunction(kernel, skPrompt);

const summary = await SK.SKFunctionExtensions.InvokeAsync(summaryFunction, textToSummarize);

console.log(summary.toString());
