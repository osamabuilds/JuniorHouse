import { Injectable, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { debounceTime, map, of, switchMap } from 'rxjs';
import { SearchSuggestService } from '../services/search-suggest.service';
import { SearchSuggestActions } from './search-suggest.actions';

export const DEBOUNCE_MS = 200;
export const MAX_QUERY_LENGTH = 100;

@Injectable()
export class SearchSuggestEffects {
  private readonly actions$ = inject(Actions);
  private readonly service = inject(SearchSuggestService);

  /** One request per pause in typing, for the last text only; an empty query clears instead of asking. */
  readonly query$ = createEffect(() =>
    this.actions$.pipe(
      ofType(SearchSuggestActions.queryChanged),
      debounceTime(DEBOUNCE_MS),
      map(({ query }) => query.trim().slice(0, MAX_QUERY_LENGTH)),
      switchMap((query) =>
        query === ''
          ? of(SearchSuggestActions.cleared({}))
          : this.service.suggest(query).pipe(map((results) => SearchSuggestActions.resultsLoaded({ query, results }))),
      ),
    ),
  );
}
