# Namespaces

.NET namespaces are projected to JavaScript only in the
[dynamic invocation scenario](../scenarios/js-dotnet-dynamic)
since pre-built .NET APIs were designed with namespaces in mind.

But when developing a [Node.js addon module in C#](../scenarios/js-dotnet-module) the APIs
specifically exported to JavaScript with `[JSExport]` are exposed on the module object without
any additional namespacing. (In that scenario, any .NET namespaces have no impact on the exported
JavaScript API.)

## Importing assemblies and namespaces

When dynamically importing .NET assemblies, all namespaces and types provided by each imported
assembly are merged onto the top-level `dotnet` object (created by the `node-api-dotnet` package).

In this snippet from the Semantic Kernel example, JavaScript code imports three .NET SemanticKernel
assemblies. Note the result of the `import` statement is not named or assigned, because these
imports do not return _modules_, rather the imports cause a side-effect of merging all of the
types into the combined .NET namespace hierarchy.

```JS
import dotnet from 'node-api-dotnet';
import './bin/Microsoft.SemanticKernel.Abstractions.js';
import './bin/Microsoft.SemanticKernel.Core.js';
import './bin/Microsoft.SemanticKernel.Connectors.OpenAI.js';

// All of the namespaces, types, and extension methods from the 3 imported
// assemblies are now available on the `dotnet` object.
const kernelBuilder = dotnet.Microsoft.SemanticKernel.Kernel.CreateBuilder();
```

The import mechanism is designed this way because when working with .NET APIs there is an
expectation that all .NET types are in a single combined namespace hierarchy. Since each .NET
assembly can provide types to multiple namespaces, and multiple assemblies can provide types to
each namespace, .NET developers are typically not aware of precisely which assembly provides
every type or extension method. (Even if you think you know, you might be mistaken because types
can be [forwarded](https://learn.microsoft.com/en-us/dotnet/standard/assembly/type-forwarding) to
other assemblies.) This is different from typical JavaScript development, where APIs are explicitly
imported from specific JS modules or packages (though JS packages can forward APIs from other
modules as well).
