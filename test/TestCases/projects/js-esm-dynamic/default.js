// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import dotnet from 'node-api-dotnet';

import './bin/System.Runtime.js';
import './bin/System.Console.js';

dotnet.System.Console.WriteLine(`Hello from .NET ${dotnet.runtimeVersion}!`);
