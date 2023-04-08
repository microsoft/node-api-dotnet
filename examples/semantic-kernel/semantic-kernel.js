// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import * as fs from 'node:fs';
import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
import dotnet from 'node-api-dotnet';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const pkgDir = path.join(__dirname, 'pkg');

const dependencies = [
  'Microsoft.Extensions.Logging.Abstractions',
  'System.Text.Json',
  'Microsoft.Bcl.AsyncInterfaces',
  'System.Text.Encodings.Web',
  'System.Runtime.CompilerServices.Unsafe',
];

dependencies.forEach((assembly) => {
  // Find the latest installed version of the packages. (That might not be the correct version if
  // the dotnet project packages have not been restored. )
  const version = fs.readdirSync(path.join(pkgDir, assembly)).reverse()[0];
  dotnet.load(path.join(pkgDir, `${assembly}/${version}/lib/netstandard2.0/${assembly}.dll`));
});

const skVersion = fs.readdirSync(path.join(pkgDir, 'microsoft.semantickernel')).reverse()[0];

/** @type import('./Microsoft.SemanticKernel') */
const SK = dotnet.load(path.join(pkgDir,
  `microsoft.semantickernel/${skVersion}/lib/netstandard2.1/Microsoft.SemanticKernel.dll`));

export default SK;
