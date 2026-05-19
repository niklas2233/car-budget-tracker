const { app, BrowserWindow, Menu, Tray, nativeImage, shell } = require('electron');
const { spawn } = require('child_process');
const http = require('http');
const fs = require('fs');
const path = require('path');
const isDev = !app.isPackaged;

let mainWindow;
let tray;
let isQuitting = false;
let backendProcess = null;
let resolvedDataDir = null;
const backendPort = 2233;
const trayMode = process.argv.includes('--tray');

function getBackendExePath() {
  const exeName = process.platform === 'win32' ? 'CarBudget.Api.exe' : 'CarBudget.Api';

  if (isDev) {
    return path.join(__dirname, '../../src/CarBudget.Api/bin/Debug/net10.0', exeName);
  }

  return path.join(process.resourcesPath, 'backend', exeName);
}

function waitForBackendReady(timeoutMs = 30000) {
  const startedAt = Date.now();

  return new Promise((resolve, reject) => {
    const tryConnect = () => {
      const req = http.get(`http://127.0.0.1:${backendPort}/openapi/v1.json`, (res) => {
        res.resume();
        if (res.statusCode && res.statusCode < 500) {
          resolve();
          return;
        }

        if (Date.now() - startedAt > timeoutMs) {
          reject(new Error('Backend did not become ready in time.'));
          return;
        }

        setTimeout(tryConnect, 500);
      });

      req.on('error', () => {
        if (Date.now() - startedAt > timeoutMs) {
          reject(new Error('Backend did not become ready in time.'));
          return;
        }

        setTimeout(tryConnect, 500);
      });

      req.setTimeout(3000, () => {
        req.destroy();
      });
    };

    tryConnect();
  });
}

function startBackendIfNeeded() {
  if (isDev) {
    return Promise.resolve();
  }

  const backendExe = getBackendExePath();
  if (!fs.existsSync(backendExe)) {
    return Promise.reject(new Error(`Backend executable not found at ${backendExe}`));
  }

  const dataDir = resolvePreferredDataDirectory();
  resolvedDataDir = dataDir;
  fs.mkdirSync(dataDir, { recursive: true });

  const backendEnv = {
    ...process.env,
    webui_port: String(backendPort),
    ASPNETCORE_ENVIRONMENT: 'Production',
    CARBUDGET_DATA_DIR: dataDir,
  };

  backendProcess = spawn(backendExe, [], {
    cwd: path.dirname(backendExe),
    env: backendEnv,
    windowsHide: true,
    stdio: 'ignore',
  });

  backendProcess.on('exit', () => {
    backendProcess = null;
  });

  return waitForBackendReady();
}

function resolvePreferredDataDirectory() {
  // For the portable exe, electron-builder sets PORTABLE_EXECUTABLE_DIR to the
  // directory containing the original portable exe — use that so data lives next to it.
  const portableExecutableDir = process.env.PORTABLE_EXECUTABLE_DIR;
  if (portableExecutableDir && portableExecutableDir.trim().length > 0) {
    const dir = path.join(portableExecutableDir.trim(), 'CarBudgetData');
    try {
      fs.mkdirSync(dir, { recursive: true });
      const probePath = path.join(dir, '.carbudget-write-test');
      fs.writeFileSync(probePath, 'ok');
      fs.unlinkSync(probePath);
      return dir;
    } catch {
    }
  }

  // Installed build or PORTABLE_EXECUTABLE_DIR unavailable/unwritable:
  // fall back to the standard per-user app-data directory.
  // NOTE: process.execPath is intentionally NOT used here — for portable exes it
  // points to the temporary extraction directory, not the original exe folder.
  return app.getPath('userData');
}

function stopBackendIfRunning() {
  if (!backendProcess) {
    return;
  }

  try {
    backendProcess.kill();
  } catch {
  }

  backendProcess = null;
}

function showWindow() {
  if (!mainWindow) {
    createWindow();
    return;
  }

  if (!mainWindow.isVisible()) {
    mainWindow.show();
  }

  if (mainWindow.isMinimized()) {
    mainWindow.restore();
  }

  mainWindow.focus();
}

function createTray() {
  const iconPath = path.join(__dirname, 'favicon.ico');
  const trayIcon = nativeImage.createFromPath(iconPath);
  tray = new Tray(trayIcon);
  tray.setToolTip('CarBudget');

  const contextMenu = Menu.buildFromTemplate([
    {
      label: 'Open CarBudget',
      click: () => {
        showWindow();
      },
    },
    {
      label: 'Hide Window',
      click: () => {
        if (mainWindow) {
          mainWindow.hide();
        }
      },
    },
    { type: 'separator' },
    {
      label: 'Open data folder',
      click: () => {
        const dir = resolvedDataDir || app.getPath('userData');
        shell.openPath(dir);
      },
    },
    {
      label: 'Quit',
      click: () => {
        isQuitting = true;
        app.quit();
      },
    },
  ]);

  tray.setContextMenu(contextMenu);
  tray.on('double-click', () => {
    showWindow();
  });
}

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    minWidth: 800,
    minHeight: 600,
    show: !trayMode,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
    },
    icon: path.join(__dirname, 'favicon.ico'),
  });

  const startUrl = `http://127.0.0.1:${backendPort}`;

  mainWindow.loadURL(startUrl);

  if (!trayMode) {
    mainWindow.show();
  }

  if (isDev) {
    mainWindow.webContents.openDevTools();
  }

  mainWindow.on('minimize', (event) => {
    event.preventDefault();
    mainWindow.hide();
  });

  mainWindow.on('close', (event) => {
    if (trayMode && !isQuitting) {
      event.preventDefault();
      mainWindow.hide();
    }
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

app.on('ready', async () => {
  try {
    await startBackendIfNeeded();
  } catch (error) {
    console.error(error);
    app.quit();
    return;
  }

  createTray();
  createWindow();
});

app.on('before-quit', () => {
  isQuitting = true;
  stopBackendIfRunning();
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin' && !trayMode) {
    app.quit();
  }
});

app.on('activate', () => {
  if (mainWindow === null) {
    createWindow();
  }
});

app.on('quit', () => {
  stopBackendIfRunning();
});

Menu.setApplicationMenu(null);
