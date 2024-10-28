const { contextBridge, ipcRenderer } = require('electron');

// Expose the callDotNet method to the renderer process
contextBridge.exposeInMainWorld('myAPI', {
  callDotNet: (methodName, ...args) => ipcRenderer.invoke('call-dotnet', methodName, ...args),
});
