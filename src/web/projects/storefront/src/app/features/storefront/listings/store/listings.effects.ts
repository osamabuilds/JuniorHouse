import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { map, switchMap } from 'rxjs';
import { ListingsApi } from '../services/listings-api.service';
import { ListingsActions } from './listings.actions';

@Injectable()
export class ListingsEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(ListingsApi);

  /** switchMap: a newer filter cancels the request for an older one. The browser reuses the server's response through the HTTP transfer cache. */
  readonly load$ = createEffect(() =>
    this.actions$.pipe(
      ofType(ListingsActions.load),
      switchMap(({ key, scope, filter }) => this.api.get(scope, filter).pipe(map((result) => ListingsActions.loadDone({ key, result })))),
    ),
  );
}
