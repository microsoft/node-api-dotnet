// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// @ts-check

import * as path from 'node:path';
import { fileURLToPath } from 'node:url';
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

import dotnet from 'node-api-dotnet';
import './bin/PresentationFramework.js';
import './bin/WpfExample.js';

// Explicitly load some assembly dependencies that are not automatically loaded
// by the NodeApi assembly resolver. (This may be improved in the future.)
dotnet.load('System.Configuration.ConfigurationManager');
dotnet.load('System.Windows.Extensions');
dotnet.load(__dirname + '/pkg/microsoft.web.webview2/1.0.2088.41/lib/netcoreapp3.0/Microsoft.Web.WebView2.Wpf.dll');
dotnet.load('PresentationFramework.Aero2');

// Explicitly load some native library dependencies.
dotnet.load('wpfgfx_cor3');
dotnet.load(__dirname + '/bin/runtimes/win-x64/native/WebView2Loader.dll');

// Show a simple message box. (This doesn't need most of the dependencies.)
////dotnet.System.Windows.MessageBox.Show('Hello from JS!', "Example");

// Show a WPF window with a WebView2 control that renders a mermaid diagram.
const diagram = 'graph TD\n    A[Hello from JS!]';
dotnet.Microsoft.JavaScript.NodeApi.Examples.Window1.CreateWebView2Window(diagram);
