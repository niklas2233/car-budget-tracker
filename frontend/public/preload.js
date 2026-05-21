const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('api', {
  setCloseToTray: (enabled) => ipcRenderer.send('set-close-to-tray', enabled),
  onUpdateAvailable: (callback) => ipcRenderer.on('update-available', (_, info) => callback(info)),
});
