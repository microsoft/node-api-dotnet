### Steps to Build a .DLL on macOS Using the .NET CLI

1. **Install the .NET SDK for macOS**:
   - Download and install the [.NET SDK](https://dotnet.microsoft.com/download) for macOS.
   - After installation, you can verify it by running:

     ```bash
     dotnet --version
     ```

2. **Create a New Class Library Project**:
   - Open the Terminal, navigate to your desired directory, and create a new class library project by running:

     ```bash
     dotnet new classlib -o MyDotNetLibrary
     ```

   - This creates a folder named `MyDotNetLibrary` with a basic class library template.

3. **Edit the Code**:
   - Navigate to the project folder and open the generated `Class1.cs` file with your preferred code editor (VS Code, Sublime, etc.):

     ```bash
     cd MyDotNetLibrary
     ```

   - Replace the content with your code:

     ```csharp
     using System;
     using NodeApi.DotNet;

     namespace MyDotNetLibrary
     {
         public class MyMathClass
         {
             public int Add(int a, int b) => a + b;

             [JSExport]
             public static string SayHello(string name) => $"Hello, {name} from .NET!";
         }
     }
     ```

4. **Build the DLL**:
   - Build the project to produce the `.dll` file:

     ```bash
     dotnet build
     ```

   - This generates a `.dll` file in the `bin/Debug/net6.0/` directory by default.

5. **Locate the DLL File**:
   - You can find `MyDotNetLibrary.dll` in the `bin/Debug/net6.0/` directory within the project folder.

6. **Use the DLL in Node.js**:
   - With the `.dll` file ready, you can now load and interact with it in a Node.js application using the `node-api-dotnet` package.

### Example: Loading the DLL in Node.js on macOS

```javascript
import dotnet from 'node-api-dotnet/net6.0';

dotnet.load('path/to/MyDotNetLibrary.dll');

const MyMathClass = dotnet.MyDotNetLibrary.MyMathClass;
const instance = new MyMathClass();
console.log(instance.Add(3, 5)); // Example usage
```