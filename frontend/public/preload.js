const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('api', {
  setCloseToTray: (enabled) => ipcRenderer.send('set-close-to-tray', enabled),
  onUpdateAvailable: (callback) => ipcRenderer.on('update-available', (_, info) => callback(info)),
  downloadAndInstallUpdate: (downloadUrl) => ipcRenderer.send('download-and-install-update', downloadUrl),
  onDownloadProgress: (callback) => ipcRenderer.on('download-progress', (_, percent) => callback(percent)),
  onDownloadError: (callback) => ipcRenderer.on('download-error', (_, message) => callback(message)),
  openExternal: (url) => ipcRenderer.send('open-external', url),
});
