
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
| `dotnet build`                   | Install `SemanticKernel` nuget packages into the project and generate type definitions.
| `npm install`                    | Install `node-api-dotnet` npm package into the project.
| `node example.js`                | Run example JS code that uses the above packages to call the Azure OpenAI service.
