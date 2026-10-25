import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { map, switchMap } from 'rxjs';
import { SearchApi } from '../services/search-api.service';
import { SearchResultsActions } from './search-results.actions';

@Injectable()
export class SearchResultsEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(SearchApi);

  /** switchMap: a newer search cancels the request for an older one. The browser reuses the server's response through the HTTP transfer cache. */
  readonly load$ = createEffect(() =>
    this.actions$.pipe(
      ofType(SearchResultsActions.load),
      switchMap(({ key, query, filter }) => this.api.find(query, filter).pipe(map((result) => SearchResultsActions.loadDone({ key, result })))),
    ),
  );
}
