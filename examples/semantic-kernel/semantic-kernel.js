// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import dotnet from 'node-api-dotnet';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const skAssemblyName = 'Microsoft.SemanticKernel.Core';
const skOpenAIAssemblyName = 'Microsoft.SemanticKernel.Connectors.AI.OpenAI';

/** All assemblies are resolved from the bin directory, where they were copied by MSBuild. */
function resolveAssembly(name) {
  return path.join(__dirname, 'bin', name + '.dll');
}

dotnet.addListener('resolving', (name, version) => {
  const filePath = resolveAssembly(name, version);
  if (fs.existsSync(filePath)) dotnet.load(filePath);
});

/** @type import('./bin/Microsoft.SemanticKernel.Core') */
const SK = dotnet.load(resolveAssembly(skAssemblyName));
/** @type import('./bin/Microsoft.SemanticKernel.Connectors.AI.OpenAI') */
const SKOpenAI = dotnet.load(resolveAssembly(skOpenAIAssemblyName));

export { SK, SKOpenAI };
