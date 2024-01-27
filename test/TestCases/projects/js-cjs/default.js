// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const dotnet = require('node-api-dotnet');

require('./bin/System.Runtime');
require('./bin/System.Console');

dotnet.System.Console.WriteLine(`Hello from .NET ${dotnet.runtimeVersion}!`);
