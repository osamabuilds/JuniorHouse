import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { debounceTime, map, of, switchMap } from 'rxjs';
import { MAX_QUERY_LENGTH } from '../models/search-query';
import { SearchApi } from '../services/search-api.service';
import { SearchSuggestActions } from './search-suggest.actions';

export const DEBOUNCE_MS = 200;
export { MAX_QUERY_LENGTH };

@Injectable()
export class SearchSuggestEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(SearchApi);

  /** One request per pause in typing, for the last text only (a newer one cancels an older one); an empty query clears instead of asking. */
  readonly query$ = createEffect(() =>
    this.actions$.pipe(
      ofType(SearchSuggestActions.queryChanged),
      debounceTime(DEBOUNCE_MS),
      map(({ query }) => query.trim().slice(0, MAX_QUERY_LENGTH)),
      switchMap((query) =>
        query === ''
          ? of(SearchSuggestActions.cleared({}))
          : this.api.suggest(query).pipe(map((result) => SearchSuggestActions.resultsLoaded({ query, result }))),
      ),
    ),
  );
}
