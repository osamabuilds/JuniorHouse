import { InjectionToken } from '@angular/core';

declare global {
  interface Window {
    readonly __env?: { readonly apiBaseUrl?: string };
  }
}

/**
 * The Romp API's origin. Read from `window.__env` (public/env.js, loaded before the Angular
 * bundle in index.html) rather than baked into the build, so the same compiled app can point at a
 * different API per environment. The default is empty: the app calls its own origin, where `ng serve` (proxy.conf.json) and the
 * admin container (nginx) forward /api to the API, so the session cookies are first-party (sessions AC-1, G9);
 * Dockerfile.admin overwrites env.js from the API_BASE_URL container env var at startup.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => window.__env?.apiBaseUrl ?? '',
});
