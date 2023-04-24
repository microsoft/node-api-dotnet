
## Example: Using .NET Semantic Kernel to call Azure OpenAI
The `example.js` script dynamically loads the `Microsoft.SemanticKernel` .NET assembly and uses it
to call Azure OpenAI to summarize some text. It is a direct JavaScript translation of the C# example
code in the [Semantic Kernel](https://github.com/microsoft/semantic-kernel) project readme.

To run this example, first set the following environment variables referencing your
[Azure OpenAI deployment](https://learn.microsoft.com/en-us/azure/cognitive-services/openai/quickstart):
 - `OPENAI_ENDPOINT`
 - `OPENAI_DEPLOYMENT`
 - `OPENAI_KEY`

Then run the following commands in sequence:

| Command                          | Explanation
|----------------------------------|--------------------------------------------------
| `dotnet pack ../..`              | Build Node API .NET packages.
| `dotnet restore`                 | Install `SemanticKernel` nuget package into the project.
| `npm install`                    | Install `node-api-dotnet` npm package into the project.
| `node example.js`                | Run example JS code that uses the above packages to call the Azure OpenAI service.

#### Type Definitions (Optional)
To generate type definitions for the example JavaScript code, run the following commands:
```
npm exec node-api-dotnet-generator -- -t Microsoft.SemanticKernel.Core.d.ts -a ./pkg/microsoft.semantickernel.core/0.12.207.1-preview/lib/netstandard2.0/Microsoft.SemanticKernel.Core.dll -r ./pkg/microsoft.semantickernel.abstractions/0.12.207.1-preview/lib/netstandard2.0/Microsoft.SemanticKernel.Abstractions.dll -r ./pkg/microsoft.extensions.logging.abstractions/6.0.0/lib/netstandard2.0/Microsoft.Extensions.Logging.Abstractions.dll -r ./pkg/microsoft.bcl.asyncinterfaces/6.0.0/lib/netstandard2.0/Microsoft.Bcl.AsyncInterfaces.dll

npm exec node-api-dotnet-generator -- -t Microsoft.SemanticKernel.Abstractions.d.ts -a .\pkg\microsoft.semantickernel.abstractions\0.12.207.1-preview\lib\netstandard2.0\Microsoft.SemanticKernel.Abstractions.dll -r .\pkg\microsoft.extensions.logging.abstractions\6.0.0\lib\netstandard2.0\Microsoft.Extensions.Logging.Abstractions.dll -r .\pkg\microsoft.bcl.asyncinterfaces\6.0.0\lib\netstandard2.0\Microsoft.Bcl.AsyncInterfaces.dll

npm exec node-api-dotnet-generator -- -t Microsoft.SemanticKernel.Connectors.AI.OpenAI.d.ts -a ./pkg/microsoft.semantickernel.connectors.ai.openai/0.12.207.1-preview/lib/netstandard2.0/Microsoft.SemanticKernel.Connectors.AI.OpenAI.dll -r ./pkg/microsoft.semantickernel.abstractions/0.12.207.1-preview/lib/netstandard2.0/Microsoft.SemanticKernel.Abstractions.dll -r ./pkg/azure.core/1.30.0/lib/netstandard2.0/Azure.Core.dll -r ./pkg/microsoft.extensions.logging.abstractions/6.0.0/lib/netstandard2.0/Microsoft.Extensions.Logging.Abstractions.dll
```
MSBuild scripts will eventually automate these complex commands.

(Ignore the warnings about unsupported types. Those will also be addressed in the future.)
