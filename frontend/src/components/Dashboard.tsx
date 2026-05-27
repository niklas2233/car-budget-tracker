import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { vehicleApi } from '../api';
import { formatCurrency, formatWholeCurrency, formatDistance, supportsCarInfoLookup } from '../currency';
import { Vehicle, VehicleExportPackageDto } from '../types';
import './Dashboard.css';

const DASHBOARD_PREVIEW_WIDTH = 440;
const DASHBOARD_PREVIEW_HEIGHT = 330;

type HoverPreview = {
	src: string;
	alt: string;
	x: number;
	y: number;
};

const getHoverPreviewPosition = (clientX: number, clientY: number, previewWidth: number, previewHeight: number) => {
	const offset = 24;
	const minEdgePadding = 16;
	const maxLeft = Math.max(minEdgePadding, window.innerWidth - previewWidth - minEdgePadding);
	const maxTop = Math.max(minEdgePadding, window.innerHeight - previewHeight - minEdgePadding);

	let x = clientX + offset;
	if (x > maxLeft) {
		x = Math.max(minEdgePadding, clientX - previewWidth - offset);
	}

	const centeredTop = clientY - previewHeight / 2;
	const y = Math.min(maxTop, Math.max(minEdgePadding, centeredTop));

	return { x, y };
};

