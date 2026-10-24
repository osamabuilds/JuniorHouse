import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store, createSelector } from '@ngrx/store';
import { filter, map, mergeMap, of, take } from 'rxjs';
import { launchPages } from '../../../../shared/data/launch-pages';
import { SETTINGS_API_URL } from '../../site-settings/site-settings.providers';
import { siteSettingsFeature } from '../../site-settings/store/site-settings.reducer';
import { selectSettings } from '../../site-settings/store/site-settings.selectors';
import { PageApi, PageResult } from '../services/page-api.service';
import { PageActions } from './page.actions';

const selectLoaded = createSelector(siteSettingsFeature.selectSource, siteSettingsFeature.selectStatus, (source, status) => ({ source, status }));

@Injectable()
export class PageEffects {
  private readonly actions$ = inject(Actions);
  private readonly store = inject(Store);
  private readonly api = inject(PageApi);
  private readonly url = inject(SETTINGS_API_URL);

  readonly load$ = createEffect(() =>
    this.actions$.pipe(
      ofType(PageActions.load),
      mergeMap(({ slug }) =>
        // Wait for the settings to settle: the source tells whether anything is published yet.
        this.store.select(selectLoaded).pipe(
          filter((loaded) => loaded.status === 'loaded' || loaded.status === 'failed'),
          take(1),
          mergeMap(({ source }) => (this.url && source === 'api' ? this.api.get(slug) : this.fromLaunchSet(slug))),
          map((result) => PageActions.loaded({ slug, result })),
        ),
      ),
    ),
  );

  /** Until something is published (settings source "launch" or "fallback"): the seeded drafts, gated by the status the settings give each page. */
  private fromLaunchSet(slug: string) {
    return this.store.select(selectSettings).pipe(
      take(1),
      mergeMap((settings) => {
        const summary = settings.pages.find((p) => p.slug === slug);
        const page = launchPages[slug];
        const result: PageResult =
          summary?.status === 'archived' ? { status: 'gone' } : summary?.status === 'published' && page ? { status: 'ok', page } : { status: 'notFound' };
        return of(result);
      }),
    );
  }
}
