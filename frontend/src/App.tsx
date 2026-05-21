import React, { useEffect, useState } from 'react';
import { BrowserRouter as Router, Routes, Route, Link, Navigate } from 'react-router-dom';
import Dashboard from './components/Dashboard';
import EconomyDashboard from './components/EconomyDashboard';
import VehicleForm from './components/VehicleForm';
import VehicleDetails from './components/VehicleDetails';
import ExpenseForm from './components/ExpenseForm';
import LookupCachePage from './components/LookupCachePage';
import SetupPage from './components/SetupPage';
import { appConfigApi, getApiErrorMessage } from './api';
import { AppSetupStatusDto } from './types';
import './App.css';

function App() {
  const [darkMode, setDarkMode] = useState<boolean>(() => {
    return localStorage.getItem('darkMode') === 'true';
  });
  const [setupStatus, setSetupStatus] = useState<AppSetupStatusDto | null>(null);
  const [setupLoading, setSetupLoading] = useState(true);
  const [setupError, setSetupError] = useState('');
  const [updateInfo, setUpdateInfo] = useState<{ version: string; url: string; downloadUrl: string | null; isPortable: boolean } | null>(null);
  const [downloadProgress, setDownloadProgress] = useState<number | null>(null);
  const [downloadError, setDownloadError] = useState('');

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', darkMode ? 'dark' : 'light');
    localStorage.setItem('darkMode', String(darkMode));
  }, [darkMode]);

  useEffect(() => {
    const api = (window as any).api;
    if (!api) return;
    api.onUpdateAvailable?.((info: { version: string; url: string; downloadUrl: string | null; isPortable: boolean }) => {
      setUpdateInfo(info);
    });
    api.onDownloadProgress?.((percent: number) => setDownloadProgress(percent));
    api.onDownloadError?.((msg: string) => { setDownloadError(msg); setDownloadProgress(null); });
  }, []);

  useEffect(() => {
    const loadSetupStatus = async () => {
      try {
        const response = await appConfigApi.getSetupStatus();
        setSetupStatus(response.data);
      } catch (error) {
        setSetupError(getApiErrorMessage(error, 'Could not load setup status.'));
      } finally {
        setSetupLoading(false);
      }
    };

    loadSetupStatus();
  }, []);

  if (setupError) {
    return <div className="App">{setupError}</div>;
  }

  if (setupLoading || !setupStatus) {
    return <div className="App">Loading configuration...</div>;
  }

  return (
    <Router>
      <div className="App">
        <header className="App-header">
          <div className="App-header-left">
            <Link to="/" className="App-home-link" aria-label="Go to dashboard">
              <h1>🚗 Car Budget Manager</h1>
              <p>Track your vehicle expenses like a pro</p>
            </Link>
            <nav className="App-nav" aria-label="Primary navigation">
              <Link to="/" className="App-nav-link">Dashboard</Link>
              <Link to="/economy" className="App-nav-link">Economy</Link>
              {!setupStatus.isContainer && (
                <Link to="/settings" className="App-nav-link">Settings</Link>
              )}
            </nav>
          </div>
          <button
            className="dark-mode-toggle"
            onClick={() => setDarkMode(d => !d)}
            aria-label="Toggle dark mode"
          >
            {darkMode ? '☀️ Light' : '🌙 Dark'}
          </button>
        </header>
        {updateInfo && (
          <div className="update-banner">
            {downloadProgress !== null ? (
              <>
                <span>Downloading update... {downloadProgress}%</span>
                <div className="update-progress-track">
                  <div className="update-progress-fill" style={{ width: `${downloadProgress}%` }} />
                </div>
              </>
            ) : downloadError ? (
              <>
                <span>Download failed: {downloadError}</span>
                <button className="update-banner-action" onClick={() => { setDownloadError(''); (window as any).api?.downloadAndInstallUpdate(updateInfo.downloadUrl); setDownloadProgress(0); }}>Retry</button>
                <button className="update-banner-dismiss" onClick={() => { setUpdateInfo(null); setDownloadError(''); }} aria-label="Dismiss">✕</button>
              </>
            ) : (
              <>
                <span>Version {updateInfo.version} is available.</span>
                {updateInfo.isPortable || !updateInfo.downloadUrl ? (
                  <button className="update-banner-action" onClick={() => (window as any).api?.openExternal(updateInfo.url)}>Download</button>
                ) : (
                  <button className="update-banner-action" onClick={() => { (window as any).api?.downloadAndInstallUpdate(updateInfo.downloadUrl); setDownloadProgress(0); }}>Update now</button>
                )}
                <button className="update-banner-dismiss" onClick={() => setUpdateInfo(null)} aria-label="Dismiss">✕</button>
              </>
            )}
          </div>
        )}
        <main>
          <Routes>
            <Route
              path="/setup"
              element={
                <SetupPage
                  setupStatus={setupStatus}
                  onSaved={() => {
                    window.location.replace('/');
                  }}
                />
              }
            />
            {setupStatus.setupRequired ? (
              <>
                <Route path="*" element={<Navigate to="/setup" replace />} />
              </>
            ) : (
              <>
                <Route path="/" element={<Dashboard />} />
                <Route path="/economy" element={<EconomyDashboard />} />
                <Route path="/vehicles/new" element={<VehicleForm />} />
                <Route path="/vehicles/:vehicleKey/edit" element={<VehicleForm />} />
                <Route path="/vehicles/:vehicleKey" element={<VehicleDetails />} />
                <Route path="/vehicles/:vehicleKey/expenses/new" element={<ExpenseForm />} />
                <Route path="/vehicles/:vehicleKey/expenses/:expenseId/edit" element={<ExpenseForm />} />
                <Route path="/cached-plates" element={<LookupCachePage />} />
                <Route path="/cached-plates/:licensePlate" element={<LookupCachePage />} />
                {!setupStatus.isContainer && (
                  <Route
                    path="/settings"
                    element={
                      <SetupPage
                        setupStatus={setupStatus}
                        onSaved={() => {
                          window.location.replace('/');
                        }}
                      />
                    }
                  />
                )}
                <Route path="*" element={<Navigate to="/" replace />} />
              </>
            )}
          </Routes>
        </main>
      </div>
    </Router>
  );
}

export default App;

