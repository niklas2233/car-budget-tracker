const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('api', {
  getElectronSettings: () => ipcRenderer.invoke('get-electron-settings'),
  getLocalAddress: () => ipcRenderer.invoke('get-local-address'),
  refocusWindow: () => ipcRenderer.send('refocus-window'),
  setCloseToTray: (enabled) => ipcRenderer.send('set-close-to-tray', enabled),
  setStartInTray: (enabled) => ipcRenderer.send('set-start-in-tray', enabled),
  onUpdateAvailable: (callback) => ipcRenderer.on('update-available', (_, info) => callback(info)),
  setBackendPort: (port) => ipcRenderer.send('set-backend-port', port),
  downloadAndInstallUpdate: (downloadUrl) => ipcRenderer.send('download-and-install-update', downloadUrl),
  onDownloadProgress: (callback) => ipcRenderer.on('download-progress', (_, percent) => callback(percent)),
  onDownloadError: (callback) => ipcRenderer.on('download-error', (_, message) => callback(message)),
  openExternal: (url) => ipcRenderer.send('open-external', url),
});
