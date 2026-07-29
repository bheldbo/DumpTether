import { deploymentTarget } from './generated/deploymentTarget';
import { isDesktopRuntime } from './clientRuntime';

const configuredDefaultCloudApiBaseUrl = normalizeUrl(
  deploymentTarget.cloudApiBaseUrl,
);

export function readCloudSyncApiBaseUrl() {
  if (!isDesktopRuntime() && typeof window !== 'undefined') {
    return window.location.origin;
  }

  return configuredDefaultCloudApiBaseUrl;
}

function normalizeUrl(value: string) {
  return value.trim().replace(/\/+$/, '');
}
