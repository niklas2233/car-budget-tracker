import React, { useEffect, useMemo, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { vehicleApi } from '../api';
import { VehicleLookupCacheDto } from '../types';
import { formatDistance } from '../currency';
import './LookupCachePage.css';

const formatDateTime = (value?: string) => {
  if (!value) {
    return 'Unknown';
  }

  return new Intl.DateTimeFormat('sv-SE', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
};

const buildCarInfoSpecsUrl = (licensePlate: string) => {
  return `https://www.car.info/sv-se/license-plate/S/${encodeURIComponent(licensePlate)}#specs`;
};

const normalizePlate = (plate: string) => plate.toUpperCase().replace(/[^A-Z0-9]/g, '');

const LookupCachePage: React.FC = () => {
  const { licensePlate } = useParams<{ licensePlate?: string }>();
  const [entries, setEntries] = useState<VehicleLookupCacheDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [recacheLoading, setRecacheLoading] = useState(false);
  const [recacheStatus, setRecacheStatus] = useState<string | null>(null);
  const normalizedRoutePlate = licensePlate ? normalizePlate(decodeURIComponent(licensePlate)) : null;
  const isSinglePlateView = Boolean(normalizedRoutePlate);

  const loadCache = async () => {
    const response = await vehicleApi.getLookupCache();
    setEntries(response.data);
  };

  useEffect(() => {
    const loadInitialCache = async () => {
      try {
        await loadCache();
      } catch (error) {
        console.error('Error loading lookup cache:', error);
        alert('Failed to load cached Car.info data.');
      } finally {
        setLoading(false);
      }
    };

    loadInitialCache();
  }, []);

  const filteredEntries = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase();

    let result = entries;

    if (normalizedRoutePlate) {
      result = result.filter((entry) => normalizePlate(entry.licensePlate) === normalizedRoutePlate);
    }

    if (!normalizedSearch) {
      return result;
    }

    return result.filter((entry) => {
      const haystack = [
        entry.licensePlate,
        entry.make,
        entry.model,
        entry.colorName,
        entry.bodyType,
        entry.classification,
        entry.engine,
        entry.fuelType,
        entry.gearbox,
        entry.driveTrain,
        entry.sourceUrl,
      ]
        .filter(Boolean)
        .join(' ')
        .toLowerCase();

      return haystack.includes(normalizedSearch);
    });
  }, [entries, normalizedRoutePlate, search]);

  const handleRecacheSinglePlate = async () => {
    if (!normalizedRoutePlate) {
      return;
    }

    setRecacheLoading(true);
    setRecacheStatus(null);

    try {
      await vehicleApi.refreshFromCarInfo(normalizedRoutePlate);
      await loadCache();
      setRecacheStatus(`Re-cached ${normalizedRoutePlate}.`);
    } catch (error) {
      console.error(`Error refreshing Car.info data for ${normalizedRoutePlate}:`, error);
      setRecacheStatus(`Failed to re-cache ${normalizedRoutePlate}.`);
    } finally {
      setRecacheLoading(false);
    }
  };

  if (loading) {
    return <div className="loading">Loading cached plates...</div>;
  }

  return (
    <div className="lookup-cache-page">
      <div className="lookup-cache-header">
        <div>
          <p className="lookup-cache-kicker">Car.info cache</p>
          <h1>{normalizedRoutePlate ? `Cached Plate: ${normalizedRoutePlate}` : 'Cached License Plates'}</h1>
          <p className="lookup-cache-subtitle">
            {normalizedRoutePlate
              ? 'Showing cached data for one selected plate.'
              : 'Browse the stored Car.info data for every plate the app has seen.'}
          </p>
        </div>
        <div className="lookup-cache-header-actions">
          {isSinglePlateView && (
            <button
              type="button"
              className="btn-primary"
              onClick={handleRecacheSinglePlate}
              disabled={recacheLoading}
            >
              {recacheLoading ? 'Re-caching...' : 'Re-cache this plate'}
            </button>
          )}
          {normalizedRoutePlate && (
            <Link to="/cached-plates" className="btn-secondary">
              View All Cached Plates
            </Link>
          )}
          <Link to="/" className="btn-secondary">
            Back to Dashboard
          </Link>
        </div>
      </div>

      <div className="lookup-cache-toolbar">
        {!isSinglePlateView && (
          <input
            type="search"
            className="lookup-cache-search"
            placeholder="Search plate, make, model, year..."
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />
        )}
        {isSinglePlateView && (
          <div className="lookup-cache-single-view-note">
            Single plate view
          </div>
        )}
        <div className="lookup-cache-count">
          {filteredEntries.length} / {entries.length} cached plates
        </div>
      </div>

      {recacheStatus && <div className="lookup-cache-status">{recacheStatus}</div>}

      {filteredEntries.length === 0 ? (
        <div className="lookup-cache-empty">
          <h2>No cached plates found</h2>
          <p>Try a different search or fetch a plate from the vehicle form first.</p>
        </div>
      ) : (
        <div className="lookup-cache-grid">
          {filteredEntries.map((entry) => (
            <article key={entry.id} className="lookup-cache-card">
              <Link to={`/cached-plates/${encodeURIComponent(entry.licensePlate)}`} style={{ textDecoration: 'none', color: 'inherit' }}>
                <div className="lookup-cache-card-header">
                  <div>
                    <h2 className="lookup-cache-plate-heading">{entry.licensePlate}</h2>
                    <p className="lookup-cache-vehicle-info">{entry.year ? `${entry.year} ${entry.make ?? ''} ${entry.model ?? ''}`.trim() : `${entry.make ?? ''} ${entry.model ?? ''}`.trim() || 'Unknown vehicle'}</p>
                  </div>
                  {!isSinglePlateView && <div className="lookup-cache-badge">Cached</div>}
                </div>
              </Link>

              {isSinglePlateView ? (
                <>
                  <div className="lookup-cache-meta">
                    <div><span>Color</span><strong>{entry.colorName || 'Unknown'}</strong></div>
                    <div><span>Mileage</span><strong>{entry.mileageKm != null ? formatDistance(entry.mileageKm) : 'Unknown'}</strong></div>
                    <div><span>Fuel</span><strong>{entry.fuelType || 'Unknown'}</strong></div>
                    <div><span>Gearbox</span><strong>{entry.gearbox || 'Unknown'}</strong></div>
                    <div><span>Drive train</span><strong>{entry.driveTrain || 'Unknown'}</strong></div>
                    <div><span>Body type</span><strong>{entry.bodyType || 'Unknown'}</strong></div>
                    <div><span>Fetched</span><strong>{formatDateTime(entry.fetchedAt)}</strong></div>
                    <div><span>Updated</span><strong>{formatDateTime(entry.updatedAt)}</strong></div>
                  </div>

                  <div className="lookup-cache-specs">
                    {[
                      ['In traffic', entry.inTraffic],
                      ['Swedish sold', entry.swedishSold],
                      ['Owner count', entry.ownerCount],
                      ['Classification', entry.classification],
                      ['Generation', entry.generation],
                      ['Engine', entry.engine],
                      ['Fuel consumption', entry.fuelConsumptionMixed],
                      ['CO2 mixed', entry.co2Mixed],
                      ['Cargo volume', entry.cargoVolume],
                      ['Seat count', entry.seatCount],
                      ['VIN', entry.vin],
                    ]
                      .filter(([, value]) => Boolean(value))
                      .map(([label, value]) => (
                        <div key={label} className="lookup-cache-spec">
                          <span>{label}</span>
                          <strong>{value}</strong>
                        </div>
                      ))}
                    <div className="lookup-cache-spec">
                      <span>Source</span>
                      <strong>
                        <a href={buildCarInfoSpecsUrl(entry.licensePlate)} target="_blank" rel="noreferrer">
                          {buildCarInfoSpecsUrl(entry.licensePlate)}
                        </a>
                      </strong>
                    </div>
                  </div>

                  {Object.keys(entry.specifications).length > 0 && (
                    <div className="lookup-cache-spec-map">
                      <h3>All captured specs</h3>
                      <dl>
                        {Object.entries(entry.specifications).map(([label, value]) => (
                          <React.Fragment key={label}>
                            <dt>{label}</dt>
                            <dd>{value}</dd>
                          </React.Fragment>
                        ))}
                      </dl>
                    </div>
                  )}
                </>
              ) : null}
            </article>
          ))}
        </div>
      )}
    </div>
  );
};

export default LookupCachePage;