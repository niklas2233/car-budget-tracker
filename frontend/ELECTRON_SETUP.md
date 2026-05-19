# Electron Setup for CarBudget

This frontend can now run as both a web app and a Windows desktop app using Electron.

## Installation

First, install the new dependencies:

```bash
cd frontend
npm install
```

## Running the Application

### Web Version (React Development)
```bash
npm start
```
Runs on `http://localhost:2233`

### Desktop Version (Electron Development)
```bash
npm run electron-dev
```
This will:
- Start the React dev server on port 2233
- Launch the Electron app that connects to it
- Show DevTools for debugging

### Building for Windows Desktop

Build as a portable executable:
```bash
npm run electron-build-win
```

This creates:
- `dist/CarBudget Setup.exe` - Installer
- `dist/CarBudget.exe` - Portable executable

## What Changed

1. **package.json** - Added Electron dependencies and build scripts
2. **public/electron.js** - Main Electron process (entry point)
3. **public/preload.js** - Security context bridge

## Configuration

- Default window size: 1200x800
- Minimum size: 800x600
- Dev tools auto-open in development
- Built-in menu (File, Edit, View)

## Architecture

- **Backend**: .NET API (unchanged) - runs on localhost:5000
- **React Frontend**: Same code for web and desktop
- **Electron**: Wraps React build or connects to dev server

The API proxy is still set to `http://localhost:5000` in package.json, so make sure your .NET backend is running.

## Notes

- The app will connect to your .NET API at `http://localhost:5000`
- In development, press `Ctrl+Shift+I` to toggle DevTools
- The built Windows app is self-contained and can be distributed
