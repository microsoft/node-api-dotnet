# ElectronJS

A sample Electron application that demonstrates loading a .NET `.dll` file and interacting with it using the `node-api-dotnet` package. This example allows you to invoke methods from your .NET assembly directly within your Electron app.

## Project Structure

```
ElectronJS
│   ├── main.js                # Main process file
│   ├── renderer.js            # Renderer process file
│   ├── index.html             # Main HTML file
│   ├── MyDotNetLibrary.dll    # .NET DLL file to interact with
│   └── package.json           # Project configuration file
```

## Prerequisites

1. **Node.js** - Install [Node.js](https://nodejs.org/) (version 14 or higher recommended).
2. **Electron** - Installed via npm.
3. **.NET SDK** - Required to build `.dll` files. Install from [here](https://dotnet.microsoft.com/download).
4. **node-api-dotnet** - Used to load .NET assemblies into Node.js.

## Getting Started

1. **Clone the Repository**:

    ```bash
    git clone <repository_url>
    cd ElectronJS
    ```

2. **Add Your DLL File**:

   Make sure your `.NET` library, `MyDotNetLibrary.dll`, is in the project root directory (`MyElectronApp/`).

3. **Install Dependencies**:

    ```bash
    npm install
    ```

## Project Files

### `main.js`

Handles the main process of Electron, loads the `.dll` file, and sets up IPC communication with the renderer process to handle `.NET` method calls.

### `renderer.js`

Handles interactions from the HTML UI, sends data to the main process, and displays results.

### `index.html`

A simple interface to take input, invoke the .NET function, and display the result.

## Usage

1. **Start the Application**:

    ```bash
    npm start
    ```

2. **Using the App**:

   - Enter two numbers in the input fields.
   - Click **"Add Numbers"**.
   - The result of the addition (calculated by the .NET method) will appear below the button.

## Sample Code

### Loading the .NET Assembly

In `main.js`, the `.dll` file is loaded using `node-api-dotnet`:

```javascript
const dotnet = require('node-api-dotnet/net6.0');
dotnet.load(path.join(__dirname, 'MyDotNetLibrary.dll'));
```

### Calling a .NET Method from the Renderer

The `renderer.js` file communicates with the main process to call a `.NET` method:

```javascript
const result = await ipcRenderer.invoke('dotnet-add', a, b);
```

## Troubleshooting

- **Electron Version**: Ensure compatibility between `node-api-dotnet` and your Electron version.
- **Path Issues**: Double-check the path to `MyDotNetLibrary.dll`.
- **DLL Compatibility**: Ensure that the `.dll` was built for the same .NET runtime as specified (e.g., `net6.0`).
