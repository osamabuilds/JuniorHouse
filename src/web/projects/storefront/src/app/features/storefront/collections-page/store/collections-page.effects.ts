import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { map, switchMap } from 'rxjs';
import { CollectionsPageApi } from '../services/collections-page-api.service';
import { CollectionsPageActions } from './collections-page.actions';

@Injectable()
export class CollectionsPageEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(CollectionsPageApi);

  /** switchMap: a newer page cancels the request for an older one. The browser reuses the server's response through the HTTP transfer cache. */
  readonly load$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CollectionsPageActions.load),
      switchMap(({ key, page }) => this.api.get(page).pipe(map((result) => CollectionsPageActions.loadDone({ key, result })))),
    ),
  );
}
