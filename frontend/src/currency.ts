const SUPPORTED_REGIONS = {
  sweden: { currency: 'SEK', locale: 'sv-SE' },
  norway: { currency: 'NOK', locale: 'nb-NO' },
  europe: { currency: 'EUR', locale: 'de-DE' },
} as const;

type SupportedRegion = keyof typeof SUPPORTED_REGIONS;

const normalizeRegion = (value: string | undefined): SupportedRegion => {
  const region = (value ?? '').trim().toLowerCase();
  if (region === 'norway' || region === 'europe') {
    return region;
  }

  return 'sweden';
};

const activeRegion = normalizeRegion(window.__APP_REGION__ ?? process.env.REACT_APP_REGION);
const regionSettings = SUPPORTED_REGIONS[activeRegion];

export const appRegion = activeRegion;
export const appCurrency = window.__APP_CURRENCY__ ?? regionSettings.currency;
export const appLocale = regionSettings.locale;

export const formatCurrency = (amount: number, options?: Intl.NumberFormatOptions) => {
  return new Intl.NumberFormat(appLocale, {
    style: 'currency',
    currency: appCurrency,
    ...options,
  }).format(amount);
};

export const formatWholeCurrency = (amount: number) => {
  return formatCurrency(amount, {
    minimumFractionDigits: 0,
    maximumFractionDigits: 0,
  });
};
