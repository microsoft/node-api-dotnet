# Classes and Interfaces

## Export a .NET class to JavaScript

C# code in a [.NET Module for Node.js](../scenarios/js-dotnet-module) can export classes to JS.
When a class is exported, all `public` constructors, properties, and methods of the class are
made available to JavaScript (including static and non-static members). Marshalling code for
the exported class and members is
[generated at compile time](../features/js-dotnet-marshalling#compile-time-code-generation).

```C#
// Assembly: ExampleLibrary
namespace Microsoft.JavaScript.NodeApi.Examples;

[JSExport]
public class ExampleClass
{
    public ExampleClass(string name) { Name = name; }
    public string Name { get; set; }
    public string Hello() => $"Hello {Name}!";
}
```
```JS
import { ExampleClass } from './bin/ExampleLibrary.js'; // generated
const example = new ExampleClass('.NET');
const name = example.name; // returns ".NET"
const result = example.hello(); // returns "Hello .NET!"
```

Note the class's namespace is not projected to JavaScript, rather the class is exported directly
from the assembly module. Also the class's properties and methods are automatically converted to
camel-case when the class is exported. This makes it easier to develop modules that follow both
C# and JavaScript naming conventions.

## Dynamically import a .NET assembly and class

JavaScript code can use the `node-api-dotnet` package to
[dynamically load .NET assemblies and classes](../scenarios/js-dotnet-dynamic). This is typically
used when the assembly is already built (often acquired via a nuget package), rather than
custom-developed for JS interop as in the previous example.

```JS
import dotnet from 'node-api-dotnet';
import './bin/Microsoft.SemanticKernel.Core.js';              // generated
import './bin/Microsoft.SemanticKernel.Connectors.OpenAI.js'; // generated

const kernelBuilder = dotnet.Microsoft.SemanticKernel.Kernel.CreateBuilder();
kernelBuilder.AddAzureOpenAIChatCompletion(…);
```

When dyanmically importing APIs like this, the imported types are fully namespaced, and members are
_not_ camel-cased. Importing each assembly merges the namespaces and types from the assembly onto
the `dotnet` object. (The types are actually delay-loaded, meaning the
[marshalling](../features/js-dotnet-marshalling#runtime-code-generation) code for each type is not
generated until first use.)

See the [Semantic Kernel](https://github.com/microsoft/node-api-dotnet/tree/main/examples/semantic-kernel)
project for a more complete example of generating type definitions for nuget packages, loading the
assemblies, and dynamically calling the APIs.

## Marshalling .NET classes to JS

.NET class instances are marshalled by reference. That means that when an instance of .NET class is
constructed by JS or passed from .NET to JS, the instance data is not copied. Instead, the JS value
created by the marshaller is a proxy to the .NET value. Any calls to properties or methods on the
JS object get marshalled over to the original .NET object, and the .NET return value is marshalled
back to JS. And when the JS proxy object is garbage-collected, the .NET object can also be
garbage-collected (if there are no other .NET references to the object).

The JS object is not a actually JavaScript
[`Proxy`](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Global_Objects/Proxy),
rather it is an instance of a JavaScript class (registered with
[`DefineClass()`](./dotnet/Microsoft.JavaScript.NodeApi.Interop/JSClassBuilder-1/DefineClass))
whose property getters & setters and methods all have .NET callback functions.

## Implement a .NET interface with a JS class

The [TypeScript type-definitions generator](../features/type-definitions.md) converts a .NET
interface into a TypeScript interface. Then a JavaScript (TypeScript) class can implement
implement the interface, and an instance of that JS class can be passed to a .NET API that
expects an instance of the interface.

```C#
// Assembly: ExampleLibrary

[JSExport]
public interface IExampleInterface
{
    void CallBack(string value);
}

[JSExport]
public class ExampleClass
{
    public static string CallMeBack(IExampleInterface caller, string value)
        => caller.CallBack(value);
}
```

```JS
import {
    IExampleInterface,
    ExampleClass,
} from './bin/ExampleLibrary.js'; // generated

class ExampleImplementation extends IExampleInterface {
    public void callBack(value: string) {
        console.log(`callBack(${value})`);
    }
}

ExampleClass.callMeBack(new ExampleImplementation());

```

This is one way for .NET to call back into JavaScript. Another way is to
[provide a JS function as a .NET delegate](./delegates).
