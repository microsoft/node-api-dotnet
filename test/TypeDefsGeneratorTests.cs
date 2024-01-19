// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.JavaScript.NodeApi.Generator;
using Xunit;

namespace Microsoft.JavaScript.NodeApi.Test;

#if !NETFRAMEWORK
#pragma warning disable CA1822 // Mark members as static

public class TypeDefsGeneratorTests
{
    private static TypeDefinitionsGenerator CreateTypeDefinitionsGenerator(
        IEnumerable<KeyValuePair<string, string>> docs)
    {
        return CreateTypeDefinitionsGenerator(docs.Select((pair) =>
            new KeyValuePair<string, XElement>(pair.Key, new XElement("summary", pair.Value))));
    }

    private static TypeDefinitionsGenerator CreateTypeDefinitionsGenerator(
        IEnumerable<KeyValuePair<string, XElement>> docs)
    {
        string ns = typeof(TypeDefsGeneratorTests).FullName + "+";
        XDocument docsXml = new(new XElement("root", new XElement("members",
            docs.Select((pair) => new XElement("member",
                new XAttribute("name", pair.Key.Insert(2, ns)),
                pair.Value)))));
        return new TypeDefinitionsGenerator(
            typeof(TypeDefsGeneratorTests).Assembly,
            assemblyDoc: docsXml,
            referenceAssemblies: new Dictionary<string, Assembly>(),
            suppressWarnings: true);
    }

    private string GenerateTypeDefinition(Type type, IDictionary<string, string> docs)
        => CreateTypeDefinitionsGenerator(docs).GenerateTypeDefinition(type).TrimEnd();

    private string GenerateMemberDefinition(MemberInfo member, IDictionary<string, string> docs)
        => CreateTypeDefinitionsGenerator(docs).GenerateMemberDefinition(member).TrimEnd();

    private string GenerateTypeDefinition(Type type, IDictionary<string, XElement> docs)
        => CreateTypeDefinitionsGenerator(docs).GenerateTypeDefinition(type).TrimEnd();

    private string GenerateMemberDefinition(MemberInfo member, IDictionary<string, XElement> docs)
        => CreateTypeDefinitionsGenerator(docs).GenerateMemberDefinition(member).TrimEnd();

    private interface SimpleInterface
    {
        string TestProperty { get; set; }
        string TestMethod();
    }

    [Fact]
    public void GenerateSimpleInterface()
    {
        // NOTE: String literals in these tests use TABS for indentation!
        Assert.Equal("""

            /** interface */
            export interface SimpleInterface {
            	/** property */
            	TestProperty: string;

            	/** method */
            	TestMethod(): string;
            }
            """.ReplaceLineEndings(),
        GenerateTypeDefinition(typeof(SimpleInterface), new Dictionary<string, string>
        {
            ["T:SimpleInterface"] = "interface",
            ["P:SimpleInterface.TestProperty"] = "property",
            ["M:SimpleInterface.TestMethod"] = "method",
        }));
    }

    private class SimpleClass : SimpleInterface
    {
        public string TestProperty { get; set; } = null!;
        public string TestMethod() { return string.Empty; }
    }

    [Fact]
    public void GenerateSimpleClass()
    {
        Assert.Equal("""

            /** class */
            export class SimpleClass {
            	/** constructor */
            	constructor();

            	/** property */
            	TestProperty: string;

            	/** method */
            	TestMethod(): string;
            }
            """.ReplaceLineEndings(),
        GenerateTypeDefinition(typeof(SimpleClass), new Dictionary<string, string>
        {
            ["T:SimpleClass"] = "class",
            ["M:SimpleClass.#ctor"] = "constructor",
            ["P:SimpleClass.TestProperty"] = "property",
            ["M:SimpleClass.TestMethod"] = "method",
        }));
    }

    [Fact]
    public void GenerateSimpleProperty()
    {
        Assert.Equal(@"TestProperty: string;",
            GenerateMemberDefinition(
                typeof(SimpleClass).GetProperty(nameof(SimpleClass.TestProperty))!,
                new Dictionary<string, string>()));
    }

    [Fact]
    public void GenerateSimpleMethod()
    {
        Assert.Equal(@"TestMethod(): string;",
            GenerateMemberDefinition(
                typeof(SimpleClass).GetMethod(nameof(SimpleClass.TestMethod))!,
                new Dictionary<string, string>()));
    }

    private delegate void SimpleDelegate(string arg);

    [Fact]
    public void GenerateSimpleDelegate()
    {
        Assert.Equal("""

            /** delegate */
            export interface SimpleDelegate { (arg: string): void; }
            """.ReplaceLineEndings(),
        GenerateTypeDefinition(typeof(SimpleDelegate), new Dictionary<string, string>
        {
            ["T:SimpleDelegate"] = "delegate",
        }));
    }

