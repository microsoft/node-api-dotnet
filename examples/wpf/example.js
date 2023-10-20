// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// @ts-check

import dotnet from 'node-api-dotnet';
import './bin/PresentationFramework.js';

dotnet.System.Windows.MessageBox.Show('Hello from JS!', "Example");
