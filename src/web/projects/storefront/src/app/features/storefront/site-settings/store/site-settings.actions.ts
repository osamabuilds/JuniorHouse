import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { SiteSettings } from '../../../../shared/models/site-settings.model';

export type SettingsSource = 'launch' | 'api' | 'fallback';

export const SiteSettingsActions = createActionGroup({
  source: 'Site settings',
  events: {
    Load: emptyProps(),
    'Load Success': props<{ settings: SiteSettings; source: SettingsSource }>(),
    'Load Failure': props<{ error: string }>(),
  },
});
