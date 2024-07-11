# .NET Extension Methods in JavaScript

Extension methods are important to the usability and discoverability of many .NET libraries, yet
JavaScript has no built-in support for extension methods. Since the JavaScript type system is
very dynamic, the JS marshaller can simulate extension methods by dynamically attaching the methods
to the types they apply to.

Extension methods are supported only in the
[dynamic invocation scenario](../scenarios/js-dotnet-dynamic)
since pre-built .NET APIs were most likely not designed with JavaScript in mind. But when developing
a [Node.js addon module in C#](../scenarios/js-dotnet-module) the expectation is that the APIs
specifically exported to JavaScript with `[JSExport]` should be designed without any need for
extension methods.

Extension methods may be provided by the same assembly as the target type, or a different assembly.
When provided by a different assembly, it may be necessary to explicitly import the other assembly,
otherwise the extension method will not be registered on the target type.

Here is a snippet from the Semantic Kernel example. The Semantic Kernel library makes heavy use of
extension methods.

```JS
import dotnet from 'node-api-dotnet';
import './bin/Microsoft.SemanticKernel.Core.js';
import './bin/Microsoft.SemanticKernel.Connectors.OpenAI.js';

const kernelBuilder = dotnet.Microsoft.SemanticKernel.Kernel.CreateBuilder();

// Call an extension method provided by the MS.SK.Connectors.OpenAI assembly.
kernelBuilder.AddAzureOpenAIChatCompletion(deployment, endpoint, key);
```
