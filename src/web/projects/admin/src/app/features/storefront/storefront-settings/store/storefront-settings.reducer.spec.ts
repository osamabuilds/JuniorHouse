import { ApiError } from '@core/http';
import { contentPage, draftSettings, pageRow, snapshotRow } from '@app/testing/storefront-fixtures';
import { pageOf } from '@app/testing/warehouse-fixtures';
import { StorefrontSettingsApiActions as Api, StorefrontSettingsPageActions as Ui } from './storefront-settings.actions';
import { storefrontSettingsReducer } from './storefront-settings.reducer';
import { initialStorefrontSettingsState } from './storefront-settings.state';

const conflict: ApiError = {
  status: 409,
  message: 'Someone else changed the settings since you opened them. Reload and apply your change again.',
  fieldErrors: {},
};

describe('storefrontSettingsReducer (spec ST-3, ST-14, ST-17)', () => {
  it('opening a settings screen reloads the draft and clears old messages', () => {
    const state = storefrontSettingsReducer({ ...initialStorefrontSettingsState, notice: 'old', saveError: conflict }, Ui.draftOpened());

    expect(state).toMatchObject({ loading: true, notice: null, saveError: null });
  });

  it('keeps the loaded draft with its version, which the next save quotes (ST-14)', () => {
    const state = storefrontSettingsReducer({ ...initialStorefrontSettingsState, loading: true }, Api.loadDraftSucceeded({ draft: draftSettings({ version: 7 }) }));

    expect(state.draft?.version).toBe(7);
    expect(state.loading).toBe(false);
  });

  it('a saved part replaces just that part and moves the version on', () => {
    const draft = draftSettings({ version: 4 });
    const start = { ...initialStorefrontSettingsState, draft, saving: true };

    const state = storefrontSettingsReducer(start, Api.saveSucceeded({ version: 5, patch: { popularSearches: ['Frocks'] } }));

    expect(state.draft).toMatchObject({ version: 5, popularSearches: ['Frocks'] });
    expect(state.draft?.navigation).toEqual(draft.navigation);
    expect(state.saving).toBe(false);
    expect(state.notice).toContain('publish');
  });

  it('a refused save keeps what was loaded and shows the server reason, such as a conflict (ST-14)', () => {
    const start = { ...initialStorefrontSettingsState, draft: draftSettings(), saving: true };

    const state = storefrontSettingsReducer(start, Api.saveFailed({ error: conflict }));

    expect(state).toMatchObject({ saving: false, saveError: conflict });
    expect(state.draft).toBe(start.draft);
  });

  it('filtering the page list goes back to the first page and reloads', () => {
    const state = storefrontSettingsReducer({ ...initialStorefrontSettingsState, pagesPage: 3 }, Ui.pagesStatusFiltered({ status: 'Published' }));

    expect(state).toMatchObject({ pagesStatus: 'Published', pagesPage: 1, pagesLoading: true });
  });

  it('keeps the page of pages and the total', () => {
    const state = storefrontSettingsReducer(initialStorefrontSettingsState, Api.loadPagesSucceeded({ result: pageOf([pageRow()], { totalCount: 31 }) }));

    expect(state).toMatchObject({ pagesTotal: 31, pagesLoading: false });
    expect(state.pages).toHaveLength(1);
  });

  it('opening the editor for a new page starts blank, for an existing one it waits for the page', () => {
    const blank = storefrontSettingsReducer({ ...initialStorefrontSettingsState, page: contentPage() }, Ui.pageEditorOpened({ id: null }));
    expect(blank).toMatchObject({ page: null, pageLoading: false });

    const existing = storefrontSettingsReducer(blank, Ui.pageEditorOpened({ id: 5 }));
    expect(existing.pageLoading).toBe(true);
  });

  it('a saved page is kept with its new row version, and a created one says so', () => {
    const created = storefrontSettingsReducer(
      { ...initialStorefrontSettingsState, pageSaving: true },
      Api.savePageSucceeded({ page: contentPage({ rowVersion: 12 }), created: true }),
    );

    expect(created.page?.rowVersion).toBe(12);
    expect(created).toMatchObject({ pageSaving: false, pageNotice: 'The page was created as a draft.' });
  });

  it('a page that cannot go live shows the reason and stays as it was (ST-17)', () => {
    const reason: ApiError = {
      status: 400,
      message: 'The page quotes "exchange.conditions", which is still empty.',
      fieldErrors: { 'pages.exchanges': ['still empty'] },
    };
    const start = { ...initialStorefrontSettingsState, page: contentPage(), pageSaving: true };

    const state = storefrontSettingsReducer(start, Api.savePageFailed({ error: reason }));

    expect(state.pageError).toBe(reason);
    expect(state.page).toBe(start.page);
  });

  it('publishing reports a new version, or that nothing changed (ST-3)', () => {
    const published = storefrontSettingsReducer(
      { ...initialStorefrontSettingsState, publishing: true },
      Api.publishSucceeded({ result: { versionNo: 4, documentHash: 'x', created: true } }),
    );
    expect(published.publishNotice).toContain('version 4');
    expect(published.snapshotsLoading).toBe(true);

    const same = storefrontSettingsReducer(published, Api.publishSucceeded({ result: { versionNo: 4, documentHash: 'x', created: false } }));
    expect(same.publishNotice).toContain('Nothing has changed');
  });

  it('a refused publish keeps the field-by-field report for the screen (ST-3, ST-4)', () => {
    const report: ApiError = {
      status: 400,
      message: 'The page "delivery" does not exist or is not published.',
      fieldErrors: { 'footer.Help.Delivery': ['The page "delivery" does not exist or is not published.'] },
    };

    const state = storefrontSettingsReducer({ ...initialStorefrontSettingsState, publishing: true }, Api.publishFailed({ error: report }));

    expect(state).toMatchObject({ publishing: false, publishError: report });
  });

  it('restoring marks that version while it runs, then reports the new current version (ST-12)', () => {
    const running = storefrontSettingsReducer(initialStorefrontSettingsState, Ui.restoreSubmitted({ versionNo: 2, note: '' }));
    expect(running.restoringVersion).toBe(2);

    const done = storefrontSettingsReducer(running, Api.restoreSucceeded({ result: { versionNo: 5, documentHash: 'x', created: true } }));
    expect(done.restoringVersion).toBeNull();
    expect(done.publishNotice).toContain('version 5');
  });

  it('keeps the page of versions and the total', () => {
    const state = storefrontSettingsReducer(initialStorefrontSettingsState, Api.loadSnapshotsSucceeded({ result: pageOf([snapshotRow()], { totalCount: 14 }) }));

    expect(state).toMatchObject({ snapshotsTotal: 14, snapshotsLoading: false });
  });
});