const Dashboard: React.FC = () => {
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [loading, setLoading] = useState(true);
	const [showSoldVehicles, setShowSoldVehicles] = useState(true);
	const [selectedVehicleIds, setSelectedVehicleIds] = useState<number[]>([]);
	const [isSelectMode, setIsSelectMode] = useState(false);
	const importInputRef = useRef<HTMLInputElement>(null);
  const [hoverPreview, setHoverPreview] = useState<HoverPreview | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [sortOption, setSortOption] = useState(() => localStorage.getItem('carbudget.sortOption') || 'name-asc');
  const [importConflicts, setImportConflicts] = useState<{ payload: VehicleExportPackageDto; label: string }[]>([]);
  const [toast, setToast] = useState<{ message: string; type: 'success' | 'error' } | null>(null);
  const [pendingDelete, setPendingDelete] = useState<{ type: 'single'; id: number } | { type: 'bulk' } | null>(null);
  const navigate = useNavigate();

  const showToast = (message: string, type: 'success' | 'error' = 'success') => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 3500);
  };

  const loadVehicles = useCallback(async (retries = 3) => {
	try {
	  const response = await vehicleApi.getAll();
	  setVehicles(response.data);
	} catch (error) {
	  console.error('Error loading vehicles:', error);
	  if (retries > 0) {
		console.log(`Retrying... (${retries} attempts remaining)`);
		await new Promise(resolve => setTimeout(resolve, 1000));
		await loadVehicles(retries - 1);
	  } else {
		showToast('Failed to load vehicles. Make sure the API is running.', 'error');
	  }
	} finally {
	  setLoading(false);
	}
  }, []);

	useEffect(() => {
		setSelectedVehicleIds((previous) => previous.filter((id) => vehicles.some((vehicle) => vehicle.id === id)));
	}, [vehicles]);

  useEffect(() => {
	loadVehicles();
  }, [loadVehicles]);

  const handleDeleteVehicle = (id: number) => {
    setPendingDelete({ type: 'single', id });
  };

  const formatPurchasePrice = (amount: number) => {
	return formatWholeCurrency(amount);
  };

  const calculateProfitLoss = (vehicle: Vehicle) => {
	const sellPrice = vehicle.sellPrice ?? 0;
	return sellPrice - vehicle.purchasePrice - vehicle.totalExpenses;
  };

  const getProfitLossColor = (profitLoss: number) => {
	return profitLoss >= 0 ? '#27AE60' : '#C0392B';
  };

  const getVehicleRouteKey = (vehicle: Vehicle) => {
	return vehicle.licensePlate ? encodeURIComponent(vehicle.licensePlate) : String(vehicle.id);
  };

	const hoverPreviewStyle = hoverPreview
	  ? {
		'--preview-x': `${hoverPreview.x}px`,
		'--preview-y': `${hoverPreview.y}px`,
	  } as React.CSSProperties
	  : undefined;
	const hoverPreviewSrc = hoverPreview?.src ?? '';

	const getVehicleDisplayName = (vehicle: Vehicle) => {
	  return vehicle.nickname ? vehicle.nickname : `${vehicle.year} ${vehicle.make} ${vehicle.model}`;
	};

	const handleVehiclePreviewMove = (event: React.MouseEvent<HTMLElement>, src: string, alt: string) => {
	  const { x, y } = getHoverPreviewPosition(event.clientX, event.clientY, DASHBOARD_PREVIEW_WIDTH, DASHBOARD_PREVIEW_HEIGHT);
	  setHoverPreview({ src, alt, x, y });
	};

	const clearVehiclePreview = () => {
	  setHoverPreview(null);
	};

	const isVehicleSold = (vehicle: Vehicle) => {
	  const hasSellPrice = vehicle.sellPrice !== undefined && vehicle.sellPrice !== null;
	  const hasSellDate = Boolean(vehicle.sellDate);
	  return hasSellPrice || hasSellDate;
	};

	const filteredVehicles = showSoldVehicles
	  ? vehicles
	  : vehicles.filter((vehicle) => !isVehicleSold(vehicle));

  const searchedVehicles = searchQuery.trim()
    ? filteredVehicles.filter((vehicle) => {
        const q = searchQuery.toLowerCase();
        return (
          getVehicleDisplayName(vehicle).toLowerCase().includes(q) ||
          (vehicle.licensePlate ?? '').toLowerCase().includes(q) ||
          (vehicle.vin ?? '').toLowerCase().includes(q)
        );
      })
    : filteredVehicles;

  const sortedVehicles = [...searchedVehicles].sort((a, b) => {
    switch (sortOption) {
      case 'name-asc':
        return getVehicleDisplayName(a).localeCompare(getVehicleDisplayName(b));
      case 'name-desc':
        return getVehicleDisplayName(b).localeCompare(getVehicleDisplayName(a));
      case 'date-newest':
        return new Date(b.purchaseDate).getTime() - new Date(a.purchaseDate).getTime();
      case 'date-oldest':
        return new Date(a.purchaseDate).getTime() - new Date(b.purchaseDate).getTime();
      case 'price-asc':
        return a.purchasePrice - b.purchasePrice;
      case 'price-desc':
        return b.purchasePrice - a.purchasePrice;
      case 'expenses-desc':
        return b.totalExpenses - a.totalExpenses;
      case 'expenses-asc':
        return a.totalExpenses - b.totalExpenses;
      case 'profit-desc':
        return calculateProfitLoss(b) - calculateProfitLoss(a);
      case 'profit-asc':
        return calculateProfitLoss(a) - calculateProfitLoss(b);
      default:
        return 0;
    }
  });

  const toggleVehicleSelection = (vehicleId: number) => {
	setSelectedVehicleIds((previous) => (
	  previous.includes(vehicleId)
		? previous.filter((id) => id !== vehicleId)
		: [...previous, vehicleId]
	));
  };

	const allVehiclesSelected = sortedVehicles.length > 0
	  && sortedVehicles.every((vehicle) => selectedVehicleIds.includes(vehicle.id));

	const handleToggleSelectAll = () => {
	  const visibleVehicleIds = sortedVehicles.map((vehicle) => vehicle.id);

	  if (allVehiclesSelected) {
		setSelectedVehicleIds((previous) => previous.filter((id) => !visibleVehicleIds.includes(id)));
		return;
	  }

	  setSelectedVehicleIds((previous) => Array.from(new Set([...previous, ...visibleVehicleIds])));
	};

  const handleExportSelected = async () => {
	if (selectedVehicleIds.length === 0) {
	  showToast('Select at least one vehicle to export.', 'error');
	  return;
	}

	try {
	  const sortedIds = [...selectedVehicleIds].sort((a, b) => a - b);
	  const responses = await Promise.all(sortedIds.map((id) => vehicleApi.exportVehicle(id)));
	  const exportedVehicles = responses.map((response) => response.data);

	  const payload = exportedVehicles.length === 1
		? exportedVehicles[0]
		: {
			schemaVersion: 1,
			exportedAtUtc: new Date().toISOString(),
			vehicles: exportedVehicles,
		  };

	  const prettyJson = JSON.stringify(payload, null, 2);
	  const blob = new Blob([prettyJson], { type: 'application/json' });
	  const url = window.URL.createObjectURL(blob);

	  const link = document.createElement('a');
	  link.href = url;
	  const now = new Date();
	  const datePart = now.toISOString().slice(0, 10);
	  const timePart = now.toTimeString().slice(0, 5).replace(':', '-');
	  link.download = `carbudget_export_${datePart}_${timePart}.json`;
	  document.body.appendChild(link);
	  link.click();
	  link.remove();
	  window.URL.revokeObjectURL(url);
	  setSelectedVehicleIds([]);
	  setIsSelectMode(false);
	} catch (error) {
	  console.error('Error exporting selected vehicles:', error);
	  showToast('Failed to export selected vehicles.', 'error');
	}
  };

  const handleDeleteSelected = () => {
    if (selectedVehicleIds.length === 0) {
      showToast('Select at least one vehicle to delete.', 'error');
      return;
    }
    setPendingDelete({ type: 'bulk' });
  };

  const confirmDelete = async () => {
    if (!pendingDelete) return;
    const ids = pendingDelete.type === 'single' ? [pendingDelete.id] : selectedVehicleIds;
    setPendingDelete(null);
    try {
      await Promise.all(ids.map((id) => vehicleApi.delete(id)));
      if (pendingDelete.type === 'bulk') {
        setSelectedVehicleIds([]);
        setIsSelectMode(false);
      }
      await loadVehicles();
      showToast(`${ids.length === 1 ? 'Vehicle' : `${ids.length} vehicles`} deleted.`);
    } catch (error) {
      console.error('Error deleting vehicle(s):', error);
      showToast('Failed to delete one or more vehicles.', 'error');
      await loadVehicles();
    }
  };

	const handleCancelSelection = () => {
	  setSelectedVehicleIds([]);
	  setIsSelectMode(false);
	};

  const triggerImport = () => {
	importInputRef.current?.click();
  };

  const getVehicleLabel = (pkg: VehicleExportPackageDto) => {
    const v = pkg.vehicle;
    const name = [v.year, v.make, v.model].filter(Boolean).join(' ');
    const id = v.licensePlate || v.vin || '';
    return id ? `${name} (${id})` : name;
  };

  const handleImportVehicle = async (event: React.ChangeEvent<HTMLInputElement>) => {
    (window as any).api?.refocusWindow();
    const file = event.target.files?.[0];
    if (!file) return;

    let payloads: VehicleExportPackageDto[];
    try {
      const text = await file.text();
      const parsed = JSON.parse(text) as VehicleExportPackageDto | { vehicles?: VehicleExportPackageDto[] };
      payloads = Array.isArray((parsed as { vehicles?: VehicleExportPackageDto[] }).vehicles)
        ? (parsed as { vehicles: VehicleExportPackageDto[] }).vehicles
        : [parsed as VehicleExportPackageDto];
    } catch {
      showToast('Failed to import JSON. Make sure the file is a valid car export.', 'error');
      event.target.value = '';
      return;
    }

    const conflicts: { payload: VehicleExportPackageDto; label: string }[] = [];
    for (const payload of payloads) {
      try {
        await vehicleApi.importVehicle(payload);
      } catch (error: any) {
        if (error?.response?.status === 409) {
          conflicts.push({ payload, label: getVehicleLabel(payload) });
        } else {
          showToast(`Failed to import "${getVehicleLabel(payload)}": ${error?.response?.data || error?.message || 'Unknown error'}`, 'error');
        }
      }
    }

    await loadVehicles();
    event.target.value = '';

    if (conflicts.length > 0) {
      setImportConflicts(conflicts);
    } else {
      showToast(`${payloads.length === 1 ? 'Vehicle' : `${payloads.length} vehicles`} imported successfully.`);
    }
  };

  const handleOverwriteConflicts = async (toOverwrite: { payload: VehicleExportPackageDto; label: string }[]) => {
    setImportConflicts([]);
    for (const { payload, label } of toOverwrite) {
      try {
        await vehicleApi.importVehicle(payload, true);
      } catch (error: any) {
        showToast(`Failed to overwrite "${label}": ${error?.response?.data || error?.message || 'Unknown error'}`, 'error');
      }
    }
    await loadVehicles();
    showToast(`${toOverwrite.length} vehicle${toOverwrite.length > 1 ? 's' : ''} overwritten successfully.`);
  };

  if (loading) {
	return <div className="loading">Loading...</div>;
  }

  return (
	<div className="dashboard">
	  <input
		ref={importInputRef}
		type="file"
		accept="application/json,.json"
		onChange={handleImportVehicle}
		className="import-json-input"
	  />
	  <div className="dashboard-header">
		<h1>Car Budget Dashboard</h1>
				<div className="dashboard-header-actions">
					<label className="sold-toggle">
						<input
							type="checkbox"
							checked={showSoldVehicles}
							onChange={(event) => setShowSoldVehicles(event.target.checked)}
						/>
						<span>Show sold cars</span>
					</label>
					{!isSelectMode && (
						<button className="btn-secondary" onClick={triggerImport}>
							Import JSON
						</button>
					)}
					<button className="btn-secondary" onClick={() => { if (!isSelectMode) setIsSelectMode(true); }}>
						{isSelectMode ? `${selectedVehicleIds.length} selected` : 'Select'}
					</button>
					{isSelectMode && (
						<button className="btn-secondary" onClick={handleToggleSelectAll}>
							{allVehiclesSelected ? 'Clear All' : 'Select All'}
						</button>
					)}
					{isSelectMode && (
						<button className="btn-secondary" onClick={handleExportSelected}>
							Export
						</button>
					)}
					{isSelectMode && (
						<button className="btn-danger" onClick={handleDeleteSelected}>
							Delete
						</button>
					)}
					{isSelectMode && (
						<button className="btn-secondary" onClick={handleCancelSelection}>
							Cancel
						</button>
					)}
					{supportsCarInfoLookup && (
						<button className="btn-secondary" onClick={() => navigate('/cached-plates')}>
							Cached Plates
						</button>
					)}
					<button className="btn-primary" onClick={() => navigate('/vehicles/new')}>
						Add New Vehicle
					</button>
				</div>
	  </div>

	  <div className="dashboard-controls">
		  <div className="dashboard-search-wrap">
			<span className="dashboard-search-icon" aria-hidden="true">&#128269;</span>
			<input
			  type="search"
			  className="dashboard-search-input"
			  placeholder="Search by name, plate, or VIN…"
			  value={searchQuery}
			  onChange={(e) => setSearchQuery(e.target.value)}
			/>
		  </div>
		  <div className="dashboard-sort-wrap">
			<label className="dashboard-sort-label" htmlFor="dashboard-sort">Sort:</label>
			<select
			  id="dashboard-sort"
			  className="dashboard-sort-select"
			  value={sortOption}
			  onChange={(e) => { setSortOption(e.target.value); localStorage.setItem('carbudget.sortOption', e.target.value); }}
			>
			  <option value="name-asc">Name (A–Z)</option>
			  <option value="name-desc">Name (Z–A)</option>
			  <option value="date-newest">Purchase Date (Newest)</option>
			  <option value="date-oldest">Purchase Date (Oldest)</option>
			  <option value="price-asc">Purchase Price (Low–High)</option>
			  <option value="price-desc">Purchase Price (High–Low)</option>
			  <option value="expenses-desc">Total Expenses (High–Low)</option>
			  <option value="expenses-asc">Total Expenses (Low–High)</option>
			  <option value="profit-desc">Result (Best–Worst)</option>
			  <option value="profit-asc">Result (Worst–Best)</option>
			</select>
		  </div>
		</div>

  {vehicles.length === 0 ? (
		<div className="empty-state">
		  <h2>No vehicles yet</h2>
		  <p>Add your first vehicle to start tracking expenses</p>
		  <button className="btn-primary" onClick={() => navigate('/vehicles/new')}>
			Add Vehicle
		  </button>
		</div>
	  ) : filteredVehicles.length === 0 ? (
		<div className="empty-state">
		  <h2>No matching vehicles</h2>
		  <p>Turn on "Show sold cars" to include sold vehicles in the dashboard.</p>
		</div>
	  ) : sortedVehicles.length === 0 ? (
		<div className="empty-state">
		  <h2>No results</h2>
		  <p>No vehicles match "<strong>{searchQuery}</strong>". Try a different name, plate, or VIN.</p>
		</div>
	  ) : (
		<div className="vehicle-grid">
		  {sortedVehicles.map((vehicle) => (
			<div key={vehicle.id} className={`vehicle-card${isSelectMode && selectedVehicleIds.includes(vehicle.id) ? ' vehicle-card--selected' : ''}`}>
			  {isSelectMode && (
				<div className="vehicle-select-overlay" onClick={() => toggleVehicleSelection(vehicle.id)}>
				  <div className={`vehicle-select-check${selectedVehicleIds.includes(vehicle.id) ? ' vehicle-select-check--checked' : ''}`}>✓</div>
				</div>
			  )}
			  <div className="vehicle-header">
				<div className="vehicle-title-wrap">
				  {vehicle.photoDataUrl ? (
					<div
					  className="vehicle-thumb-hover-card"
					  onMouseEnter={(event) => handleVehiclePreviewMove(event, vehicle.photoDataUrl!, `${getVehicleDisplayName(vehicle)} preview`)}
					  onMouseMove={(event) => handleVehiclePreviewMove(event, vehicle.photoDataUrl!, `${getVehicleDisplayName(vehicle)} preview`)}
					  onMouseLeave={clearVehiclePreview}
					>
					  <img
						className="vehicle-thumb"
						src={vehicle.photoDataUrl}
						alt={`${getVehicleDisplayName(vehicle)} preview`}
					  />
					</div>
				  ) : (
					<div className="vehicle-thumb vehicle-thumb-placeholder" aria-hidden="true">
					  {vehicle.make.charAt(0)}
					</div>
				  )}
				  <h3>
				  <Link to={`/vehicles/${getVehicleRouteKey(vehicle)}`} className="vehicle-title-link">
					{getVehicleDisplayName(vehicle)}
				  </Link>
				  </h3>
				  {isVehicleSold(vehicle) && <span className="vehicle-status-sold">Sold</span>}
				</div>
				<span className="vehicle-color" style={{ backgroundColor: vehicle.color || '#ccc' }}></span>
			  </div>
			  {vehicle.nickname && (
				<p className="vehicle-subtitle">{vehicle.year} {vehicle.make} {vehicle.model}</p>
			  )}

			  <div className="vehicle-details">
				{vehicle.licensePlate && (
				  <p><strong>License Plate:</strong> {vehicle.licensePlate}</p>
				)}
				{vehicle.vin && (
				  <p><strong>VIN:</strong> {vehicle.vin}</p>
				)}
				{vehicle.mileage && (
				  <p><strong>Mileage:</strong> {formatDistance(vehicle.mileage)}</p>
				)}
			  </div>

			  <div className="vehicle-stats">
				<div className="stat">
				  <span className="stat-label">Purchase Price</span>
				  <span className="stat-value">{formatPurchasePrice(vehicle.purchasePrice)}</span>
				</div>
				<div className="stat">
				  <span className="stat-label">Sell Price</span>
				  <span className="stat-value sell">
					{vehicle.sellPrice !== undefined && vehicle.sellPrice !== null
					  ? formatPurchasePrice(vehicle.sellPrice)
					  : '—'}
				  </span>
				</div>
				<div className="stat">
				  <span className="stat-label">Total Expenses</span>
				  <span className="stat-value expense">{formatCurrency(vehicle.totalExpenses)}</span>
				</div>
				<div className="stat">
				  <span className="stat-label">Result</span>
				  <span className="stat-value profit-loss" style={{ color: getProfitLossColor(calculateProfitLoss(vehicle)) }}>
					{formatCurrency(calculateProfitLoss(vehicle))}
				  </span>
				</div>
			  </div>

			  <div className="vehicle-actions">
				<button className="btn-secondary" onClick={() => navigate(`/vehicles/${getVehicleRouteKey(vehicle)}`)}>
				  View Details
				</button>
				<button className="btn-secondary" onClick={() => navigate(`/vehicles/${getVehicleRouteKey(vehicle)}/expenses/new`)}>
				  Add Expense
				</button>
				<button className="btn-danger" onClick={() => handleDeleteVehicle(vehicle.id)}>
				  Delete
				</button>
			  </div>
			</div>
		  ))}
		</div>
	  )}

	  {hoverPreview && (
		<div
		  className="vehicle-thumb-hover-preview is-visible"
		  style={hoverPreviewStyle}
		  aria-hidden="true"
		>
		  <img
			className="vehicle-thumb-hover-image"
			src={hoverPreviewSrc}
			alt=""
		  />
		</div>
	  )}

      {importConflicts.length > 0 && (
        <div className="import-conflict-backdrop">
          <div className="import-conflict-modal">
            <h3>Vehicle{importConflicts.length > 1 ? 's' : ''} already exist</h3>
            <p>The following vehicle{importConflicts.length > 1 ? 's' : ''} already exist in your database:</p>
            <ul className="import-conflict-list">
              {importConflicts.map((c, i) => (
                <li key={i}>{c.label}</li>
              ))}
            </ul>
            <p>Do you want to overwrite {importConflicts.length > 1 ? 'them' : 'it'}?</p>
            <div className="import-conflict-actions">
              <button className="btn-secondary" onClick={() => setImportConflicts([])}>Skip</button>
              <button className="btn-danger" onClick={() => handleOverwriteConflicts(importConflicts)}>Overwrite</button>
            </div>
          </div>
        </div>
      )}

      {pendingDelete && (
        <div className="import-conflict-backdrop">
          <div className="import-conflict-modal">
            <h3>Delete {pendingDelete.type === 'bulk' ? `${selectedVehicleIds.length} vehicle${selectedVehicleIds.length > 1 ? 's' : ''}` : 'vehicle'}?</h3>
            <p>This will also remove all associated expenses and cannot be undone.</p>
            <div className="import-conflict-actions">
              <button className="btn-secondary" onClick={() => setPendingDelete(null)}>Cancel</button>
              <button className="btn-danger" onClick={confirmDelete}>Delete</button>
            </div>
          </div>
        </div>
      )}

      {toast && (
        <div className={`dashboard-toast dashboard-toast--${toast.type}`}>
          {toast.message}
        </div>
      )}
	</div>
  );
};

export default Dashboard;
