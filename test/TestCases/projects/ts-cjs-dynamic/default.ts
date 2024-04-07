// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import * as dotnet from 'node-api-dotnet';

import './bin/System.Runtime';
import './bin/System.Console';

dotnet.System.Console.WriteLine(`Hello from .NET ${dotnet.runtimeVersion}!`);
