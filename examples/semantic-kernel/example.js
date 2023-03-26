// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

function getenv(name) {
  const value = process.env[name];
  if (!value) {
    console.error(`Missing ${name} environment variable.`);
    process.exit(1);
  }
  return value;
}

const SK = require('./semantic-kernel');

const kernel = SK.Kernel.Builder.Build();

kernel.Config.AddAzureOpenAICompletionBackend(
  'davinci-backend',
  getenv('OPENAI_DEPLOYMENT'),
  getenv('OPENAI_ENDPOINT'),
  getenv('OPENAI_KEY'),
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

kernel.RunAsync(textToSummarize, [tldrFunction]).then((summary) => {
  console.log(summary.toString());
});
