import React, { useCallback, useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { vehicleApi, expenseApi, getApiErrorMessage } from '../api';
import { formatCurrency, appLocale, formatDistance, distanceLabel, supportsCarInfoLookup } from '../currency';
import { Vehicle, Expense } from '../types';
import './VehicleDetails.css';

const EXPENSE_PREVIEW_WIDTH = 520;
const EXPENSE_PREVIEW_HEIGHT = 390;

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

const VehicleDetails: React.FC = () => {
  const { vehicleKey } = useParams<{ vehicleKey: string }>();
  const navigate = useNavigate();
  const [vehicle, setVehicle] = useState<Vehicle | null>(null);
  const [expenses, setExpenses] = useState<Expense[]>([]);
  const [loading, setLoading] = useState(true);
	const [refreshingCarInfo, setRefreshingCarInfo] = useState(false);
	const [hoverPreview, setHoverPreview] = useState<HoverPreview | null>(null);

  const loadData = useCallback(async () => {
	if (!vehicleKey) {
	  setLoading(false);
	  return;
	}

	try {
	  const decodedKey = decodeURIComponent(vehicleKey);
	  const vehicleResponse = /^\d+$/.test(decodedKey)
		? await vehicleApi.getById(parseInt(decodedKey, 10))
		: await vehicleApi.getByLicensePlate(decodedKey);

	  const loadedVehicle = vehicleResponse.data;
	  const expensesResponse = await expenseApi.getByVehicleId(loadedVehicle.id);
	  setVehicle(loadedVehicle);
	  setExpenses(expensesResponse.data);
	} catch (error) {
	  console.error('Error loading data:', error);
	  alert('Failed to load vehicle details');
	} finally {
	  setLoading(false);
	}
  }, [vehicleKey]);

  useEffect(() => {
	loadData();
  }, [loadData]);

  const handleDeleteExpense = async (expenseId: number) => {
	if (window.confirm('Are you sure you want to delete this expense?')) {
	  try {
		await expenseApi.delete(expenseId);
		loadData();
	  } catch (error) {
		console.error('Error deleting expense:', error);
		alert('Failed to delete expense');
	  }
	}
  };

  const handleRefreshCarInfo = async () => {
	if (!vehicle?.licensePlate) {
	  alert('This vehicle needs a license plate to refresh Car.info data.');
	  return;
	}

	setRefreshingCarInfo(true);
	try {
	  await vehicleApi.refreshFromCarInfo(vehicle.licensePlate);
	  alert('Car.info data refreshed and cached.');
	} catch (error) {
	  console.error('Error refreshing Car.info data:', error);
	  alert(getApiErrorMessage(error, 'Failed to refresh Car.info data.'));
	} finally {
	  setRefreshingCarInfo(false);
	}
  };

  const formatPurchasePrice = (amount: number) => {
	return new Intl.NumberFormat(appLocale, { maximumFractionDigits: 0 }).format(amount);
  };

  const formatDate = (dateString: string) => {
	return new Date(dateString).toLocaleDateString();
  };

	const handleExpensePreviewMove = (event: React.MouseEvent<HTMLElement>, src: string, alt: string) => {
	  const { x, y } = getHoverPreviewPosition(event.clientX, event.clientY, EXPENSE_PREVIEW_WIDTH, EXPENSE_PREVIEW_HEIGHT);
	  setHoverPreview({ src, alt, x, y });
	};

	const clearExpensePreview = () => {
	  setHoverPreview(null);
	};

	const hoverPreviewStyle = hoverPreview
	  ? {
		'--preview-x': `${hoverPreview.x}px`,
		'--preview-y': `${hoverPreview.y}px`,
	  } as React.CSSProperties
	  : undefined;
	const hoverPreviewSrc = hoverPreview?.src ?? '';

  if (loading) {
	return <div className="loading">Loading...</div>;
  }

  if (!vehicle) {
	return <div className="loading">Vehicle not found</div>;
  }

  const resultAmount = vehicle.sellPrice != null
	? vehicle.sellPrice - (vehicle.purchasePrice + vehicle.totalExpenses)
	: null;

  return (
	<>
	<div className="vehicle-details">
	  <div className="details-header">
		<button className="btn-back" onClick={() => navigate('/')}>
		  ← Back to Dashboard
		</button>
		<h1>{vehicle.year} {vehicle.make} {vehicle.model}</h1>
		<div className="vehicle-actions">
		  <button className="btn-secondary" onClick={() => navigate(`/vehicles/${vehicle.licensePlate ? encodeURIComponent(vehicle.licensePlate) : vehicle.id}/edit`)}>
			Edit Vehicle
		  </button>
		  {supportsCarInfoLookup && (
			<button
			  className="btn-secondary"
			  onClick={handleRefreshCarInfo}
			  disabled={refreshingCarInfo || !vehicle.licensePlate}
			>
			  {refreshingCarInfo ? 'Refreshing Car.info...' : 'Update Car.info Data'}
			</button>
		  )}
		  <button className="btn-primary" onClick={() => navigate(`/vehicles/${vehicle.licensePlate ? encodeURIComponent(vehicle.licensePlate) : vehicle.id}/expenses/new`)}>
			Add Expense
		  </button>
		</div>
	  </div>

	  <div className="vehicle-info-card">
		<h2>Vehicle Information</h2>
		<div className="info-grid">
		  {/* Row 1: Make, Model, Year, License Plate, VIN */}
		  <div className="info-item">
			<span className="info-label">Make</span>
			<span className="info-value">{vehicle.make}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">Model</span>
			<span className="info-value">{vehicle.model}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">Year</span>
			<span className="info-value">{vehicle.year}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">License Plate</span>
			<span className="info-value">{vehicle.licensePlate || '-'}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">VIN</span>
			<span className="info-value">{vehicle.vin || '-'}</span>
		  </div>
		  {/* Row 2: Mileage, Purchase Date, Sell Date, Purchase Price, Sell Price */}
		  <div className="info-item">
			<span className="info-label">Mileage</span>
			<span className="info-value">{vehicle.mileage ? formatDistance(vehicle.mileage) : '-'}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">Purchase Date</span>
			<span className="info-value">{formatDate(vehicle.purchaseDate)}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">Sell Date</span>
			<span className="info-value">{vehicle.sellDate ? formatDate(vehicle.sellDate) : '-'}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">Purchase Price</span>
			<span className="info-value">{formatPurchasePrice(vehicle.purchasePrice)}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">Sell Price</span>
			<span className="info-value">{vehicle.sellPrice != null ? formatPurchasePrice(vehicle.sellPrice) : '-'}</span>
		  </div>
		  {/* Row 3: Total Expenses, Total Cost, Result */}
		  <div className="info-item">
			<span className="info-label">Total Expenses</span>
			<span className="info-value expense">{formatCurrency(vehicle.totalExpenses)}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">Total Cost</span>
			<span className="info-value total">{formatCurrency(vehicle.purchasePrice + vehicle.totalExpenses)}</span>
		  </div>
		  <div className="info-item">
			<span className="info-label">Result</span>
			<span className={`info-value ${resultAmount == null ? '' : resultAmount >= 0 ? 'profit' : 'expense'}`}>
			  {resultAmount == null ? '-' : formatCurrency(resultAmount)}
			</span>
		  </div>
		</div>
	  </div>

	  <div className="expenses-section">
		<h2>Expense History ({expenses.length})</h2>
		{expenses.length === 0 ? (
		  <div className="empty-expenses">
			<p>No expenses recorded yet</p>
			<button className="btn-primary" onClick={() => navigate(`/vehicles/${vehicle.licensePlate ? encodeURIComponent(vehicle.licensePlate) : vehicle.id}/expenses/new`)}>
			  Add First Expense
			</button>
		  </div>
		) : (
		  <div className="expenses-table">
			<table>
			  <thead>
				<tr>
				  <th>Date</th>
				  <th>Type</th>
				  <th>Description</th>
				  <th>Amount</th>
				  <th>Vendor</th>
				  <th>Mileage ({distanceLabel})</th>
				  <th>Actions</th>
				</tr>
			  </thead>
			  <tbody>
				{expenses.map((expense) => (
				  <tr key={expense.id}>
					<td>{formatDate(expense.date)}</td>
					<td><span className="expense-type">{expense.typeName}</span></td>
					<td>
					  <div className="expense-description-cell">
						<span>{expense.description}</span>
						{expense.photoDataUrls.length > 0 && (
						  <div className="expense-photo-strip">
							{expense.photoDataUrls.map((photoDataUrl, index) => (
							  <div
								key={`${expense.id}-${index}`}
								className="expense-photo-hover-card"
								onMouseEnter={(event) => handleExpensePreviewMove(event, photoDataUrl, `${expense.description} attachment ${index + 1}`)}
								onMouseMove={(event) => handleExpensePreviewMove(event, photoDataUrl, `${expense.description} attachment ${index + 1}`)}
								onMouseLeave={clearExpensePreview}
							  >
								<a
								  href={photoDataUrl}
								  target="_blank"
								  rel="noreferrer"
								  className="expense-photo-link"
								>
								  <img
									src={photoDataUrl}
									alt={`${expense.description} attachment ${index + 1}`}
									className="expense-history-thumb"
								  />
								</a>
							  </div>
							))}
						  </div>
						)}
					  </div>
					</td>
					<td className="amount">{formatCurrency(expense.type === 10 ? expense.amount : expense.amount + (expense.shipping ?? 0))}</td>
					<td>{expense.vendor || '-'}</td>
					<td>{expense.mileage ? formatDistance(expense.mileage) : '-'}</td>
					<td className="expense-actions">
					  <div className="expense-actions-inner">
						<button
						  className="btn-secondary btn-sm expense-action-btn"
						  title="Edit expense"
						  aria-label="Edit expense"
						  onClick={() => navigate(`/vehicles/${vehicle.licensePlate ? encodeURIComponent(vehicle.licensePlate) : vehicle.id}/expenses/${expense.id}/edit`)}
						>
						  <span className="expense-action-emoji" aria-hidden="true">🔧</span>
						</button>
						<button
						  className="btn-delete btn-sm expense-action-btn"
						  title="Delete expense"
						  aria-label="Delete expense"
						  onClick={() => handleDeleteExpense(expense.id)}
						>
						  <span className="expense-action-emoji" aria-hidden="true">🗑️</span>
						</button>
					  </div>
					</td>
				  </tr>
				))}
			  </tbody>
			</table>
		  </div>
		)}
	  </div>
	</div>

	  {hoverPreview && (
		<div
		  className="expense-photo-hover-preview is-visible"
		  style={hoverPreviewStyle}
		  aria-hidden="true"
		>
		  <img
			className="expense-photo-hover-image"
			src={hoverPreviewSrc}
			alt=""
		  />
		</div>
	  )}
	</>
  );
};

export default VehicleDetails;
