export interface Vehicle {
  id: number;
  make: string;
  model: string;
  year: number;
  photoDataUrl?: string;
  vin?: string;
  licensePlate?: string;
  purchasePrice: number;
  purchaseDate: string;
  mileage?: number;
  color?: string;
  nickname?: string;
  sellPrice?: number;
  sellDate?: string;
  totalExpenses: number;
  expenseCount: number;
}

export interface CreateVehicleDto {
  make: string;
  model: string;
  year: number;
  photoDataUrl?: string;
  vin?: string;
  licensePlate?: string;
  purchasePrice: number;
  purchaseDate: string;
  mileage?: number;
  color?: string;
  nickname?: string;
  sellPrice?: number;
  sellDate?: string;
}

export interface VehicleLookupDto {
  licensePlate: string;
  make?: string;
  model?: string;
  year?: number;
  colorName?: string;
  mileageKm?: number;
  fuelType?: string;
  gearbox?: string;
  driveTrain?: string;
  sourceUrl: string;
}

export interface VehicleLookupCacheDto extends VehicleLookupDto {
  id: number;
  createdAt: string;
  fetchedAt: string;
  updatedAt?: string;
  vin?: string;
  inTraffic?: string;
  swedishSold?: string;
  colorName?: string;
  ownerCount?: string;
  mileageKm?: number;
  bodyType?: string;
  classification?: string;
  generation?: string;
  engine?: string;
  fuelType?: string;
  gearbox?: string;
  driveTrain?: string;
  fuelConsumptionMixed?: string;
  co2Mixed?: string;
  cargoVolume?: string;
  seatCount?: string;
  specifications: Record<string, string>;
  sourceUrl: string;
}

export interface ExpenseExportDto {
  type: ExpenseType;
  description: string;
  photoDataUrls: string[];
  amount: number;
  date: string;
  mileage?: number;
  vendor?: string;
  notes?: string;
  shipping?: number;
}

export interface VehicleExportDto {
  make: string;
  model: string;
  year: number;
  photoDataUrl?: string;
  vin?: string;
  licensePlate?: string;
  purchasePrice: number;
  purchaseDate: string;
  mileage?: number;
  color?: string;
  nickname?: string;
  sellPrice?: number;
  sellDate?: string;
}

export interface VehicleExportPackageDto {
  schemaVersion: number;
  exportedAtUtc: string;
  vehicle: VehicleExportDto;
  expenses: ExpenseExportDto[];
}

export interface Expense {
  id: number;
  vehicleId: number;
  vehicleName: string;
  type: ExpenseType;
  typeName: string;
  description: string;
  photoDataUrls: string[];
  amount: number;
  date: string;
  mileage?: number;
  vendor?: string;
  notes?: string;
  shipping?: number;
}

export interface CreateExpenseDto {
  vehicleId: number;
  type: ExpenseType;
  description: string;
  photoDataUrls: string[];
  amount: number;
  date: string;
  mileage?: number;
  vendor?: string;
  notes?: string;
  shipping?: number;
}

export enum ExpenseType {
  Fuel = 1,
  Maintenance = 2,
  Repair = 3,
  Insurance = 4,
  Registration = 5,
  Parking = 6,
  Tolls = 7,
  Wash = 8,
  SpareParts = 10,
  Other = 9
}

export interface AppSetupStatusDto {
  setupRequired: boolean;
  dataDirectoryPath: string;
  configFilePath: string;
  currentRegion: string;
  currentCurrency?: string;
  currentDistanceUnit?: string;
  currentPort: number;
  debugSavePlaywrightHtml: boolean;
  isContainer: boolean;
}

export interface AppConfigurationDto {
  region: string;
  dataDirectoryPath: string;
  configFilePath: string;
  debugSavePlaywrightHtml: boolean;
}

export interface SaveAppConfigurationDto {
  region: string;
  currency?: string;
  distanceUnit?: string;
  port?: number;
  debugSavePlaywrightHtml?: boolean;
}

export interface SaveAppConfigurationResultDto {
  saved: boolean;
  configFilePath: string;
}
