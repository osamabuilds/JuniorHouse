import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { missRow, synonymGroup } from '@app/testing/search-fixtures';
import { pageOf } from '@app/testing/warehouse-fixtures';
import { SearchAdminApiActions as Api, SearchAdminPageActions as Ui } from './search.actions';
import * as effects from './search.effects';
import { searchAdminReducer } from './search.reducer';
import * as select from './search.selectors';
import { SEARCH_FEATURE_KEY } from './search.state';

const ROOT = '/api/admin/storefront/search';

describe('storefront search admin store (spec SRC-12, SRC-13)', () => {
  let store: Store;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideStore({ [SEARCH_FEATURE_KEY]: searchAdminReducer }), provideEffects(effects)],
    });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const url = (path: string) => (r: { url: string }) => r.url.endsWith(`${ROOT}${path}`);
  const value = { name: 'bags', isActive: true, terms: ['bag', 'tote'] };

  it('SRC-12 opening the screen loads the first page of groups', () => {
    store.dispatch(Ui.synonymsOpened());
    expect(store.selectSignal(select.selectSynonymsLoading)()).toBe(true);

    const request = http.expectOne(url('/synonyms'));
    expect(request.request.params.get('page')).toBe('1');
    request.flush(pageOf([synonymGroup(), synonymGroup({ id: 2, name: 'kurta' })], { totalCount: 2 }));

    expect(store.selectSignal(select.selectSynonyms)().map((g) => g.name)).toEqual(['dress', 'kurta']);
    expect(store.selectSignal(select.selectSynonymsTotal)()).toBe(2);
    expect(store.selectSignal(select.selectSynonymsLoading)()).toBe(false);
  });

  it('SRC-12 changing the page asks for that page, and the size resets to the first page', () => {
    store.dispatch(Ui.synonymsPageChanged({ page: 3 }));
    expect(http.expectOne(url('/synonyms')).request.params.get('page')).toBe('3');
    store.dispatch(Ui.synonymsPageSizeChanged({ pageSize: 25 }));
    const request = http.expectOne(url('/synonyms'));
    expect([request.request.params.get('page'), request.request.params.get('pageSize')]).toEqual(['1', '25']);
  });

  it('SRC-12 creating posts the group, returns to the list and reloads it', () => {
    store.dispatch(Ui.createStarted());
    expect(store.selectSignal(select.selectSearchMode)()).toBe('create');

    store.dispatch(Ui.createSubmitted({ value }));
    expect(store.selectSignal(select.selectSearchSaving)()).toBe(true);
    const post = http.expectOne((r) => r.method === 'POST' && r.url.endsWith(`${ROOT}/synonyms`));
    expect(post.request.body).toEqual(value);
    post.flush(synonymGroup({ id: 9, ...value, terms: value.terms }));

    expect(store.selectSignal(select.selectSearchMode)()).toBe('list');
    expect(store.selectSignal(select.selectSearchSaving)()).toBe(false);
    http.expectOne((r) => r.method === 'GET' && r.url.endsWith(`${ROOT}/synonyms`)).flush(pageOf([]));
  });

  it('SRC-12 updating quotes the row version that was loaded', () => {
    const group = synonymGroup({ rowVersion: 42 });
    store.dispatch(Ui.editStarted({ group }));

    store.dispatch(Ui.updateSubmitted({ id: group.id, rowVersion: group.rowVersion, value }));
    const put = http.expectOne((r) => r.method === 'PUT' && r.url.endsWith(`${ROOT}/synonyms/1`));
    expect(put.request.body).toEqual({ rowVersion: 42, ...value });
    put.flush(group);
    http.expectOne((r) => r.method === 'GET').flush(pageOf([]));
  });

  it('SRC-12 a refused save stays on the form with the server messages', () => {
    store.dispatch(Ui.createStarted());
    store.dispatch(Ui.createSubmitted({ value }));

    http.expectOne((r) => r.method === 'POST').flush(
      { title: 'One or more validation errors occurred.', errors: { terms: ['"bag" already belongs to another group.'] } },
      { status: 400, statusText: 'Bad Request' },
    );

    expect(store.selectSignal(select.selectSearchMode)()).toBe('create');
    expect(store.selectSignal(select.selectSearchSaving)()).toBe(false);
    expect(store.selectSignal(select.selectSearchFormError)()?.fieldErrors['terms']).toEqual(['"bag" already belongs to another group.']);
  });

  it('SRC-12 deleting takes two steps: ask, then confirm; then the list reloads', () => {
    store.dispatch(Ui.deleteRequested({ id: 7 }));
    expect(store.selectSignal(select.selectDeletingId)()).toBe(7);
    store.dispatch(Ui.deleteCancelled());
    expect(store.selectSignal(select.selectDeletingId)()).toBeNull();

    store.dispatch(Ui.deleteRequested({ id: 7 }));
    store.dispatch(Ui.deleteConfirmed({ id: 7 }));
    http.expectOne((r) => r.method === 'DELETE' && r.url.endsWith(`${ROOT}/synonyms/7`)).flush(null, { status: 204, statusText: 'No Content' });

    expect(store.selectSignal(select.selectDeletingId)()).toBeNull();
    http.expectOne((r) => r.method === 'GET').flush(pageOf([]));
  });

  it('SRC-13 the zero-result log loads most frequent first, paged', () => {
    store.dispatch(Ui.missesOpened());
    http.expectOne(url('/misses')).flush(pageOf([missRow(), missRow({ text: 'tutu', count: 1 })], { totalCount: 12 }));

    expect(store.selectSignal(select.selectMisses)().map((m) => m.text)).toEqual(['ghagra', 'tutu']);
    expect(store.selectSignal(select.selectMissesTotal)()).toBe(12);

    store.dispatch(Ui.missesPageChanged({ page: 2 }));
    expect(http.expectOne(url('/misses')).request.params.get('page')).toBe('2');
    store.dispatch(Api.loadMissesFailed({ error: { status: 500, message: 'down', fieldErrors: {} } }));
    expect(store.selectSignal(select.selectMissesError)()?.message).toBe('down');
  });
});
