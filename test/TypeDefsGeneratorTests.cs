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

#if !(NETFRAMEWORK || NETSTANDARD)
#pragma warning disable CA1822 // Mark members as static

public class TypeDefsGeneratorTests
{
    private static TypeDefinitionsGenerator CreateTypeDefinitionsGenerator(
        IEnumerable<KeyValuePair<string, string>> docs, bool insertNamespace = true)
    {
        return CreateTypeDefinitionsGenerator(docs.Select((pair) =>
            new KeyValuePair<string, XElement>(pair.Key, new XElement("summary", pair.Value))),
            insertNamespace);
    }

    private static TypeDefinitionsGenerator CreateTypeDefinitionsGenerator(
        IEnumerable<KeyValuePair<string, XElement>> docs, bool insertNamespace = true)
    {
        string ns = typeof(TypeDefsGeneratorTests).Namespace + ".";
        XDocument docsXml = new(new XElement("root", new XElement("members",
            docs.Select((pair) => new XElement("member",
                new XAttribute("name", insertNamespace ? pair.Key.Insert(2, ns) : pair.Key),
                pair.Value)))));
        TypeDefinitionsGenerator generator = new(
            typeof(TypeDefsGeneratorTests).Assembly,
            referenceAssemblies: new Dictionary<string, Assembly>())
        {
            ExportAll = true,
            SuppressWarnings = true,
        };
        generator.LoadAssemblyDoc(typeof(TypeDefsGeneratorTests).Assembly.GetName().Name!, docsXml);
        return generator;
    }

    private string GenerateTypeDefinition(
        Type type,
        IDictionary<string, string> docs,
        bool insertNamespace = true)
        => CreateTypeDefinitionsGenerator(docs, insertNamespace)
            .GenerateTypeDefinition(type).TrimEnd();

    private string GenerateMemberDefinition(MemberInfo member, IDictionary<string, string> docs)
        => CreateTypeDefinitionsGenerator(docs).GenerateMemberDefinition(member).TrimEnd();

    private string GenerateTypeDefinition(Type type, IDictionary<string, XElement> docs)
        => CreateTypeDefinitionsGenerator(docs).GenerateTypeDefinition(type).TrimEnd();

    private string GenerateMemberDefinition(MemberInfo member, IDictionary<string, XElement> docs)
        => CreateTypeDefinitionsGenerator(docs).GenerateMemberDefinition(member).TrimEnd();

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

    [Fact]
    public void GenerateSimpleClass()
    {
        Assert.Equal("""

            /** class */
            export class SimpleClass implements SimpleInterface {
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

    [Fact]
    public void GenerateGenericInterface()
    {
        Assert.Equal("""

            /** [Generic type factory] generic-interface */
            export function GenericInterface$(T: IType): IType;

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

    [Fact]
    public void GenerateGenericClass()
    {
        Assert.Equal("""

            /** [Generic type factory] generic-class */
            export function GenericClass$(T: IType): typeof GenericClass$1<any>;

            /** generic-class */
            export class GenericClass$1<T> implements GenericInterface$1<T> {
            	/** constructor */
            	new(value: T): GenericClass$1<T>;

            	/** instance-property */
            	TestProperty: T;

            	/** static-property */
            	static TestStaticProperty: any;

            	/** instance-method */
            	TestMethod(value: T): T;

            	/** static-method */
            	static TestStaticMethod(value: any): any;
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

    [Fact]
    public void GenerateGenericDelegate()
    {
        Assert.Equal("""

            /** [Generic type factory] generic-delegate */
            export function GenericDelegate$(T: IType): IType;

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

    [Fact]
    public void GenerateExtensionMethods()
    {
        string extensionsName = typeof(SimpleClassExtensions).FullName!;
        Assert.Equal("""

            export namespace SimpleClassExtensions {
            	/** extension A */
            	export function TestExtensionA(value: SimpleClass): void;

            	/** extension B */
            	export function TestExtensionB(value: SimpleClass): void;
            }

            /** Extension methods from {@link Microsoft.JavaScript.NodeApi.Test.SimpleClassExtensions} */
            export interface SimpleClass {
            	/** extension A */
            	TestExtensionA(): void;

            	/** extension B */
            	TestExtensionB(): void;
            }
            """.ReplaceLineEndings(),
        GenerateTypeDefinition(typeof(SimpleClassExtensions), new Dictionary<string, string>
        {
            [$"M:{extensionsName}.TestExtensionA({typeof(SimpleClass).FullName})"] = "extension A",
            [$"M:{extensionsName}.TestExtensionB({typeof(SimpleClass).FullName})"] = "extension B",
        }, insertNamespace: false));
    }
}

public interface SimpleInterface
{
    string TestProperty { get; set; }
    string TestMethod();
}

public class SimpleClass : SimpleInterface
{
    public string TestProperty { get; set; } = null!;
    public string TestMethod() { return string.Empty; }
}

public delegate void SimpleDelegate(string arg);

public enum TestEnum
{
    Zero = 0,
    One = 1,
}


public interface GenericInterface<T>
{
    T TestProperty { get; set; }
    T TestMethod(T value);
}

public class GenericClass<T> : GenericInterface<T>
{
    public GenericClass(T value) { TestProperty = value; }
    public T TestProperty { get; set; } = default!;
    public T TestMethod(T value) { return value; }
    public static T TestStaticProperty { get; set; } = default!;
    public static T TestStaticMethod(T value) { return value; }
}

public delegate T GenericDelegate<T>(T arg);

public static class SimpleClassExtensions
{
    public static void TestExtensionA(this SimpleClass value)
        => value.TestMethod();
    public static void TestExtensionB(this SimpleClass value)
        => value.TestMethod();
}

#endif
