// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.JavaScript.NodeApi.Generator;
using Xunit;

namespace Microsoft.JavaScript.NodeApi.Test;

#if !NETFRAMEWORK
#pragma warning disable CA1822 // Mark members as static

public class TypeDefsGeneratorTests
{
    private readonly TypeDefinitionsGenerator _generator = new(
        typeof(TypeDefsGeneratorTests).Assembly,
        assemblyDoc: null,
        referenceAssemblies: new Dictionary<string, Assembly>(),
        suppressWarnings: true);

    private string GenerateTypeDefinition(Type type)
        => _generator.GenerateTypeDefinition(type).TrimEnd();

    private string GenerateMemberDefinition(MemberInfo member)
        => _generator.GenerateMemberDefinition(member).TrimEnd();

    private class SimpleClass
    {
        public string TestProperty { get; set; } = null!;
        public string TestMethod() { return string.Empty; }
    }

    [Fact]
    public void GenerateSimpleClass()
    {
        Assert.Equal(@"
export class SimpleClass {
	constructor();

	TestProperty: string;

	TestMethod(): string;
}", GenerateTypeDefinition(typeof(SimpleClass)));
    }

    [Fact]
    public void GenerateSimpleProperty()
    {
        Assert.Equal(@"TestProperty: string;",
            GenerateMemberDefinition(
                typeof(SimpleClass).GetProperty(nameof(SimpleClass.TestProperty))!));
    }

    [Fact]
    public void GenerateSimpleMethod()
    {
        Assert.Equal(@"TestMethod(): string;",
            GenerateMemberDefinition(
                typeof(SimpleClass).GetMethod(nameof(SimpleClass.TestMethod))!));
    }

    private delegate void SimpleDelegate(string arg);

    [Fact]
    public void GenerateSimpleDelegate()
    {
        Assert.Equal(@"
export interface SimpleDelegate { (arg: string): void; }",
            GenerateTypeDefinition(typeof(SimpleDelegate)));
    }
}

#endif // !NETFRAMEWORK
