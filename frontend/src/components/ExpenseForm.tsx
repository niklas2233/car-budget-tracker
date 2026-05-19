import React, { useCallback, useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import DatePicker from 'react-datepicker';
import 'react-datepicker/dist/react-datepicker.css';
import { expenseApi, vehicleApi } from '../api';
import { CreateExpenseDto, ExpenseType, Vehicle } from '../types';
import { appCurrency, appLocale } from '../currency';
import './VehicleForm.css';

const MAX_EXPENSE_PHOTO_SIZE_BYTES = 5 * 1024 * 1024;
const MAX_EXPENSE_PHOTO_COUNT = 6;

const formatMileage = (value: number) => new Intl.NumberFormat(appLocale, { maximumFractionDigits: 0 }).format(value);

type SparePartLine = {
	id: number;
	name: string;
	cost: number;
};

const stripPartsBreakdownFromNotes = (notes: string) => notes.replace(/(?:\n\n)?Parts breakdown:[\s\S]*$/, '').trim();

const parseSparePartsBreakdownFromNotes = (notes: string): SparePartLine[] => {
	const parts: SparePartLine[] = [];
	const sectionMatch = notes.match(/Parts breakdown:\s*([\s\S]*)$/);

	if (!sectionMatch) {
		return parts;
	}

	const lines = sectionMatch[1]
		.split('\n')
		.map((line) => line.trim())
		.filter(Boolean);

	let nextId = 1;
	for (const line of lines) {
		if (line.toLowerCase().startsWith('shipping:')) {
			continue;
		}

		const itemMatch = line.match(/^-\s*(.+):\s*(-?\d+(?:\.\d+)?)\s*[A-Z]{3}$/i);
		if (!itemMatch) {
			continue;
		}

		const [, name, amountText] = itemMatch;
		const amount = parseFloat(amountText);
		if (Number.isFinite(amount)) {
			parts.push({ id: nextId++, name: name.trim(), cost: amount });
		}
	}

	return parts;
};

const buildPartsBreakdownNotes = (existingNotes: string, parts: SparePartLine[], shipping: number) => {
	const cleanNotes = stripPartsBreakdownFromNotes(existingNotes.trim());
	const breakdownLines = [
		'Parts breakdown:',
		...parts.map((part) => `- ${part.name}: ${part.cost.toFixed(2)} ${appCurrency}`),
		`Shipping: ${shipping.toFixed(2)} ${appCurrency}`,
	];
	const breakdown = breakdownLines.join('\n');

	return cleanNotes ? `${cleanNotes}\n\n${breakdown}` : breakdown;
};

const ExpenseForm: React.FC = () => {
  const navigate = useNavigate();
	const { vehicleKey, expenseId } = useParams<{ vehicleKey: string; expenseId: string }>();
	const isEditMode = Boolean(expenseId);
  const photoInputRef = React.useRef<HTMLInputElement>(null);
  const [vehicle, setVehicle] = useState<Vehicle | null>(null);
  const [mileageInput, setMileageInput] = useState('');
  const [photoError, setPhotoError] = useState<string | null>(null);
  const [formData, setFormData] = useState<CreateExpenseDto>({
		vehicleId: 0,
		type: ExpenseType.Fuel,
		description: '',
		photoDataUrls: [],
		amount: 0,
		date: new Date().toISOString().split('T')[0],
		mileage: undefined,
		vendor: '',
		notes: '',
		shipping: undefined,
  });
	const [spareParts, setSpareParts] = useState<SparePartLine[]>([
		{ id: 1, name: '', cost: 0 },
	]);

  const loadVehicle = useCallback(async () => {
		if (!vehicleKey) {
			return;
		}

		try {
			const decodedKey = decodeURIComponent(vehicleKey);
			const response = /^\d+$/.test(decodedKey)
				? await vehicleApi.getById(parseInt(decodedKey, 10))
				: await vehicleApi.getByLicensePlate(decodedKey);
			setVehicle(response.data);
			setFormData((previous) => ({
				...previous,
				vehicleId: response.data.id,
			}));
		} catch (error) {
			console.error('Error loading vehicle:', error);
		}
  }, [vehicleKey]);

  useEffect(() => {
		loadVehicle();
  }, [loadVehicle]);

  const loadExpense = useCallback(async () => {
		if (!expenseId) return;
		try {
			const response = await expenseApi.getById(parseInt(expenseId, 10));
			const e = response.data;
			setFormData({
				vehicleId: e.vehicleId,
				type: e.type,
				description: e.description,
				photoDataUrls: e.photoDataUrls ?? [],
				amount: e.amount,
				date: e.date.split('T')[0],
				mileage: e.mileage,
				vendor: e.vendor ?? '',
				notes: e.notes ?? '',
				shipping: e.shipping,
			});

			if (e.type === ExpenseType.SpareParts) {
				const parsedParts = parseSparePartsBreakdownFromNotes(e.notes ?? '');
				setSpareParts(parsedParts.length > 0 ? parsedParts : [{ id: 1, name: '', cost: 0 }]);
			}

			if (e.mileage) setMileageInput(formatMileage(e.mileage));
		} catch (error) {
			console.error('Error loading expense:', error);
		}
  }, [expenseId]);

  useEffect(() => {
		loadExpense();
  }, [loadExpense]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
		const { name, value, type } = e.target;

		if (name === 'mileage') {
			const numericOnly = value.replace(/\D/g, '');

			if (!numericOnly) {
				setFormData({
					...formData,
					mileage: undefined,
				});
				setMileageInput('');
				return;
			}

			const parsedMileage = parseInt(numericOnly, 10);
			setFormData({
				...formData,
				mileage: parsedMileage,
			});
			setMileageInput(formatMileage(parsedMileage));
			return;
		}

		if (name === 'type') {
			const nextType = parseInt(value, 10) as ExpenseType;
			if (nextType === ExpenseType.SpareParts && spareParts.length === 0) {
				setSpareParts([{ id: 1, name: '', cost: 0 }]);
			}
			setFormData({ ...formData, type: nextType });
			return;
		}

		if (type === 'number') {
			const parsed = parseFloat(value);
			setFormData({
				...formData,
				[name]: isNaN(parsed) ? (name === 'amount' ? 0 : undefined) : parsed,
			});
			return;
		}

		setFormData({
			...formData,
			[name]: value,
		});
  };

	const openPhotoPicker = () => {
		photoInputRef.current?.click();
	};

	const handlePhotoUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
		const files = Array.from(event.target.files ?? []);
		if (files.length === 0) {
			return;
		}

		const remainingSlots = MAX_EXPENSE_PHOTO_COUNT - formData.photoDataUrls.length;
		if (remainingSlots <= 0) {
			setPhotoError(`You can attach up to ${MAX_EXPENSE_PHOTO_COUNT} images.`);
			event.target.value = '';
			return;
		}

		const filesToRead = files.slice(0, remainingSlots);

		for (const file of filesToRead) {
			if (!file.type.startsWith('image/')) {
				setPhotoError('Please choose image files only.');
				event.target.value = '';
				return;
			}

			if (file.size > MAX_EXPENSE_PHOTO_SIZE_BYTES) {
				setPhotoError('Each image must be smaller than 5 MB.');
				event.target.value = '';
				return;
			}
		}

		try {
			const uploadedPhotos = await Promise.all(filesToRead.map((file) => new Promise<string>((resolve, reject) => {
				const reader = new FileReader();
				reader.onload = () => {
					if (typeof reader.result === 'string') {
						resolve(reader.result);
						return;
					}

					reject(new Error('Could not read image file.'));
				};
				reader.onerror = () => reject(reader.error ?? new Error('Could not read image file.'));
				reader.readAsDataURL(file);
			})));

			setFormData((previous) => ({
				...previous,
				photoDataUrls: [...previous.photoDataUrls, ...uploadedPhotos],
			}));
			setPhotoError(files.length > filesToRead.length ? `Only ${remainingSlots} more image${remainingSlots === 1 ? '' : 's'} could be added.` : null);
		} catch (error) {
			console.error('Error reading expense photos:', error);
			setPhotoError('Could not load one of those images. Try again.');
		} finally {
			event.target.value = '';
		}
	};

	const removePhotoAtIndex = (indexToRemove: number) => {
		setFormData((previous) => ({
			...previous,
			photoDataUrls: previous.photoDataUrls.filter((_, index) => index !== indexToRemove),
		}));
		setPhotoError(null);
	};

	const handleSparePartChange = (id: number, field: 'name' | 'cost', value: string) => {
		setSpareParts((previous) => previous.map((part) => {
			if (part.id !== id) {
				return part;
			}

			if (field === 'name') {
				return { ...part, name: value };
			}

			const parsedCost = parseFloat(value);
			return { ...part, cost: Number.isFinite(parsedCost) ? parsedCost : 0 };
		}));
	};

	const addSparePartLine = () => {
		const nextId = spareParts.length > 0 ? Math.max(...spareParts.map((part) => part.id)) + 1 : 1;
		setSpareParts((previous) => [...previous, { id: nextId, name: '', cost: 0 }]);
	};

	const removeSparePartLine = (id: number) => {
		setSpareParts((previous) => {
			if (previous.length === 1) {
				return [{ ...previous[0], name: '', cost: 0 }];
			}
			return previous.filter((part) => part.id !== id);
		});
	};

	const sparePartsTotal = spareParts.reduce((sum, part) => sum + (Number.isFinite(part.cost) ? part.cost : 0), 0);
	const sparePartsShippingRemainder = Number((formData.amount - sparePartsTotal).toFixed(2));

	useEffect(() => {
		if (formData.type !== ExpenseType.SpareParts) {
			return;
		}

		setFormData((previous) => {
			const nextShipping = Number.isFinite(sparePartsShippingRemainder) ? sparePartsShippingRemainder : 0;
			if (previous.shipping === nextShipping) {
				return previous;
			}

			return {
				...previous,
				shipping: nextShipping,
			};
		});
	}, [formData.type, sparePartsShippingRemainder]);

  const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
		try {
			let payload: CreateExpenseDto = { ...formData };

			if (formData.type === ExpenseType.SpareParts) {
				const validParts = spareParts
					.map((part) => ({ ...part, name: part.name.trim() }))
					.filter((part) => part.name.length > 0);

				if (validParts.length === 0) {
					alert('Add at least one spare part before saving.');
					return;
				}

				payload = {
					...payload,
					notes: buildPartsBreakdownNotes(payload.notes ?? '', validParts, sparePartsShippingRemainder),
					shipping: sparePartsShippingRemainder,
					description: payload.description.trim() || validParts.map((part) => part.name).join(', '),
				};
			}

			if (isEditMode && expenseId) {
				await expenseApi.update(parseInt(expenseId, 10), payload);
			} else {
				await expenseApi.create(payload);
			}

			if (vehicle) {
				navigate(`/vehicles/${vehicle.licensePlate ? encodeURIComponent(vehicle.licensePlate) : vehicle.id}`);
			} else {
				navigate('/');
			}
		} catch (error) {
			console.error('Error saving expense:', error);
			alert('Failed to save expense');
		}
  };

  return (
		<div className="form-container">
			<h1>{isEditMode ? 'Edit Expense' : 'Add Expense'} {vehicle && `for ${vehicle.year} ${vehicle.make} ${vehicle.model}`}</h1>
			<form onSubmit={handleSubmit} className="expense-form">
				<div className="form-row">
					<div className="form-group">
						<label htmlFor="type">Type *</label>
						<select
							id="type"
							name="type"
							value={formData.type}
							onChange={handleChange}
							required
						>
							<option value={ExpenseType.Fuel}>Fuel</option>
							<option value={ExpenseType.Maintenance}>Maintenance</option>
							<option value={ExpenseType.Repair}>Repair</option>
							<option value={ExpenseType.Insurance}>Insurance</option>
							<option value={ExpenseType.Registration}>Registration</option>
							<option value={ExpenseType.Parking}>Parking</option>
							<option value={ExpenseType.Tolls}>Tolls</option>
							<option value={ExpenseType.Wash}>Wash</option>
							<option value={ExpenseType.SpareParts}>Spare Parts</option>
							<option value={ExpenseType.Other}>Other</option>
						</select>
					</div>
					<div className="form-group">
						<label htmlFor="amount">Amount *</label>
						<input
							type="number"
							id="amount"
							name="amount"
							value={formData.amount}
							onChange={handleChange}
							min="0"
							step="0.01"
							required
						/>
					</div>
					<div className="form-group">
						<label htmlFor="date">Date *</label>
						<DatePicker
							id="date"
							selected={formData.date ? new Date(formData.date) : null}
							onChange={(date: Date | null) => {
								setFormData({ ...formData, date: date ? date.toISOString().split('T')[0] : '' });
							}}
							dateFormat="yyyy-MM-dd"
							showMonthDropdown
							showYearDropdown
							dropdownMode="select"
							maxDate={new Date()}
							placeholderText="Select date"
							className="datepicker-input"
							required
						/>
					</div>
				</div>

				<div className="form-row">
					<div className="form-group">
						<label htmlFor="description">Description *</label>
						<input
							type="text"
							id="description"
							name="description"
							value={formData.description}
							onChange={handleChange}
							required
						/>
					</div>
				</div>

				<div className="form-row">
					<div className="form-group form-group-photo">
						<label htmlFor="expensePhotos">Photos</label>
						<input
							ref={photoInputRef}
							type="file"
							id="expensePhotos"
							accept="image/*"
							multiple
							className="photo-input-hidden"
							onChange={handlePhotoUpload}
						/>
						<div className="expense-photo-editor">
							<div className="expense-photo-grid">
								{formData.photoDataUrls.length > 0 ? formData.photoDataUrls.map((photoDataUrl, index) => (
									<div key={`${index}-${photoDataUrl.slice(0, 32)}`} className="expense-photo-card">
										<img className="expense-photo-preview" src={photoDataUrl} alt={`Expense attachment ${index + 1}`} />
										<button
											type="button"
											className="btn-danger expense-photo-remove"
											onClick={() => removePhotoAtIndex(index)}
										>
											Remove
										</button>
									</div>
								)) : (
									<div className="vehicle-photo-placeholder expense-photo-placeholder">No photos attached yet</div>
								)}
							</div>
							<div className="vehicle-photo-actions">
								<button type="button" className="btn-secondary" onClick={openPhotoPicker}>
									Add photos
								</button>
								<small className="field-help">Attach up to {MAX_EXPENSE_PHOTO_COUNT} images. Good for receipts or part photos.</small>
								{photoError && <small className="photo-error-message">{photoError}</small>}
							</div>
						</div>
					</div>
				</div>

				{formData.type === ExpenseType.SpareParts && (
					<div className="spare-parts-box">
						<div className="spare-parts-header-row">
							<h3>Spare Parts</h3>
							<button type="button" className="btn-secondary" onClick={addSparePartLine}>
								Add Part
							</button>
						</div>
						{spareParts.map((part) => (
							<div key={part.id} className="spare-part-row">
								<input
									type="text"
									value={part.name}
									onChange={(event) => handleSparePartChange(part.id, 'name', event.target.value)}
									placeholder="Part name (e.g. Alternator)"
								/>
								<input
									type="number"
									value={part.cost}
									onChange={(event) => handleSparePartChange(part.id, 'cost', event.target.value)}
									min="0"
									step="0.01"
									placeholder="Cost"
								/>
								<button type="button" className="btn-danger" onClick={() => removeSparePartLine(part.id)}>
									Remove
								</button>
							</div>
						))}
						<div className="spare-parts-summary">
							<p><strong>Parts Total:</strong> {sparePartsTotal.toFixed(2)} {appCurrency}</p>
							<p><strong>Shipping / Remaining:</strong> {sparePartsShippingRemainder.toFixed(2)} {appCurrency}</p>
						</div>
					</div>
				)}

				<div className="form-row">
					<div className="form-group">
						<label htmlFor="vendor">Vendor</label>
						<input
							type="text"
							id="vendor"
							name="vendor"
							value={formData.vendor}
							onChange={handleChange}
						/>
					</div>
					<div className="form-group">
						<label htmlFor="shipping">Shipping ({appCurrency})</label>
						<input
							type="number"
							id="shipping"
							name="shipping"
							value={formData.type === ExpenseType.SpareParts ? sparePartsShippingRemainder : (formData.shipping ?? '')}
							onChange={handleChange}
							step="0.01"
							placeholder="0"
							readOnly={formData.type === ExpenseType.SpareParts}
						/>
						{formData.type === ExpenseType.SpareParts && (
							<small className="field-help">Auto-calculated as total amount minus sum of parts.</small>
						)}
					</div>
					<div className="form-group">
						<label htmlFor="mileage">Mileage (km)</label>
						<input
							type="text"
							id="mileage"
							name="mileage"
							value={mileageInput}
							onChange={handleChange}
							inputMode="numeric"
							placeholder="t.ex. 12 345"
						/>
					</div>
				</div>

				<div className="form-row">
					<div className="form-group">
						<label htmlFor="notes">Notes</label>
						<textarea
							id="notes"
							name="notes"
							value={formData.notes}
							onChange={handleChange}
						/>
					</div>
				</div>

				<div className="form-actions">
					<button
						type="button"
						className="btn-secondary"
						onClick={() => navigate(vehicle ? `/vehicles/${vehicle.licensePlate ? encodeURIComponent(vehicle.licensePlate) : vehicle.id}` : '/')}
					>
						Cancel
					</button>
					<button type="submit" className="btn-primary">
						{isEditMode ? 'Save Changes' : 'Add Expense'}
					</button>
				</div>
			</form>
		</div>
  );
};

export default ExpenseForm;
