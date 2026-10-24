import { createFeature, createReducer, on } from '@ngrx/store';
import { launchSettings } from '../../../../shared/data/launch-settings';
import { SiteSettings } from '../../../../shared/models/site-settings.model';
import { SettingsSource, SiteSettingsActions } from './site-settings.actions';

export interface SiteSettingsState {
  readonly settings: SiteSettings;
  readonly source: SettingsSource;
  readonly status: 'idle' | 'loading' | 'loaded' | 'failed';
}

/** Never empty: the launch set is the starting point and the last resort (ST-13). */
export const initialSiteSettingsState: SiteSettingsState = { settings: launchSettings, source: 'launch', status: 'idle' };

export const siteSettingsReducer = createReducer(
  initialSiteSettingsState,
  on(SiteSettingsActions.load, (s): SiteSettingsState => ({ ...s, status: 'loading' })),
  on(SiteSettingsActions.loadSuccess, (_, { settings, source }): SiteSettingsState => ({ settings, source, status: 'loaded' })),
  on(SiteSettingsActions.loadFailure, (s): SiteSettingsState => ({ ...s, source: 'fallback', status: 'failed' })),
);

export const siteSettingsFeature = createFeature({ name: 'siteSettings', reducer: siteSettingsReducer });
