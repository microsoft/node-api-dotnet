#!/usr/bin/env node

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const path = require('path');
const assemblyDir = path.join(__dirname, 'net6.0');

const dotnet = require('node-api-dotnet');

// The generator depends on these assemblies; for now they have to be loaded explicitly.
dotnet.load(path.join(assemblyDir, 'System.Reflection.MetadataLoadContext.dll'));
dotnet.load(path.join(assemblyDir, 'Microsoft.CodeAnalysis.dll'));

const Generator = dotnet.load(path.join(assemblyDir, 'Microsoft.JavaScript.NodeApi.Generator.dll'));

const args = process.argv.slice(2);
Generator.Program.Main(args);
