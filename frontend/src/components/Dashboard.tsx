import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { vehicleApi } from '../api';
import { formatCurrency, formatWholeCurrency } from '../currency';
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
	const [isExportMode, setIsExportMode] = useState(false);
	const importInputRef = useRef<HTMLInputElement>(null);
  const [hoverPreview, setHoverPreview] = useState<HoverPreview | null>(null);
  const navigate = useNavigate();

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
		alert('Failed to load vehicles. Make sure the API is running.');
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

  const handleDeleteVehicle = async (id: number) => {
	if (window.confirm('Are you sure you want to delete this vehicle?')) {
	  try {
		await vehicleApi.delete(id);
		loadVehicles();
	  } catch (error) {
		console.error('Error deleting vehicle:', error);
		alert('Failed to delete vehicle');
	  }
	}
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

  const toggleVehicleSelection = (vehicleId: number) => {
	setSelectedVehicleIds((previous) => (
	  previous.includes(vehicleId)
		? previous.filter((id) => id !== vehicleId)
		: [...previous, vehicleId]
	));
  };

	const allVehiclesSelected = filteredVehicles.length > 0
	  && filteredVehicles.every((vehicle) => selectedVehicleIds.includes(vehicle.id));

	const handleToggleSelectAll = () => {
	  const filteredVehicleIds = filteredVehicles.map((vehicle) => vehicle.id);

	  if (allVehiclesSelected) {
		setSelectedVehicleIds((previous) => previous.filter((id) => !filteredVehicleIds.includes(id)));
		return;
	  }

	  setSelectedVehicleIds((previous) => Array.from(new Set([...previous, ...filteredVehicleIds])));
	};

  const handleExportSelectedVehicles = async () => {
	if (!isExportMode) {
	  setIsExportMode(true);
	  return;
	}

	if (selectedVehicleIds.length === 0) {
	  alert('Select at least one vehicle to export.');
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
	  setIsExportMode(false);
	} catch (error) {
	  console.error('Error exporting selected vehicles:', error);
	  alert('Failed to export selected vehicles.');
	}
  };

	const handleCancelExportSelection = () => {
	  setSelectedVehicleIds([]);
	  setIsExportMode(false);
	};

  const triggerImport = () => {
	importInputRef.current?.click();
  };

  const handleImportVehicle = async (event: React.ChangeEvent<HTMLInputElement>) => {
	const file = event.target.files?.[0];
	if (!file) {
	  return;
	}

	try {
	  const text = await file.text();
	  const parsed = JSON.parse(text) as VehicleExportPackageDto | { vehicles?: VehicleExportPackageDto[] };

	  if (parsed && typeof parsed === 'object' && Array.isArray((parsed as { vehicles?: VehicleExportPackageDto[] }).vehicles)) {
		for (const vehiclePayload of (parsed as { vehicles: VehicleExportPackageDto[] }).vehicles) {
		  await vehicleApi.importVehicle(vehiclePayload);
		}
	  } else {
		await vehicleApi.importVehicle(parsed as VehicleExportPackageDto);
	  }

	  await loadVehicles();
	  alert('Vehicle imported successfully.');
	} catch (error) {
	  console.error('Error importing vehicle:', error);
	  alert('Failed to import JSON. Make sure the file is a valid car export.');
	} finally {
	  event.target.value = '';
	}
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
					<button className="btn-secondary" onClick={triggerImport}>
						Import JSON
					</button>
					<button className="btn-secondary" onClick={handleExportSelectedVehicles}>
						{isExportMode ? `Export Selected (${selectedVehicleIds.length})` : 'Export'}
					</button>
					{isExportMode && (
						<button className="btn-secondary" onClick={handleToggleSelectAll}>
							{allVehiclesSelected ? 'Clear All' : 'Select All'}
						</button>
					)}
					{isExportMode && (
						<button className="btn-secondary" onClick={handleCancelExportSelection}>
							Cancel Export
						</button>
					)}
					<button className="btn-secondary" onClick={() => navigate('/cached-plates')}>
						Cached Plates
					</button>
					<button className="btn-primary" onClick={() => navigate('/vehicles/new')}>
						Add New Vehicle
					</button>
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
	  ) : (filteredVehicles.length === 0 ? (
		<div className="empty-state">
		  <h2>No matching vehicles</h2>
		  <p>Turn on "Show sold cars" to include sold vehicles in the dashboard.</p>
		</div>
	  ) : (
		<div className="vehicle-grid">
		  {filteredVehicles.map((vehicle) => (
			<div key={vehicle.id} className="vehicle-card">
			  {isExportMode && (
				<label className="vehicle-export-select">
				  <input
					type="checkbox"
					checked={selectedVehicleIds.includes(vehicle.id)}
					onChange={() => toggleVehicleSelection(vehicle.id)}
				  />
				  <span>Select for export</span>
				</label>
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
				  <p><strong>Mileage:</strong> {vehicle.mileage.toLocaleString('sv-SE')} km</p>
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
	  ))}

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
	</div>
  );
};

export default Dashboard;
