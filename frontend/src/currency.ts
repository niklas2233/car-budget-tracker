const SUPPORTED_REGIONS = {
  sweden:  { currency: 'SEK', locale: 'sv-SE', distanceUnit: 'km',    calendarStart: 1 },
  norway:  { currency: 'NOK', locale: 'nb-NO', distanceUnit: 'km',    calendarStart: 1 },
  europe:  { currency: 'EUR', locale: 'en-GB', distanceUnit: 'km',    calendarStart: 1 },
  america: { currency: 'USD', locale: 'en-US', distanceUnit: 'km',    calendarStart: 0 },
  usa:     { currency: 'USD', locale: 'en-US', distanceUnit: 'miles', calendarStart: 0 },
  gb:      { currency: 'GBP', locale: 'en-GB', distanceUnit: 'miles', calendarStart: 1 },
} as const;

type SupportedRegion = keyof typeof SUPPORTED_REGIONS;

const normalizeRegion = (value: string | undefined): SupportedRegion => {
  const region = (value ?? '').trim().toLowerCase() as SupportedRegion;
  if (region in SUPPORTED_REGIONS) {
    return region;
  }

  return 'sweden';
};

const activeRegion = normalizeRegion(window.__APP_REGION__ ?? process.env.REACT_APP_REGION);
const regionSettings = SUPPORTED_REGIONS[activeRegion];

const KM_PER_MILE = 1.60934;

export const appRegion = activeRegion;
export const appCurrency = window.__APP_CURRENCY__ ?? regionSettings.currency;
export const appLocale = regionSettings.locale;
export const calendarStartDay: 0 | 1 = regionSettings.calendarStart as 0 | 1;
export const useMiles = regionSettings.distanceUnit === 'miles';
export const supportsCarInfoLookup = activeRegion === 'sweden' || activeRegion === 'norway';
export const distanceLabel = regionSettings.distanceUnit;

export const kmToDisplayDistance = (km: number): number =>
  useMiles ? Math.round(km / KM_PER_MILE) : km;

export const displayDistanceToKm = (distance: number): number =>
  useMiles ? Math.round(distance * KM_PER_MILE) : distance;

export const formatDistance = (km: number): string =>
  `${new Intl.NumberFormat(appLocale, { maximumFractionDigits: 0 }).format(kmToDisplayDistance(km))} ${distanceLabel}`;

export const formatCurrency = (amount: number, options?: Intl.NumberFormatOptions) => {
  return new Intl.NumberFormat(appLocale, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
    ...options,
  }).format(amount);
};

export const formatWholeCurrency = (amount: number) => {
  return formatCurrency(amount, {
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  });
};
