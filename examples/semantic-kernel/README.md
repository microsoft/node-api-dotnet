
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
To generate type definitions for the example JavaScript code, run the following command:
```
npm exec node-api-dotnet-generator -- -t Microsoft.SemanticKernel.d.ts -a pkg/microsoft.semantickernel/0.8.48.1-preview/lib/netstandard2.1/Microsoft.SemanticKernel.dll -r pkg/microsoft.extensions.logging.abstractions/7.0.0/lib/netstandard2.0/Microsoft.Extensions.Logging.Abstractions.dll
```
(Ignore the warnings about unsupported types. Those will be addressed in the future.)
