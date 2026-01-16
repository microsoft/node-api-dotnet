const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('path');
const dotnet = require('node-api-dotnet/net8.0'); // Adjust according to your target framework

let mainWindow;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    webPreferences: {
      contextIsolation: true, // Protect from context isolation
      enableRemoteModule: false,
      preload: path.join(__dirname, 'preload.js'), // Preload script
    },
  });

  mainWindow.loadFile('index.html');

  // Open the DevTools (optional)
  // mainWindow.webContents.openDevTools();
}

// IPC to load the .dll and call a method
ipcMain.handle('call-dotnet', async (event, methodName, ...args) => {
  try {
    // Load the .dll
    dotnet.load(path.join(__dirname, '../MyDotNetLibrary/bin/Debug/net8.0/MyDotNetLibrary.dll'));

    // Access the class and method
    const MyMathClass = dotnet.MyDotNetLibrary.MyMathClass;
    const instance = new MyMathClass();

    // Call the method dynamically
    const result = await instance[methodName](...args);
    return result;
  } catch (error) {
    console.error('Error calling .NET method:', error);
    throw error;
  }
});

app.whenReady().then(createWindow);

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    createWindow();
  }
});
