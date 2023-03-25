// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const fs = require('fs');
const path = require('path');
const dotnet = require('node-api-dotnet');

const pkgDir = path.join(__dirname, 'pkg');

const laVersion = fs.readdirSync(path.join(pkgDir, 'microsoft.extensions.logging.abstractions'))[0];
const skVersion = fs.readdirSync(path.join(pkgDir, 'microsoft.semantickernel'))[0];

// The dependency needs to be loaded explicitly because it's in a different directory.
dotnet.load(path.join(pkgDir,
  `microsoft.extensions.logging.abstractions/${laVersion}/lib/netstandard2.0/Microsoft.Extensions.Logging.Abstractions.dll`));

/** @type import('./Microsoft.SemanticKernel') */
module.exports = dotnet.load(path.join(pkgDir,
  `microsoft.semantickernel/${skVersion}/lib/netstandard2.1/Microsoft.SemanticKernel.dll`));
