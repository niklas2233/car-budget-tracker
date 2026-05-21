const { app, BrowserWindow, Menu, Tray, nativeImage, shell, ipcMain } = require('electron');
const { spawn } = require('child_process');
const http = require('http');
const fs = require('fs');
const path = require('path');
const isDev = !app.isPackaged;

// Set app name before any getPath() calls so userData lands in Roaming\CarBudget, not Roaming\frontend.
app.setName('CarBudget');

const processStartedAt = Date.now();

function getLogPath() {
  try {
    return path.join(app.getPath('userData'), 'electron.log');
  } catch {
    return path.join(process.env.TEMP || '.', 'carbudget-electron.log');
  }
}

function log(message) {
  const elapsed = Date.now() - processStartedAt;
  const ts = new Date().toISOString();
  try {
    fs.appendFileSync(getLogPath(), `[${ts}] +${elapsed}ms ${message}\n`);
  } catch {}
}

// --- Settings (closeToTray) stored in a JSON file so the value is available
//     synchronously before any window events fire, eliminating the race condition
//     that caused close-to-tray to silently fail.

function getSettingsPath() {
  return path.join(app.getPath('userData'), 'electron-settings.json');
}

function readSettings() {
  try {
    return JSON.parse(fs.readFileSync(getSettingsPath(), 'utf8'));
  } catch {
    return {};
  }
}

function writeSettings(settings) {
  try {
    fs.mkdirSync(path.dirname(getSettingsPath()), { recursive: true });
    fs.writeFileSync(getSettingsPath(), JSON.stringify(settings, null, 2), 'utf8');
  } catch {}
}

log('=== Process start ===');

// Single-instance lock — if another instance is already running, focus it and exit.
const gotLock = app.requestSingleInstanceLock();
log(`requestSingleInstanceLock → gotLock=${gotLock}`);
if (!gotLock) {
  log('Second instance detected — quitting');
  app.quit();
}

let mainWindow;
let tray;
let isQuitting = false;
let closeToTray = readSettings().closeToTray === true;
let backendProcess = null;
let resolvedDataDir = null;
const backendPort = 2233;

log(`closeToTray from settings: ${closeToTray}`);

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
          log(`Backend ready — HTTP ${res.statusCode}`);
          resolve();
          return;
        }

        if (Date.now() - startedAt > timeoutMs) {
          reject(new Error('Backend did not become ready in time.'));
          return;
        }

        setTimeout(tryConnect, 500);
      });

      req.on('error', (err) => {
        if (Date.now() - startedAt > timeoutMs) {
          log(`Backend ready timeout — ${err.message}`);
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
  log(`startBackendIfNeeded — exe: ${backendExe}`);

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

  log('Spawning backend process');
  backendProcess = spawn(backendExe, [], {
    cwd: path.dirname(backendExe),
    env: backendEnv,
    windowsHide: true,
    stdio: 'ignore',
  });

  backendProcess.on('exit', (code) => {
    log(`Backend process exited — code=${code}`);
    backendProcess = null;
  });

  log('Waiting for backend to become ready');
  return waitForBackendReady();
}

function resolvePreferredDataDirectory() {
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
  log(`showWindow — mainWindow=${!!mainWindow}`);
  if (!mainWindow) {
    return;
  }

  if (mainWindow.isMinimized()) {
    mainWindow.restore();
  }

  mainWindow.show();
  mainWindow.focus();
}

function buildTrayMenu() {
  return Menu.buildFromTemplate([
    {
      label: 'Open CarBudget',
      click: () => showWindow(),
    },
    { type: 'separator' },
    {
      label: 'Open data folder',
      click: () => {
        const dir = resolvedDataDir || app.getPath('userData');
        shell.openPath(dir);
      },
    },
    { type: 'separator' },
    {
      label: 'Quit',
      click: () => {
        isQuitting = true;
        app.quit();
      },
    },
  ]);
}

function createTray() {
  const iconPath = path.join(__dirname, 'favicon.ico');
  const trayIcon = nativeImage.createFromPath(iconPath);
  tray = new Tray(trayIcon);
  tray.setToolTip('CarBudget');
  tray.setContextMenu(buildTrayMenu());
  tray.on('double-click', () => showWindow());
}

function createWindow(initialUrl) {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    minWidth: 800,
    minHeight: 600,
    show: false,
    backgroundColor: '#0f172a',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
    },
    icon: path.join(__dirname, 'favicon.ico'),
  });

  const url = initialUrl || `http://127.0.0.1:${backendPort}`;
  mainWindow.loadURL(url);

  mainWindow.once('ready-to-show', () => {
    log('ready-to-show — showing window');
    mainWindow.show();
  });

  mainWindow.webContents.on('did-finish-load', () => {
    const currentUrl = mainWindow ? mainWindow.webContents.getURL() : '?';
    log(`did-finish-load — ${currentUrl}`);
  });

  mainWindow.on('close', (event) => {
    log(`window close event — isQuitting=${isQuitting} closeToTray=${closeToTray}`);
    if (!isQuitting && closeToTray) {
      event.preventDefault();
      mainWindow.hide();
      log('Window hidden to tray');
    }
  });

  if (isDev) {
    mainWindow.webContents.openDevTools();
  }

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

// When a second instance tries to launch, focus the existing window instead.
app.on('second-instance', () => {
  log('second-instance event received');
  showWindow();
});

app.on('ready', async () => {
  if (!gotLock) {
    // app.quit() was already called but ready still fires — do nothing.
    log('app ready (second instance — ignoring)');
    return;
  }
  log('app ready');
  createTray();

  if (isDev) {
    createWindow(null);
    return;
  }

  log('Creating window with loading screen');
  const loadingHtml = `data:text/html,<!DOCTYPE html><html><head><meta charset="utf-8"><style>*{margin:0;padding:0;box-sizing:border-box}body{background:#0f172a;display:flex;flex-direction:column;align-items:center;justify-content:center;height:100vh;font-family:system-ui,sans-serif;color:#94a3b8;gap:16px}.spinner{width:40px;height:40px;border:3px solid #1e293b;border-top-color:#3b82f6;border-radius:50%;animation:spin 0.8s linear infinite}@keyframes spin{to{transform:rotate(360deg)}}p{font-size:14px;letter-spacing:0.05em}</style></head><body><div class="spinner"></div><p>Starting CarBudget...</p></body></html>`;
  createWindow(loadingHtml);

  try {
    await startBackendIfNeeded();
  } catch (error) {
    log(`startBackendIfNeeded failed — ${error}`);
    console.error(error);
    app.quit();
    return;
  }

  log('Backend ready — navigating to app');
  if (mainWindow) {
    mainWindow.loadURL(`http://127.0.0.1:${backendPort}`);
  }
});

app.on('before-quit', () => {
  log('before-quit');
  isQuitting = true;
  stopBackendIfRunning();
});

app.on('window-all-closed', () => {
  // Always quit if the quit was already initiated (e.g. from the tray menu).
  // Only stay alive when close-to-tray is enabled and this is a normal window close.
  if (process.platform !== 'darwin' && (!closeToTray || isQuitting)) {
    app.quit();
  }
});

app.on('activate', () => {
  if (mainWindow === null) {
    createWindow(null);
  }
});

app.on('quit', () => {
  stopBackendIfRunning();
});

ipcMain.on('set-close-to-tray', (event, value) => {
  closeToTray = !!value;
  const s = readSettings();
  s.closeToTray = closeToTray;
  writeSettings(s);
  log(`set-close-to-tray → ${closeToTray}`);
});

Menu.setApplicationMenu(null);
