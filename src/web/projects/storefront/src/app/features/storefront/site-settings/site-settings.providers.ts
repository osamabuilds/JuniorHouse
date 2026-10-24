import { EnvironmentProviders, InjectionToken, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { SiteSettingsEffects } from './store/site-settings.effects';
import { siteSettingsFeature } from './store/site-settings.reducer';

/**
 * URL of the published settings document (SCRUM-206). Set it to null to use the built-in launch set only. Until the first publish the
 * endpoint answers 404, and the storefront keeps serving the built-in launch set (source "fallback").
 */
export const SETTINGS_API_URL = new InjectionToken<string | null>('SETTINGS_API_URL', { providedIn: 'root', factory: () => '/api/storefront/settings' });

export function provideSiteSettings(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(siteSettingsFeature), provideEffects(SiteSettingsEffects)]);
}
