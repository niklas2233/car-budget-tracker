import React, { useState, useEffect } from 'react';
import { appConfigApi, getApiErrorMessage } from '../api';
import { AppSetupStatusDto } from '../types';
import './SetupPage.css';

type SetupPageProps = {
  setupStatus: AppSetupStatusDto;
  onSaved: () => void;
};

const WORLDWIDE_CURRENCIES = new Set(['AED','AUD','BRL','CAD','CHF','CNY','DKK','EUR','GBP','INR','JPY','MXN','NOK','NZD','SEK','SGD','USD','ZAR']);

const SetupPage: React.FC<SetupPageProps> = ({ setupStatus, onSaved }) => {
  const [region, setRegion] = useState(setupStatus.currentRegion || 'sweden');
  const initialCurrency = setupStatus.currentCurrency || '';
  const initialRegion = setupStatus.currentRegion || 'sweden';
  const isCustom = initialRegion === 'worldwide' && !WORLDWIDE_CURRENCIES.has(initialCurrency);
  const [currency, setCurrency] = useState(isCustom ? 'custom' : initialCurrency);
  const [customCurrencyText, setCustomCurrencyText] = useState(isCustom ? initialCurrency : '');
  const [distanceUnit, setDistanceUnit] = useState(setupStatus.currentDistanceUnit || 'km');
  const [port, setPort] = useState(String(setupStatus.currentPort || 2233));
  const [debugSavePlaywrightHtml, setDebugSavePlaywrightHtml] = useState(setupStatus.debugSavePlaywrightHtml || false);
  const [closeToTray, setCloseToTray] = useState(false);
  const [startInTray, setStartInTray] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [restartRequired, setRestartRequired] = useState(false);

  const isElectron = !!(window as any).api?.setCloseToTray;

  useEffect(() => {
    if ((window as any).api?.getElectronSettings) {
      (window as any).api.getElectronSettings().then((s: { closeToTray: boolean; startInTray: boolean }) => {
        setCloseToTray(!!s.closeToTray);
        setStartInTray(!!s.startInTray);
      });
    }
  }, []);

  const handleCloseToTrayChange = (checked: boolean) => {
    setCloseToTray(checked);
    (window as any).api?.setCloseToTray(checked);
  };

  const handleStartInTrayChange = (checked: boolean) => {
    setStartInTray(checked);
    (window as any).api?.setStartInTray(checked);
  };

  const isInitialSetup = setupStatus.setupRequired;

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError('');

    try {
      const parsedPort = parseInt(port, 10);
      const validPort = !isNaN(parsedPort) && parsedPort >= 1024 && parsedPort <= 65535 ? parsedPort : undefined;

      await appConfigApi.saveConfig({
        region,
        currency: currency === 'custom' ? (customCurrencyText.trim().toUpperCase() || undefined) : (currency || undefined),
        distanceUnit: region === 'worldwide' ? distanceUnit : undefined,
        port: validPort,
        debugSavePlaywrightHtml,
      });

      if (validPort && validPort !== setupStatus.currentPort) {
        (window as any).api?.setBackendPort(validPort);
        setRestartRequired(true);
        setSaving(false);
        return;
      }

      onSaved();
    } catch (submitError) {
      setError(getApiErrorMessage(submitError, 'Could not save configuration.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="setup-page">
      <div className="setup-card">
        <h2>{isInitialSetup ? 'Initial Setup' : 'Settings'}</h2>
        {isInitialSetup && (
          <p className="setup-subtitle">
            Configure CarBudget once. The same config file can be reused in Docker and EXE setups.
          </p>
        )}

        <div className="setup-paths">
          <p><strong>Config file:</strong> {setupStatus.configFilePath}</p>
          <p><strong>Data folder:</strong> {setupStatus.dataDirectoryPath}</p>
        </div>

        <form onSubmit={handleSubmit} className="setup-form">
          <label>
            Region
            <select value={region} onChange={(e) => {
              const newRegion = e.target.value;
              setRegion(newRegion);
              if (newRegion === 'worldwide' && !WORLDWIDE_CURRENCIES.has(currency)) {
                setCurrency('custom');
              }
            }}>
              <option value="sweden">Sweden</option>
              <option value="norway">Norway</option>
              <option value="europe">Europe</option>
              <option value="america">America</option>
              <option value="usa">USA</option>
              <option value="gb">Great Britain</option>
              <option value="worldwide">Worldwide (custom units)</option>
            </select>
          </label>

          {region === 'worldwide' ? (
            <>
              <label>
                Currency
                <select value={currency} onChange={(e) => setCurrency(e.target.value)}>
                  <option value="custom">Custom</option>
                  <option value="AED">AED — UAE Dirham</option>
                  <option value="AUD">AUD — Australian Dollar</option>
                  <option value="BRL">BRL — Brazilian Real</option>
                  <option value="CAD">CAD — Canadian Dollar</option>
                  <option value="CHF">CHF — Swiss Franc</option>
                  <option value="CNY">CNY — Chinese Yuan</option>
                  <option value="DKK">DKK — Danish Krone</option>
                  <option value="EUR">EUR — Euro</option>
                  <option value="GBP">GBP — British Pound</option>
                  <option value="INR">INR — Indian Rupee</option>
                  <option value="JPY">JPY — Japanese Yen</option>
                  <option value="MXN">MXN — Mexican Peso</option>
                  <option value="NOK">NOK — Norwegian Krone</option>
                  <option value="NZD">NZD — New Zealand Dollar</option>
                  <option value="SEK">SEK — Swedish Krona</option>
                  <option value="SGD">SGD — Singapore Dollar</option>
                  <option value="USD">USD — US Dollar</option>
                  <option value="ZAR">ZAR — South African Rand</option>
                </select>
              </label>
              {currency === 'custom' && (
                <label>
                  Currency code
                  <input
                    type="text"
                    placeholder="e.g. THB, KRW, TRY"
                    value={customCurrencyText}
                    onChange={(e) => setCustomCurrencyText(e.target.value.toUpperCase())}
                    maxLength={10}
                  />
                </label>
              )}

              <label>
                Distance unit
                <select value={distanceUnit} onChange={(e) => setDistanceUnit(e.target.value)}>
                  <option value="km">Kilometres (km)</option>
                  <option value="miles">Miles</option>
                </select>
              </label>
            </>
          ) : (
            <label>
              Currency override
              <select value={currency} onChange={(e) => setCurrency(e.target.value)}>
                <option value="">Region default (SEK / NOK / EUR / USD)</option>
                <option value="SEK">SEK — Swedish Krona</option>
                <option value="NOK">NOK — Norwegian Krone</option>
                <option value="EUR">EUR — Euro</option>
                <option value="DKK">DKK — Danish Krone</option>
                <option value="GBP">GBP — British Pound</option>
                <option value="USD">USD — US Dollar</option>
                <option value="CHF">CHF — Swiss Franc</option>
              </select>
            </label>
          )}

          {!isInitialSetup && isElectron && !setupStatus.isContainer && (
            <fieldset className="setup-fieldset">
              <legend>Network</legend>
              <label>
                Backend port
                <input
                  type="number"
                  min={1024}
                  max={65535}
                  value={port}
                  onChange={(e) => setPort(e.target.value)}
                />
              </label>
              <p className="setup-hint">
                Port the backend listens on. Default is 2233. Requires a restart to take effect.
              </p>
            </fieldset>
          )}

          {isElectron && !setupStatus.isContainer && !isInitialSetup && (
            <fieldset className="setup-fieldset">
              <legend>Window</legend>
              <label className="setup-checkbox-label">
                <input
                  type="checkbox"
                  checked={closeToTray}
                  onChange={(e) => handleCloseToTrayChange(e.target.checked)}
                />
                Close to tray instead of quitting
              </label>
              {closeToTray && (
                <p className="setup-hint">
                  Closing the window will hide it to the system tray. Use the tray icon to reopen or quit.
                </p>
              )}
              <label className="setup-checkbox-label">
                <input
                  type="checkbox"
                  checked={startInTray}
                  onChange={(e) => handleStartInTrayChange(e.target.checked)}
                />
                Start minimized to tray
              </label>
              {startInTray && (
                <p className="setup-hint">
                  The app will start in the background. Click the tray icon to open it.
                </p>
              )}
            </fieldset>
          )}

          <fieldset className="setup-fieldset">
            <legend>Debugging</legend>
            <label className="setup-checkbox-label">
              <input
                type="checkbox"
                checked={debugSavePlaywrightHtml}
                onChange={(e) => setDebugSavePlaywrightHtml(e.target.checked)}
              />
              Save Playwright HTML to data folder on each lookup
            </label>
            {debugSavePlaywrightHtml && (
              <p className="setup-hint">
                Each successful plate lookup will save a <code>lookup_debug_PLATE_TIMESTAMP.html</code> file
                to your data folder. Useful for diagnosing parse issues. Disable when done to avoid filling disk.
              </p>
            )}
          </fieldset>

          <button type="submit" disabled={saving}>
            {saving ? 'Saving...' : 'Save Configuration'}
          </button>
        </form>

        {restartRequired && (
          <p className="setup-hint" style={{ marginTop: '12px', color: 'var(--text-secondary)' }}>
            Port saved. Please restart the app for the new port to take effect.
          </p>
        )}
        {error && <p className="setup-error">{error}</p>}
      </div>
    </section>
  );
};

export default SetupPage;
