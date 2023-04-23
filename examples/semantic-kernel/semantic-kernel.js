// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import dotnet from 'node-api-dotnet';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const pkgDir = path.join(__dirname, 'pkg');

const skAssemblyName = 'Microsoft.SemanticKernel.Core';
const skOpenAIAssemblyName = 'Microsoft.SemanticKernel.Connectors.AI.OpenAI';
const skVersion = fs.readdirSync(path.join(pkgDir, skAssemblyName.toLowerCase())).reverse()[0];

function resolveAssembly(name, version) {
  if (/\d+\.\d+\.\d+\.0/.test(version)) version = version.substring(0, version.length - 2);
  const versions = fs.readdirSync(path.join(pkgDir, name.toLowerCase()));
  version = versions.find((v) => v.startsWith(version)) ?? version;
  const filePath = path.join(
    pkgDir, name.toLowerCase(), version, 'lib', 'netstandard2.0', name + '.dll');
  return filePath;
}

dotnet.addListener('resolving', (name, version) => {
  const filePath = resolveAssembly(name, version);
  if (fs.existsSync(filePath)) dotnet.load(filePath);
});

/** @type import('./Microsoft.SemanticKernel.Core') */
const SK = dotnet.load(resolveAssembly(skAssemblyName, skVersion));
/** @type import('./Microsoft.SemanticKernel.Connectors.AI.OpenAI') */
const SKOpenAI = dotnet.load(resolveAssembly(skOpenAIAssemblyName, skVersion));

export { SK, SKOpenAI };
