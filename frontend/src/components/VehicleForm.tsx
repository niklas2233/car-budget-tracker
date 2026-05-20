import React, { useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import DatePicker from 'react-datepicker';
import 'react-datepicker/dist/react-datepicker.css';
import { getApiErrorMessage, vehicleApi } from '../api';
import { CreateVehicleDto } from '../types';
import { appCurrency, appLocale, calendarStartDay, distanceLabel, kmToDisplayDistance, displayDistanceToKm, supportsCarInfoLookup } from '../currency';
import carMakesJson from '../carMakes.json';
import './VehicleForm.css';

const MAX_PHOTO_SIZE_BYTES = 5 * 1024 * 1024;

const MAKE_MODEL_OPTIONS: Record<string, string[]> = carMakesJson as Record<string, string[]>;

const CUSTOM_MAKE_MODEL_STORAGE_KEY = 'carbudget.customMakeModelOptions';
const PRESET_COLORS = ['#111111', '#FFFFFF', '#C0C0C0', '#1F3A93', '#C0392B', '#27AE60', '#F39C12', '#8E44AD'];

const compareOptionText = (a: string, b: string) => a.localeCompare(b, undefined, { sensitivity: 'base' });

const loadCustomMakeModelOptions = (): Record<string, string[]> => {
	if (typeof window === 'undefined') {
		return {};
	}

	try {
		const raw = window.localStorage.getItem(CUSTOM_MAKE_MODEL_STORAGE_KEY);
		if (!raw) {
			return {};
		}

		const parsed = JSON.parse(raw);
		if (!parsed || typeof parsed !== 'object') {
			return {};
		}

		const result: Record<string, string[]> = {};
		for (const [make, models] of Object.entries(parsed as Record<string, unknown>)) {
			if (!Array.isArray(models)) {
				continue;
			}

			result[make] = models
				.filter((model): model is string => typeof model === 'string' && model.trim().length > 0)
				.map((model) => model.trim())
				.sort(compareOptionText);
		}

		return result;
	} catch {
		return {};
	}
};

const saveCustomMakeModelOptions = (value: Record<string, string[]>) => {
	if (typeof window === 'undefined') {
		return;
	}

	window.localStorage.setItem(CUSTOM_MAKE_MODEL_STORAGE_KEY, JSON.stringify(value));
};

const mergeMakeModelOptions = (
	baseOptions: Record<string, string[]>,
	customOptions: Record<string, string[]>
): Record<string, string[]> => {
	const merged: Record<string, string[]> = {};

	for (const [make, models] of Object.entries(baseOptions)) {
		merged[make] = [...models].sort(compareOptionText);
	}

	for (const [make, models] of Object.entries(customOptions)) {
		const existing = merged[make] ?? [];
		const combined = new Set(existing);
		for (const model of models) {
			combined.add(model);
		}
		merged[make] = Array.from(combined).sort(compareOptionText);
	}

	return merged;
};

const formatMileage = (value: number) => new Intl.NumberFormat(appLocale, { maximumFractionDigits: 0 }).format(value);

const CAR_INFO_COLOR_MAP: Record<string, string> = {
	black: '#111111',
	vit: '#FFFFFF',
	white: '#FFFFFF',
	grå: '#C0C0C0',
	gray: '#C0C0C0',
	grey: '#C0C0C0',
	blå: '#1F3A93',
	blue: '#1F3A93',
	röd: '#C0392B',
	red: '#C0392B',
	grön: '#27AE60',
	green: '#27AE60',
	orange: '#F39C12',
	purple: '#8E44AD',
	lila: '#8E44AD',
};

const mapCarInfoColorToHex = (value?: string) => {
	if (!value) {
	  return undefined;
	}

	if (/^#([0-9a-f]{3}|[0-9a-f]{6})$/i.test(value.trim())) {
	  return value.trim();
	}

	const normalized = value.trim().toLowerCase();
	return CAR_INFO_COLOR_MAP[normalized] || undefined;
};

const VehicleForm: React.FC = () => {
  const navigate = useNavigate();
	const { vehicleKey } = useParams<{ vehicleKey: string }>();
	const isEditMode = Boolean(vehicleKey);
	const colorInputRef = useRef<HTMLInputElement>(null);
	const photoInputRef = useRef<HTMLInputElement>(null);
	const [lookupLoading, setLookupLoading] = useState(false);
	const [lookupMessage, setLookupMessage] = useState<string | null>(null);
	const [photoError, setPhotoError] = useState<string | null>(null);
	const [customMakeModelOptions, setCustomMakeModelOptions] = useState<Record<string, string[]>>(loadCustomMakeModelOptions);
  const [loading, setLoading] = useState(isEditMode);
  const [resolvedVehicleId, setResolvedVehicleId] = useState<number | null>(null);
  const [mileageInput, setMileageInput] = useState('');
  const [formData, setFormData] = useState<CreateVehicleDto>({
	make: 'Volkswagen',
	model: '',
	year: new Date().getFullYear(),
	photoDataUrl: undefined,
	vin: '',
	licensePlate: '',
	purchasePrice: 0,
	purchaseDate: new Date().toISOString().split('T')[0],
	mileage: undefined,
	color: '#000000',
	nickname: '',
	sellPrice: undefined,
	sellDate: undefined,
  });

	const combinedMakeModelOptions = mergeMakeModelOptions(MAKE_MODEL_OPTIONS, customMakeModelOptions);
	const availableMakes = Object.keys(combinedMakeModelOptions).sort(compareOptionText);

	const makeOptions = availableMakes.includes(formData.make) || !formData.make
		? availableMakes
		: [formData.make, ...availableMakes];
	const baseModelsForMake = combinedMakeModelOptions[formData.make] || [];
	const modelsForSelectedMake = formData.model && !baseModelsForMake.includes(formData.model)
		? [formData.model, ...baseModelsForMake]
		: baseModelsForMake;
	const selectedColor = formData.color || '#000000';
	const vehicleDisplayName = formData.nickname?.trim() || [formData.year, formData.make, formData.model].filter(Boolean).join(' ');

	const addFetchedMakeModelOption = (make?: string, model?: string) => {
		const normalizedMake = make?.trim();
		const normalizedModel = model?.trim();

		if (!normalizedMake) {
			return;
		}

		setCustomMakeModelOptions((previous) => {
			const baseModels = MAKE_MODEL_OPTIONS[normalizedMake] ?? [];
			const customModels = previous[normalizedMake] ?? [];

			const hasMakeInBase = Object.prototype.hasOwnProperty.call(MAKE_MODEL_OPTIONS, normalizedMake);
			const hasMakeInCustom = Object.prototype.hasOwnProperty.call(previous, normalizedMake);
			let changed = !hasMakeInBase && !hasMakeInCustom;

			const nextModelSet = new Set(customModels);
			if (normalizedModel) {
				const existsInBase = baseModels.some((item) => item.toLowerCase() === normalizedModel.toLowerCase());
				const existsInCustom = customModels.some((item) => item.toLowerCase() === normalizedModel.toLowerCase());

				if (!existsInBase && !existsInCustom) {
					nextModelSet.add(normalizedModel);
					changed = true;
				}
			}

			if (!changed) {
				return previous;
			}

			const next = {
				...previous,
				[normalizedMake]: Array.from(nextModelSet).sort(compareOptionText),
			};

			saveCustomMakeModelOptions(next);
			return next;
		});
	};

	useEffect(() => {
		if (!isEditMode || !vehicleKey) {
			return;
		}

		const loadVehicle = async () => {
			try {
				const decodedKey = decodeURIComponent(vehicleKey);
				const response = /^\d+$/.test(decodedKey)
					? await vehicleApi.getById(parseInt(decodedKey, 10))
					: await vehicleApi.getByLicensePlate(decodedKey);
				const vehicle = response.data;
				setResolvedVehicleId(vehicle.id);
				setFormData({
					make: vehicle.make,
					model: vehicle.model,
					year: vehicle.year,
					photoDataUrl: vehicle.photoDataUrl,
					vin: vehicle.vin || '',
					licensePlate: vehicle.licensePlate || '',
					purchasePrice: vehicle.purchasePrice,
					purchaseDate: vehicle.purchaseDate.split('T')[0],
					mileage: vehicle.mileage,
					color: vehicle.color || '#000000',
					nickname: vehicle.nickname || '',
					sellPrice: vehicle.sellPrice,
					sellDate: vehicle.sellDate ? vehicle.sellDate.split('T')[0] : undefined,
				});
				setMileageInput(vehicle.mileage ? formatMileage(kmToDisplayDistance(vehicle.mileage)) : '');
			} catch (error) {
				console.error('Error loading vehicle:', error);
				alert('Failed to load vehicle for editing');
				navigate('/');
			} finally {
				setLoading(false);
			}
		};

		loadVehicle();
	}, [isEditMode, navigate, vehicleKey]);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
	const { name, value, type } = e.target;

	if (name === 'make') {
	  setFormData({
		...formData,
		make: value,
		model: '',
	  });
	  return;
	}

	if (name === 'licensePlate') {
	  setFormData({
		...formData,
		licensePlate: value.toUpperCase(),
	  });
	  return;
	}

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
		mileage: displayDistanceToKm(parsedMileage),
	  });
	  setMileageInput(formatMileage(parsedMileage));
	  return;
	}

	if (name === 'sellPrice') {
	  setFormData({
		...formData,
		sellPrice: value === '' ? undefined : parseFloat(value) || 0,
	  });
	  return;
	}

	setFormData({
	  ...formData,
	  [name]: type === 'number' ? parseFloat(value) || 0 : value,
	});
  };

  const handleLookupByLicensePlate = async () => {
	const plate = formData.licensePlate?.trim();
	if (!plate) {
	  setLookupMessage('Enter a license plate first.');
	  return;
	}

	setLookupLoading(true);
	setLookupMessage(null);

	try {
	  const response = await vehicleApi.lookupFromCarInfo(plate);
	  const lookup = response.data;

	  addFetchedMakeModelOption(lookup.make, lookup.model);

	  setFormData((previous) => ({
		...previous,
		make: lookup.make || previous.make,
		model: lookup.model || previous.model,
		year: lookup.year || previous.year,
		color: mapCarInfoColorToHex(lookup.colorName) || previous.color,
		mileage: lookup.mileageKm ?? previous.mileage,
	  }));

	  if (lookup.mileageKm != null) {
		setMileageInput(formatMileage(kmToDisplayDistance(lookup.mileageKm)));
	  }

	  setLookupMessage('Vehicle data fetched from Car.info.');
	} catch (error) {
	  console.error('Error fetching Car.info data:', error);
	  setLookupMessage(getApiErrorMessage(error, 'Could not fetch data from Car.info for this plate.'));
	} finally {
	  setLookupLoading(false);
	}
  };

  const handleSubmit = async (e: React.FormEvent) => {
	e.preventDefault();
	try {
	  if (isEditMode && resolvedVehicleId) {
		await vehicleApi.update(resolvedVehicleId, formData);
		navigate(`/vehicles/${formData.licensePlate ? encodeURIComponent(formData.licensePlate) : resolvedVehicleId}`);
	  } else {
		await vehicleApi.create(formData);
		navigate('/');
	  }
	} catch (error) {
	  console.error('Error saving vehicle:', error);
	  alert('Failed to save vehicle');
	}
  };

	const openColorPicker = () => {
		colorInputRef.current?.click();
	};

	const handlePhotoUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
		const file = event.target.files?.[0];
		if (!file) {
			return;
		}

		if (!file.type.startsWith('image/')) {
			setPhotoError('Please choose an image file.');
			event.target.value = '';
			return;
		}

		if (file.size > MAX_PHOTO_SIZE_BYTES) {
			setPhotoError('Please choose an image smaller than 5 MB.');
			event.target.value = '';
			return;
		}

		try {
			const photoDataUrl = await new Promise<string>((resolve, reject) => {
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
			});

			setFormData((previous) => ({
				...previous,
				photoDataUrl,
			}));
			setPhotoError(null);
		} catch (error) {
			console.error('Error reading vehicle photo:', error);
			setPhotoError('Could not load that image. Try another file.');
		} finally {
			event.target.value = '';
		}
	};

	const clearPhoto = () => {
		setFormData((previous) => ({
			...previous,
			photoDataUrl: undefined,
		}));
		setPhotoError(null);
		if (photoInputRef.current) {
			photoInputRef.current.value = '';
		}
	};

	const setPresetColor = (color: string) => {
		setFormData({
			...formData,
			color,
		});
	};

	const openPhotoPicker = () => {
		photoInputRef.current?.click();
	};

	if (loading) {
		return <div className="loading">Loading vehicle...</div>;
	}

  return (
	<div className="form-container">
	  <h1>{isEditMode ? 'Edit Vehicle' : 'Add New Vehicle'}</h1>
	  <form onSubmit={handleSubmit} className="vehicle-form">
		<div className="form-row">
		  <div className="form-group">
			<label htmlFor="make">Make *</label>
			<select
			  id="make"
			  name="make"
			  value={formData.make}
			  onChange={handleChange}
			  required
			>
			  <option value="">Select a make</option>
			  {makeOptions.map((make) => (
				<option key={make} value={make}>
				  {make}
				</option>
			  ))}
			</select>
		  </div>
		  <div className="form-group">
			<label htmlFor="model">Model *</label>
			<select
			  id="model"
			  name="model"
			  value={formData.model}
			  onChange={handleChange}
			  disabled={!formData.make}
			  required
			>
			  <option value="">Select a model</option>
			  {modelsForSelectedMake.map((model) => (
				<option key={model} value={model}>
				  {model}
				</option>
			  ))}
			</select>
		  </div>
		  <div className="form-group">
			<label htmlFor="year">Year *</label>
			<input
			  type="number"
			  id="year"
			  name="year"
			  value={formData.year}
			  onChange={handleChange}
			  min="1900"
			  max={new Date().getFullYear() + 1}
			  required
			/>
		  </div>
		</div>

		<div className="form-row">
		  <div className="form-group">
			<label htmlFor="nickname">Nickname</label>
			<input
			  type="text"
			  id="nickname"
			  name="nickname"
			  value={formData.nickname}
			  onChange={handleChange}
			  placeholder="e.g. Old Faithful, The Beast"
			/>
		  </div>
		</div>

		<div className="form-row">
		  <div className="form-group form-group-photo">
			<label htmlFor="vehiclePhoto">Photo</label>
			<input
			  ref={photoInputRef}
			  type="file"
			  id="vehiclePhoto"
			  accept="image/*"
			  className="photo-input-hidden"
			  onChange={handlePhotoUpload}
			/>
			<div className="vehicle-photo-editor">
			  <div className="vehicle-photo-preview-shell">
				{formData.photoDataUrl ? (
				  <img
					className="vehicle-photo-preview"
					src={formData.photoDataUrl}
					alt={vehicleDisplayName ? `${vehicleDisplayName} preview` : 'Vehicle preview'}
				  />
				) : (
				  <div className="vehicle-photo-placeholder">No photo selected</div>
				)}
			  </div>
			  <div className="vehicle-photo-actions">
				<button type="button" className="btn-secondary" onClick={openPhotoPicker}>
				  {formData.photoDataUrl ? 'Replace photo' : 'Upload photo'}
				</button>
				{formData.photoDataUrl && (
				  <button type="button" className="btn-secondary" onClick={clearPhoto}>
					Remove photo
				  </button>
				)}
				<small className="field-help">JPG, PNG, WebP or GIF up to 5 MB.</small>
				{photoError && <small className="photo-error-message">{photoError}</small>}
			  </div>
			</div>
		  </div>
		</div>

		<div className="form-row">
		  <div className="form-group">
			<label htmlFor="vin">VIN</label>
			<input
			  type="text"
			  id="vin"
			  name="vin"
			  value={formData.vin}
			  onChange={handleChange}
			  maxLength={17}
			/>
		  </div>
		  <div className="form-group">
			<label htmlFor="licensePlate">License Plate</label>
			<input
			  type="text"
			  id="licensePlate"
			  name="licensePlate"
			  value={formData.licensePlate}
			  onChange={handleChange}
			/>
			{supportsCarInfoLookup && (
			  <button
				type="button"
				className="btn-secondary plate-lookup-btn"
				onClick={handleLookupByLicensePlate}
				disabled={lookupLoading}
			  >
				{lookupLoading ? 'Fetching...' : 'Fetch from plate'}
			  </button>
			)}
			{lookupMessage && <small className="lookup-message">{lookupMessage}</small>}
		  </div>
		</div>

		<div className="form-row">
		  <div className="form-group">
			<label htmlFor="purchasePrice">Purchase Price ({appCurrency}) *</label>
			<input
			  type="number"
			  id="purchasePrice"
			  name="purchasePrice"
			  value={formData.purchasePrice}
			  onChange={handleChange}
			  min="0"
			  step="1"
			  required
			/>
		  </div>
		  <div className="form-group">
			<label htmlFor="purchaseDate">Purchase Date *</label>
			<DatePicker
			  id="purchaseDate"
			  selected={formData.purchaseDate ? new Date(formData.purchaseDate) : null}
			  onChange={(date: Date | null) => {
				setFormData({ ...formData, purchaseDate: date ? date.toISOString().split('T')[0] : '' });
			  }}
			  dateFormat="yyyy-MM-dd"
			  showMonthDropdown
			  showYearDropdown
			  dropdownMode="select"
			  maxDate={new Date()}
			  placeholderText="Select date"
			  calendarStartDay={calendarStartDay}
			  className="datepicker-input"
			  required
			/>
		  </div>
		</div>

		<div className="form-row">
		  <div className="form-group">
			<label htmlFor="mileage">Current Mileage ({distanceLabel})</label>
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
		  <div className="form-group">
			<label htmlFor="color">Color</label>
			<input
			  ref={colorInputRef}
			  type="color"
			  id="color"
			  name="color"
			  className="color-input-hidden"
			  value={selectedColor}
			  onChange={handleChange}
			/>
			<button type="button" className="color-trigger" onClick={openColorPicker}>
			  <span className="color-preview-swatch" style={{ backgroundColor: selectedColor }}></span>
			  <span className="color-trigger-text">Pick custom color</span>
			  <span className="color-preview-value">{selectedColor.toUpperCase()}</span>
			</button>
			<div className="color-swatches">
			  {PRESET_COLORS.map((color) => (
				<button
				  key={color}
				  type="button"
				  className={`swatch-btn${selectedColor.toLowerCase() === color.toLowerCase() ? ' active' : ''}`}
				  style={{ backgroundColor: color }}
				  onClick={() => setPresetColor(color)}
				  aria-label={`Set color ${color}`}
				/>
			  ))}
			</div>
		  </div>
		</div>

		<div className="form-row">
		  <div className="form-group">
			<label htmlFor="sellPrice">Sell Price ({appCurrency})</label>
			<input
			  type="number"
			  id="sellPrice"
			  name="sellPrice"
			  value={formData.sellPrice ?? ''}
			  onChange={handleChange}
			  min="0"
			  step="1"
			  placeholder="Leave empty if not sold"
			/>
		  </div>
		  <div className="form-group">
			<label htmlFor="sellDate">Sell Date</label>
			<DatePicker
			  id="sellDate"
			  selected={formData.sellDate ? new Date(formData.sellDate) : null}
			  onChange={(date: Date | null) => {
				setFormData({ ...formData, sellDate: date ? date.toISOString().split('T')[0] : undefined });
			  }}
			  dateFormat="yyyy-MM-dd"
			  showMonthDropdown
			  showYearDropdown
			  dropdownMode="select"
			  maxDate={new Date()}
			  placeholderText="Select date"
			  calendarStartDay={calendarStartDay}
			  className="datepicker-input"
			  isClearable
			/>
		  </div>
		</div>

		<div className="form-actions">
		  <button
			type="button"
			className="btn-secondary"
			onClick={() => {
			  if (!isEditMode) {
				navigate('/');
				return;
			  }

			  const fallbackKey = resolvedVehicleId ? String(resolvedVehicleId) : '';
			  const routeKey = formData.licensePlate ? encodeURIComponent(formData.licensePlate) : fallbackKey;
			  navigate(routeKey ? `/vehicles/${routeKey}` : '/');
			}}
		  >
			Cancel
		  </button>
		  <button type="submit" className="btn-primary">
			{isEditMode ? 'Save Changes' : 'Add Vehicle'}
		  </button>
		</div>
	  </form>
	</div>
  );
};

export default VehicleForm;
