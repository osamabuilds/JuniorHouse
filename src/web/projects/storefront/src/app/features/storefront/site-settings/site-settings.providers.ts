import { EnvironmentProviders, InjectionToken, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { SiteSettingsEffects } from './store/site-settings.effects';
import { siteSettingsFeature } from './store/site-settings.reducer';

/** URL of the published settings document, or null to use the built-in launch set (until SCRUM-206 is deployed). */
export const SETTINGS_API_URL = new InjectionToken<string | null>('SETTINGS_API_URL', { providedIn: 'root', factory: () => null });

export function provideSiteSettings(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(siteSettingsFeature), provideEffects(SiteSettingsEffects)]);
}
