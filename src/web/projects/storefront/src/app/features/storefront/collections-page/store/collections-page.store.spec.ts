import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { Observable, Subject, of } from 'rxjs';
import { describe, expect, it } from 'vitest';
import { CollectionsPageResult } from '../models/collections-page.model';
import { CollectionsPageApi } from '../services/collections-page-api.service';
import { provideCollectionsPage } from '../collections-page.providers';
import { CollectionsPageActions } from './collections-page.actions';
import { selectCollectionsKey, selectCollectionsLoading, selectCollectionsResult } from './collections-page.selectors';

const ok = (names: string[], page = 1): CollectionsPageResult => ({
  status: 'ok',
  page: { items: names.map((n) => ({ code: n.toUpperCase(), name: n, path: '/collections/' + n.toLowerCase(), pieces: 3, image: null })), total: names.length, page, pageSize: 24, totalPages: 1 },
});

function setUp(get: (page: number) => Observable<CollectionsPageResult>) {
  TestBed.configureTestingModule({ providers: [provideStore(), provideEffects(), provideCollectionsPage(), { provide: CollectionsPageApi, useValue: { get } }] });
  return TestBed.inject(Store);
}

describe('collections page store', () => {
  it('COL-1 loading puts the server result in the store under its key', () => {
    const store = setUp(() => of(ok(['Summer', 'Winter'])));
    store.dispatch(CollectionsPageActions.load({ key: '1', page: 1 }));
    expect(store.selectSignal(selectCollectionsKey)()).toBe('1');
    expect(store.selectSignal(selectCollectionsLoading)()).toBe(false);
    const result = store.selectSignal(selectCollectionsResult)();
    expect(result?.status === 'ok' && result.page.items.map((i) => i.name)).toEqual(['Summer', 'Winter']);
  });

  it('while a new page loads the previous result stays, and the loading flag is on', () => {
    const pending = new Subject<CollectionsPageResult>();
    let call = 0;
    const store = setUp(() => (call++ === 0 ? of(ok(['Summer'])) : pending));
    store.dispatch(CollectionsPageActions.load({ key: '1', page: 1 }));
    store.dispatch(CollectionsPageActions.load({ key: '2', page: 2 }));
    expect(store.selectSignal(selectCollectionsLoading)()).toBe(true);
    expect(store.selectSignal(selectCollectionsResult)()?.status).toBe('ok');
    pending.next(ok(['Winter'], 2));
    expect(store.selectSignal(selectCollectionsLoading)()).toBe(false);
    expect(store.selectSignal(selectCollectionsKey)()).toBe('2');
  });

  it('a result for an older request is ignored when a newer one has been asked for', () => {
    const first = new Subject<CollectionsPageResult>();
    const second = new Subject<CollectionsPageResult>();
    let call = 0;
    const store = setUp(() => (call++ === 0 ? first : second));
    store.dispatch(CollectionsPageActions.load({ key: '1', page: 1 }));
    store.dispatch(CollectionsPageActions.load({ key: '2', page: 2 }));
    // switchMap cancels the first request; even if it answered late, the reducer would ignore a result for another key.
    first.next(ok(['Old']));
    store.dispatch(CollectionsPageActions.loadDone({ key: '1', result: ok(['Late']) }));
    second.next(ok(['New'], 2));
    const result = store.selectSignal(selectCollectionsResult)();
    expect(result?.status === 'ok' && result.page.items[0].name).toBe('New');
  });

  it('an error from the server is a result, not an exception', () => {
    const store = setUp(() => of<CollectionsPageResult>({ status: 'error' }));
    store.dispatch(CollectionsPageActions.load({ key: '1', page: 1 }));
    expect(store.selectSignal(selectCollectionsResult)()?.status).toBe('error');
  });
});
