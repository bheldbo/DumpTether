import { syncCloudBaseUrlStorageKey } from './appTypes';

const configuredDefaultCloudApiBaseUrl = normalizeUrl(
  import.meta.env.VITE_DEFAULT_CLOUD_API_BASE_URL ?? '',
);

export function getDefaultCloudApiBaseUrl() {
  return configuredDefaultCloudApiBaseUrl;
}

export function readCloudSyncApiBaseUrl() {
  if (typeof window === 'undefined') {
    return configuredDefaultCloudApiBaseUrl;
  }

  return normalizeUrl(window.localStorage.getItem(syncCloudBaseUrlStorageKey) ?? '') ||
    configuredDefaultCloudApiBaseUrl;
}

export function writeCloudSyncApiBaseUrl(value: string) {
  if (typeof window === 'undefined') {
    return;
  }

  const normalized = normalizeUrl(value);

  if (normalized) {
    window.localStorage.setItem(syncCloudBaseUrlStorageKey, normalized);
  } else {
    window.localStorage.removeItem(syncCloudBaseUrlStorageKey);
  }
}

function normalizeUrl(value: string) {
  return value.trim().replace(/\/+$/, '');
}
