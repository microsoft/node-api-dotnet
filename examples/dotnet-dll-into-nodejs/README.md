# DotNet DLL Loader into Node.js and Electron.js

This repository demonstrates how to load and use a .NET DLL in both Node.js and Electron.js applications. It includes sample code for each environment, making it easy for developers to understand and implement the functionality.

## Table of Contents

- [Project Structure](#project-structure)
- [Prerequisites](#prerequisites)
- [Usage](#usage)
  - [Node.js Example](#nodejs-example)
  - [Electron.js Example](#electronjs-example)
- [Building the .NET Library](#building-the-net-library)

## Project Structure

```sh
.
├── ElectronJS
│   ├── README.md           # Electron specific instructions
│   ├── index.html          # HTML file for the Electron app
│   ├── main.js             # Main process for Electron
│   ├── package-lock.json    # npm package lock file
│   └── package.json        # npm dependencies for Electron
├── LICENSE                 # License information
├── MyDotNetLibrary         # .NET library project
│   ├── Class1.cs           # Example .NET class
│   ├── MyDotNetLibrary.csproj  # Project file for .NET library
│   ├── bin                 # Compiled .NET binaries
│   └── obj                 # Intermediate build files
├── NodeJS                  # Node.js example
│   ├── README.md           # Node.js specific instructions
│   ├── index.js            # Main entry point for Node.js
│   ├── package-lock.json    # npm package lock file
│   └── package.json        # npm dependencies for Node.js
└── dotnet-dll-into-nodejs.sln  # Solution file for the project
```

## Prerequisites

To run the examples, you need the following installed on your machine:

- [.NET SDK (version 8.0 or higher)](https://dotnet.microsoft.com/download)
- [Node.js (version 14 or higher)](https://nodejs.org/)
- [Electron](https://www.electronjs.org/docs/latest/tutorial/quick-start)

## Usage

### Node.js Example

1. Navigate to the `NodeJS` directory:

   ```bash
   cd NodeJS
   ```

2. Install dependencies:

   ```bash
   npm install
   ```

3. Run the Node.js application:

   ```bash
   node index.js
   ```

The `index.js` file demonstrates how to load the .NET DLL and call methods from it.

### Electron.js Example

1. Navigate to the `ElectronJS` directory:

   ```bash
   cd ElectronJS
   ```

2. Install dependencies:

   ```bash
   npm install
   ```

3. Run the Electron application:

   ```bash
   npm start
   ```

The Electron app will load the .NET DLL and allow you to interact with it through a user interface defined in `index.html`.

## Building the .NET Library

To build the .NET library, navigate to the `MyDotNetLibrary` directory and use the following command:

```bash
cd MyDotNetLibrary
dotnet build
```

This will compile the .NET library and place the output DLL in the `bin/Debug/net8.0` directory.
