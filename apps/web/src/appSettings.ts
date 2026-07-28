import { deploymentTarget } from './generated/deploymentTarget';

const configuredDefaultCloudApiBaseUrl = normalizeUrl(
  deploymentTarget.cloudApiBaseUrl,
);

export function readCloudSyncApiBaseUrl() {
  return configuredDefaultCloudApiBaseUrl;
}

function normalizeUrl(value: string) {
  return value.trim().replace(/\/+$/, '');
}
