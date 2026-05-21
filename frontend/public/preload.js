const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('api', {
  setCloseToTray: (enabled) => ipcRenderer.send('set-close-to-tray', enabled),
});
