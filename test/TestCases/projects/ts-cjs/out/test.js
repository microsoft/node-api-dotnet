"use strict";
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
Object.defineProperty(exports, "__esModule", { value: true });
const dotnet = require("node-api-dotnet");
require("./bin/System.Runtime");
require("./bin/System.Console");
dotnet.System.Console.WriteLine('Test!');
