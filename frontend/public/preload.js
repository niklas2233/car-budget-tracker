const { contextBridge } = require('electron');

// Expose only what you need
contextBridge.exposeInMainWorld('api', {
  // Add any IPC channels here if needed
});
