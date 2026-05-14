import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { vehicleApi } from '../api';
import { Vehicle, VehicleExportPackageDto } from '../types';
import './Dashboard.css';

const Dashboard: React.FC = () => {
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [loading, setLoading] = useState(true);
	const [selectedVehicleIds, setSelectedVehicleIds] = useState<number[]>([]);
	const [isExportMode, setIsExportMode] = useState(false);
	const importInputRef = useRef<HTMLInputElement>(null);
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

  const formatCurrency = (amount: number) => {
	return new Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK' }).format(amount);
  };

  const formatPurchasePrice = (amount: number) => {
	return new Intl.NumberFormat('sv-SE', {
	  style: 'currency',
	  currency: 'SEK',
	  minimumFractionDigits: 0,
	  maximumFractionDigits: 0,
	}).format(amount);
  };

  const calculateTotalCost = (vehicle: Vehicle) => {
	return vehicle.purchasePrice + vehicle.totalExpenses;
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

  const toggleVehicleSelection = (vehicleId: number) => {
	setSelectedVehicleIds((previous) => (
	  previous.includes(vehicleId)
		? previous.filter((id) => id !== vehicleId)
		: [...previous, vehicleId]
	));
  };

	const allVehiclesSelected = vehicles.length > 0 && selectedVehicleIds.length === vehicles.length;

	const handleToggleSelectAll = () => {
	  if (allVehiclesSelected) {
		setSelectedVehicleIds([]);
		return;
	  }

	  setSelectedVehicleIds(vehicles.map((vehicle) => vehicle.id));
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
	  ) : (
		<div className="vehicle-grid">
		  {vehicles.map((vehicle) => (
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
				<h3>
				  <Link to={`/vehicles/${getVehicleRouteKey(vehicle)}`} className="vehicle-title-link">
					{vehicle.nickname ? vehicle.nickname : `${vehicle.year} ${vehicle.make} ${vehicle.model}`}
				  </Link>
				</h3>
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
				  <span className="stat-value sell">{vehicle.sellPrice ? formatPurchasePrice(vehicle.sellPrice) : '—'}</span>
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
	</div>
  );
};

export default Dashboard;
