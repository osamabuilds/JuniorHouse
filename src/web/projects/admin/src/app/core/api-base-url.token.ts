import { InjectionToken } from '@angular/core';

declare global {
  interface Window {
    readonly __env?: { readonly apiBaseUrl?: string };
  }
}

/**
 * The Romp API's origin. Read from `window.__env` (public/env.js, loaded before the Angular
 * bundle in index.html) rather than baked into the build, so the same compiled app can point at a
 * different API per environment - `ng serve` uses env.js's http://localhost:5051 default;
 * Dockerfile.admin overwrites env.js from the API_BASE_URL container env var at startup.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  factory: () => window.__env?.apiBaseUrl ?? 'http://localhost:5051',
});
