import { Injectable, TransferState, inject, makeStateKey } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { launchSettings } from '../../../../shared/data/launch-settings';
import { SiteSettings } from '../../../../shared/models/site-settings.model';
import { validateSettings } from '../../../../shared/utils/validate-settings';
import { SiteSettingsApi } from '../services/site-settings-api.service';
import { SETTINGS_API_URL } from '../site-settings.providers';
import { SiteSettingsActions } from './site-settings.actions';

@Injectable()
export class SiteSettingsEffects {
  static readonly TRANSFER_KEY = makeStateKey<SiteSettings>('site-settings');

  private readonly actions$ = inject(Actions);
  private readonly api = inject(SiteSettingsApi);
  private readonly url = inject(SETTINGS_API_URL);
  private readonly transfer = inject(TransferState);

  readonly load$ = createEffect(() =>
    this.actions$.pipe(
      ofType(SiteSettingsActions.load),
      switchMap(() => {
        const transferred = this.transfer.get(SiteSettingsEffects.TRANSFER_KEY, null);
        if (transferred) return of(SiteSettingsActions.loadSuccess({ settings: transferred, source: 'api' }));
        if (!this.url) return of(SiteSettingsActions.loadSuccess({ settings: launchSettings, source: 'launch' }));
        return this.api.get().pipe(
          map((settings) => {
            const problems = validateSettings(settings);
            if (problems.length > 0) throw new Error(`invalid settings: ${problems[0]}`);
            return settings;
          }),
          tap((settings) => this.transfer.set(SiteSettingsEffects.TRANSFER_KEY, settings)),
          map((settings) => SiteSettingsActions.loadSuccess({ settings, source: 'api' })),
          catchError((e: unknown) => of(SiteSettingsActions.loadFailure({ error: e instanceof Error ? e.message : 'unknown error' }))),
        );
      }),
    ),
  );
}
