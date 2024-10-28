const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('path');
const dotnet = require('node-api-dotnet/net8.0');

// Load the .NET assembly
dotnet.load(path.join(__dirname, '../MyDotNetLibrary/bin/Debug/net8.0/MyDotNetLibrary.dll'));

function createWindow() {
  const mainWindow = new BrowserWindow({
    width: 800,
    height: 600,
    webPreferences: {
      preload: path.join(__dirname, 'renderer.js'), // Renderer script
      contextIsolation: false,
      nodeIntegration: true
    }
  });

  mainWindow.loadFile('index.html');
}

// Handle a call from renderer to execute .NET code
ipcMain.handle('dotnet-add', async (event, a, b) => {
  const MyMathClass = dotnet.MyDotNetLibrary.MyMathClass;
  const instance = new MyMathClass();
  return instance.Add(a, b);
});

app.whenReady().then(() => {
  createWindow();
  app.on('activate', function () {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', function () {
  if (process.platform !== 'darwin') app.quit();
});
