// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

const dotnet = require('node-api-dotnet');
require('./bin/ClassLib');

const Class1 = dotnet.Microsoft.JavaScript.NodeApi.Examples.Class1;
const class1 = new Class1();
class1.Hello('.NET');
