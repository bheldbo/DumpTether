export interface DesktopRuntimeConfiguration {
  apiBaseUrl: string;
  bootstrapToken: string;
}

declare global {
  interface Window {
    __DUMPTETHER_DESKTOP_RUNTIME__?: DesktopRuntimeConfiguration;
  }
}

export function getDesktopRuntimeConfiguration() {
  return typeof window === 'undefined'
    ? undefined
    : window.__DUMPTETHER_DESKTOP_RUNTIME__;
}

export function isDesktopRuntime() {
  if (typeof window === 'undefined') {
    return false;
  }

  const desktopWindow = window as Window & {
    __TAURI_INTERNALS__?: unknown;
    __TAURI__?: unknown;
  };

  return Boolean(
    getDesktopRuntimeConfiguration() ||
    desktopWindow.__TAURI_INTERNALS__ ||
    desktopWindow.__TAURI__,
  ) ||
    window.location.protocol === 'tauri:' ||
    window.location.protocol === 'file:' ||
    window.location.hostname === 'tauri.localhost';
}
