import { TestBed } from '@angular/core/testing';
import { Store, provideStore } from '@ngrx/store';
import { provideEffects } from '@ngrx/effects';
import { of } from 'rxjs';
import { afterEach, beforeEach, vi } from 'vitest';
import { SearchApi } from '../services/search-api.service';
import { fakeSuggest } from '../testing';
import { SearchSuggestActions } from './search-suggest.actions';
import { SearchSuggestEffects } from './search-suggest.effects';
import { MAX_SUGGESTIONS, searchSuggestFeature } from './search-suggest.reducer';
import { selectSuggestions } from './search-suggest.selectors';

describe('searchSuggest', () => {
  const suggest = vi.fn((q: string) => of(fakeSuggest(q)));

  beforeEach(() => {
    vi.useFakeTimers();
    suggest.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideStore({ [searchSuggestFeature.name]: searchSuggestFeature.reducer }),
        provideEffects(SearchSuggestEffects),
        { provide: SearchApi, useValue: { suggest } },
      ],
    });
  });
  afterEach(() => vi.useRealTimers());

  it('SC-9 waits 200 ms after the last keystroke and makes one request for the last text', () => {
    const store = TestBed.inject(Store);
    store.dispatch(SearchSuggestActions.queryChanged({ query: 's' }));
    vi.advanceTimersByTime(100);
    store.dispatch(SearchSuggestActions.queryChanged({ query: 'sh' }));
    vi.advanceTimersByTime(100);
    store.dispatch(SearchSuggestActions.queryChanged({ query: 'shirt' }));
    vi.advanceTimersByTime(199);
    expect(suggest).not.toHaveBeenCalled();
    vi.advanceTimersByTime(1);
    expect(suggest).toHaveBeenCalledTimes(1);
    expect(suggest).toHaveBeenCalledWith('shirt');
  });

  it('SRC-1 records what was searched and holds at most the eight the API sends', () => {
    const store = TestBed.inject(Store);
    store.dispatch(SearchSuggestActions.queryChanged({ query: 'a' }));
    vi.advanceTimersByTime(200);
    const s = store.selectSignal(selectSuggestions)();
    expect(s.results.length).toBeGreaterThan(0);
    expect(s.results.length).toBeLessThanOrEqual(MAX_SUGGESTIONS);
    expect(s.searched).toBe('a');

    const many = Array.from({ length: 20 }, (_, i) => ({ kind: 'product' as const, label: `P${i}`, path: `/p/${i}`, image: null, price: 1 }));
    store.dispatch(SearchSuggestActions.resultsLoaded({ query: 'many', result: { query: 'many', items: many } }));
    expect(store.selectSignal(selectSuggestions)().results.length).toBe(MAX_SUGGESTIONS);
  });

  it('SC-10 an empty query clears the suggestions without a request', () => {
    const store = TestBed.inject(Store);
    store.dispatch(SearchSuggestActions.queryChanged({ query: 'shirt' }));
    vi.advanceTimersByTime(200);
    suggest.mockClear();
    store.dispatch(SearchSuggestActions.queryChanged({ query: '   ' }));
    vi.advanceTimersByTime(200);
    expect(suggest).not.toHaveBeenCalled();
    expect(store.selectSignal(selectSuggestions)().results).toEqual([]);
    expect(store.selectSignal(selectSuggestions)().searched).toBe('');
  });

  it('SRC-3 trims and caps very long input at 100 characters', () => {
    const store = TestBed.inject(Store);
    store.dispatch(SearchSuggestActions.queryChanged({ query: '  ' + 'x'.repeat(300) }));
    vi.advanceTimersByTime(200);
    expect(suggest.mock.calls[0][0].length).toBe(100);
  });
});
