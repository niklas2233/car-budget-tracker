import React, { useState } from 'react';
import { appConfigApi, getApiErrorMessage } from '../api';
import { AppSetupStatusDto } from '../types';
import './SetupPage.css';

type SetupPageProps = {
  setupStatus: AppSetupStatusDto;
  onSaved: () => void;
};

const SetupPage: React.FC<SetupPageProps> = ({ setupStatus, onSaved }) => {
  const [region, setRegion] = useState(setupStatus.currentRegion || 'sweden');
  const [debugSavePlaywrightHtml, setDebugSavePlaywrightHtml] = useState(setupStatus.debugSavePlaywrightHtml || false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const isInitialSetup = setupStatus.setupRequired;

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError('');

    try {
      await appConfigApi.saveConfig({
        region,
        debugSavePlaywrightHtml,
      });

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
            <select value={region} onChange={(e) => setRegion(e.target.value)}>
              <option value="sweden">Sweden</option>
              <option value="norway">Norway</option>
              <option value="europe">Europe</option>
            </select>
          </label>

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

        {error && <p className="setup-error">{error}</p>}
      </div>
    </section>
  );
};

export default SetupPage;