    private enum TestEnum
    {
        Zero = 0,
        One = 1,
    }

    [Fact]
    public void GenerateEnum()
    {
        Assert.Equal("""

            /** enum */
            export enum TestEnum {
            	/** zero */
            	Zero = 0,

            	/** one */
            	One = 1,
            }
            """.ReplaceLineEndings(),
        GenerateTypeDefinition(typeof(TestEnum), new Dictionary<string, string>
        {
            ["T:TestEnum"] = "enum",
            ["F:TestEnum.Zero"] = "zero",
            ["F:TestEnum.One"] = "one",
        }));
    }

    private interface GenericInterface<T>
    {
        T TestProperty { get; set; }
        T TestMethod(T value);
    }

    [Fact]
    public void GenerateGenericInterface()
    {
        Assert.Equal("""

            /** [Generic type factory] generic-interface */
            export function GenericInterface$(T: IType<any>): GenericInterface$$1<any>;

            /** generic-interface */
            export interface GenericInterface$$1<T> {
            }

            /** generic-interface */
            export interface GenericInterface$1<T> {
            	/** instance-property */
            	TestProperty: T;

            	/** instance-method */
            	TestMethod(value: T): T;
            }
            """.ReplaceLineEndings(),
        GenerateTypeDefinition(typeof(GenericInterface<>), new Dictionary<string, string>
        {
            ["T:GenericInterface`1"] = "generic-interface",
            ["P:GenericInterface`1.TestProperty"] = "instance-property",
            ["M:GenericInterface`1.TestMethod(`0)"] = "instance-method",
        }));
    }

    private class GenericClass<T> : GenericInterface<T>
    {
        public GenericClass(T value) { TestProperty = value; }
        public T TestProperty { get; set; } = default!;
        public T TestMethod(T value) { return value; }
        public static T TestStaticProperty { get; set; } = default!;
        public static T TestStaticMethod(T value) { return value; }
    }

    [Fact]
    public void GenerateGenericClass()
    {
        Assert.Equal("""

            /** [Generic type factory] generic-class */
            export function GenericClass$(T: IType<any>): GenericClass$$1<any>;

            /** generic-class */
            export interface GenericClass$$1<T> {
            	/** constructor */
            	new(value: T): GenericClass$1<T>;

            	/** static-property */
            	TestStaticProperty: T;

            	/** static-method */
            	TestStaticMethod(value: T): T;
            }

            /** generic-class */
            export interface GenericClass$1<T> {
            	/** instance-property */
            	TestProperty: T;

            	/** instance-method */
            	TestMethod(value: T): T;
            }
            """.ReplaceLineEndings(),
        GenerateTypeDefinition(typeof(GenericClass<>), new Dictionary<string, string>
        {
            ["T:GenericClass`1"] = "generic-class",
            ["M:GenericClass`1.#ctor(`0)"] = "constructor",
            ["P:GenericClass`1.TestStaticProperty"] = "static-property",
            ["M:GenericClass`1.TestStaticMethod(`0)"] = "static-method",
            ["P:GenericClass`1.TestProperty"] = "instance-property",
            ["M:GenericClass`1.TestMethod(`0)"] = "instance-method",
        }));
    }

    private delegate T GenericDelegate<T>(T arg);

    [Fact]
    public void GenerateGenericDelegate()
    {
        Assert.Equal("""

            /** [Generic type factory] generic-delegate */
            export function GenericDelegate$(T: IType<any>): GenericDelegate$$1<any>;

            /** generic-delegate */
            export interface GenericDelegate$$1<T> {
            	new(func: (arg: T) => T): GenericDelegate$1<T>;
            }

            /** generic-delegate */
            export interface GenericDelegate$1<T> { (arg: T): T; }
            """.ReplaceLineEndings(),
        GenerateTypeDefinition(typeof(GenericDelegate<>), new Dictionary<string, string>
        {
            ["T:GenericDelegate`1"] = "generic-delegate",
        }));
    }

    [Fact]
    public void GenerateJSDocLink()
    {
        Assert.Equal("""

            /** Link to {@link SimpleClass}. */
            export interface SimpleInterface {
            	TestProperty: string;

            	TestMethod(): string;
            }
            """.ReplaceLineEndings(),
        GenerateTypeDefinition(typeof(SimpleInterface), new Dictionary<string, XElement>
        {
            ["T:SimpleInterface"] = new XElement("summary",
                "Link to ",
                new XElement("see", new XAttribute("cref", "SimpleClass")),
                "."),
        }));
    }
}

#endif // !NETFRAMEWORK
