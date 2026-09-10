const { ipcRenderer } = require('electron');

document.getElementById('addButton').addEventListener('click', async () => {
  const a = parseInt(document.getElementById('inputA').value);
  const b = parseInt(document.getElementById('inputB').value);

  // Call .NET Add method via main process
  const result = await ipcRenderer.invoke('dotnet-add', a, b);
  document.getElementById('result').innerText = `Result: ${result}`;
});
