import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { map, mergeMap, of, take } from 'rxjs';
import { launchPages } from '../../../../shared/data/launch-pages';
import { SETTINGS_API_URL } from '../../site-settings/site-settings.providers';
import { selectSettings } from '../../site-settings/store/site-settings.selectors';
import { PageApi, PageResult } from '../services/page-api.service';
import { PageActions } from './page.actions';

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
        (this.url ? this.api.get(slug) : this.fromLaunchSet(slug)).pipe(map((result) => PageActions.loaded({ slug, result }))),
      ),
    ),
  );

  /** Until the settings API exists: the seeded drafts, gated by the status the settings give each page. */
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
