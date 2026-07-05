const configuredDefaultCloudApiBaseUrl = normalizeUrl(
  import.meta.env.VITE_DEFAULT_CLOUD_API_BASE_URL ?? '',
);

export function readCloudSyncApiBaseUrl() {
  return configuredDefaultCloudApiBaseUrl;
}

function normalizeUrl(value: string) {
  return value.trim().replace(/\/+$/, '');
}
