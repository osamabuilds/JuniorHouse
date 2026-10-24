import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { contentPage, draftSettings } from '@app/testing/storefront-fixtures';
import { StorefrontSettingsPageActions as Ui } from './storefront-settings.actions';
import * as effects from './storefront-settings.effects';
import { storefrontSettingsReducer } from './storefront-settings.reducer';
import { selectDraft, selectPage, selectPublishError, selectSaveError } from './storefront-settings.selectors';
import { STOREFRONT_SETTINGS_FEATURE_KEY } from './storefront-settings.state';

const ROOT = '/api/admin/storefront-settings';
const emptyPage = { items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0 };

describe('storefront settings effects (spec ST-14, ST-3, ST-12)', () => {
  let store: Store;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
        provideStore({ [STOREFRONT_SETTINGS_FEATURE_KEY]: storefrontSettingsReducer }),
        provideEffects(effects),
      ],
    });
    store = TestBed.inject(Store);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const url = (path: string) => (r: { url: string }) => r.url.endsWith(`${ROOT}${path}`);

  function loadDraft(version = 4) {
    store.dispatch(Ui.draftOpened());
    http.expectOne(url('/draft')).flush(draftSettings({ version }));
  }

  it('loads the draft when a settings screen opens', () => {
    loadDraft(7);

    expect(store.selectSignal(selectDraft)()?.version).toBe(7);
  });

  it('saves a part quoting the version that was loaded, then keeps the new version (ST-14)', () => {
    loadDraft(4);

    store.dispatch(Ui.saveSubmitted({ part: { part: 'ages', steps: [] } }));
    const request = http.expectOne(url('/ages'));
    expect(request.request.body).toEqual({ expectedVersion: 4, steps: [] });
    request.flush({ version: 5, data: [] });

    expect(store.selectSignal(selectDraft)()?.version).toBe(5);
  });

  it('a stale save comes back as a conflict the screen can show (ST-14)', () => {
    loadDraft(4);

    store.dispatch(Ui.saveSubmitted({ part: { part: 'footer', columns: [] } }));
    http
      .expectOne(url('/footer'))
      .flush({ title: 'Conflict', detail: 'Someone else changed the settings since you opened them.' }, { status: 409, statusText: 'Conflict' });

    const error = store.selectSignal(selectSaveError)();
    expect(error?.status).toBe(409);
    expect(error?.message).toContain('Someone else');
    expect(store.selectSignal(selectDraft)()?.version).toBe(4);
  });

  it('creates a page that has none yet, and updates a loaded page with its row version (ST-14)', () => {
    const value = { slug: 'care', title: 'Care', metaDescription: 'D', blocks: [] };
    store.dispatch(Ui.pageEditorOpened({ id: null }));
    store.dispatch(Ui.pageSaveSubmitted({ value }));
    const create = http.expectOne(url('/pages'));
    expect(create.request.method).toBe('POST');
    create.flush(contentPage({ id: 8, rowVersion: 3 }));
    expect(store.selectSignal(selectPage)()?.rowVersion).toBe(3);

    store.dispatch(Ui.pageSaveSubmitted({ value }));
    const update = http.expectOne(url('/pages/8'));
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toMatchObject({ rowVersion: 3 });
    update.flush(contentPage({ id: 8, rowVersion: 4 }));
    expect(store.selectSignal(selectPage)()?.rowVersion).toBe(4);
  });

  it('changes a page status with the current row version (ST-17)', () => {
    store.dispatch(Ui.pageEditorOpened({ id: 1 }));
    http.expectOne(url('/pages/1')).flush(contentPage({ rowVersion: 9 }));

    store.dispatch(Ui.pageStatusSubmitted({ status: 'Published' }));
    const request = http.expectOne(url('/pages/1/status'));
    expect(request.request.body).toEqual({ rowVersion: 9, status: 'Published' });
    request.flush(contentPage({ rowVersion: 10, status: 'Published' }));

    expect(store.selectSignal(selectPage)()).toMatchObject({ status: 'Published', rowVersion: 10 });
  });

  it('publishes with the loaded draft version, then reloads the versions (ST-3)', () => {
    store.dispatch(Ui.publishOpened());
    http.expectOne(url('/draft')).flush(draftSettings({ version: 6 }));
    http.expectOne(url('/snapshots')).flush(emptyPage);

    store.dispatch(Ui.publishSubmitted({ note: 'March' }));
    const publish = http.expectOne(url('/publish'));
    expect(publish.request.body).toEqual({ expectedVersion: 6, note: 'March' });
    publish.flush({ versionNo: 1, documentHash: 'x', created: true });

    http.expectOne(url('/snapshots')).flush(emptyPage);
  });

  it('a publish the server refuses leaves the reason, field by field, for the screen (ST-3)', () => {
    store.dispatch(Ui.publishOpened());
    http.expectOne(url('/draft')).flush(draftSettings());
    http.expectOne(url('/snapshots')).flush(emptyPage);

    store.dispatch(Ui.publishSubmitted({ note: '' }));
    http
      .expectOne(url('/publish'))
      .flush({ errors: { 'pages.exchanges': ['The page quotes "exchange.conditions", which is still empty.'] } }, { status: 400, statusText: 'Bad Request' });

    expect(store.selectSignal(selectPublishError)()?.fieldErrors['pages.exchanges']).toHaveLength(1);
  });
});
