import axios from 'axios';
import { Vehicle, CreateVehicleDto, Expense, CreateExpenseDto, VehicleLookupDto, VehicleLookupCacheDto, VehicleExportPackageDto } from './types';

const API_BASE_URL = process.env.REACT_APP_API_BASE_URL || '/api';

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
	'Content-Type': 'application/json',
  },
});

export const getApiErrorMessage = (error: unknown, fallbackMessage: string) => {
  if (axios.isAxiosError(error)) {
  const responseData = error.response?.data;

  if (typeof responseData === 'string' && responseData.trim()) {
    return responseData;
  }

  if (responseData && typeof responseData === 'object' && 'title' in responseData && typeof responseData.title === 'string') {
    return responseData.title;
  }

  if (error.message) {
    return error.message;
  }
  }

  if (error instanceof Error && error.message) {
  return error.message;
  }

  return fallbackMessage;
};

export const vehicleApi = {
  getAll: () => api.get<Vehicle[]>('/vehicles'),
  getById: (id: number) => api.get<Vehicle>(`/vehicles/${id}`),
  getByLicensePlate: (licensePlate: string) => api.get<Vehicle>(`/vehicles/by-license-plate/${encodeURIComponent(licensePlate)}`),
  exportVehicle: (id: number) => api.get<VehicleExportPackageDto>(`/vehicles/${id}/export`),
  importVehicle: (payload: VehicleExportPackageDto) => api.post<Vehicle>('/vehicles/import', payload),
  lookupFromCarInfo: (licensePlate: string) => api.get<VehicleLookupDto>(`/vehicles/lookup-car-info/${encodeURIComponent(licensePlate)}`),
  refreshFromCarInfo: (licensePlate: string) => api.post<VehicleLookupDto>(`/vehicles/lookup-car-info/${encodeURIComponent(licensePlate)}/refresh`),
  getLookupCache: () => api.get<VehicleLookupCacheDto[]>('/vehicles/lookup-cache'),
  create: (vehicle: CreateVehicleDto) => api.post<Vehicle>('/vehicles', vehicle),
  update: (id: number, vehicle: CreateVehicleDto) => api.put(`/vehicles/${id}`, vehicle),
  delete: (id: number) => api.delete(`/vehicles/${id}`),
};

export const expenseApi = {
  getAll: () => api.get<Expense[]>('/expenses'),
  getByVehicleId: (vehicleId: number) => api.get<Expense[]>(`/expenses/vehicle/${vehicleId}`),
  getById: (id: number) => api.get<Expense>(`/expenses/${id}`),
  create: (expense: CreateExpenseDto) => api.post<Expense>('/expenses', expense),
  update: (id: number, expense: Partial<CreateExpenseDto>) => api.put(`/expenses/${id}`, expense),
  delete: (id: number) => api.delete(`/expenses/${id}`),
};
