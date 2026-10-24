import { createReducer, on } from '@ngrx/store';
import { StorefrontSettingsApiActions as Api, StorefrontSettingsPageActions as Ui } from './storefront-settings.actions';
import { initialStorefrontSettingsState } from './storefront-settings.state';

export const storefrontSettingsReducer = createReducer(
  initialStorefrontSettingsState,

  // The draft screens share one stored draft. Opening one reloads it so the version quoted on Save is the current one (ST-14).
  on(Ui.draftOpened, (state) => ({ ...state, loading: true, loadError: null, saveError: null, notice: null })),
  on(Api.loadDraftSucceeded, (state, { draft }) => ({ ...state, draft, loading: false })),
  on(Api.loadDraftFailed, (state, { error }) => ({ ...state, loading: false, loadError: error })),

  on(Ui.saveSubmitted, (state) => ({ ...state, saving: true, saveError: null, notice: null })),
  on(Api.saveSucceeded, (state, { version, patch }) => ({
    ...state,
    saving: false,
    notice: 'Saved. It goes live when you publish.',
    draft: state.draft ? { ...state.draft, ...patch, version } : state.draft,
  })),
  on(Api.saveFailed, (state, { error }) => ({ ...state, saving: false, saveError: error })),
  on(Ui.noticeDismissed, (state) => ({ ...state, notice: null, pageNotice: null, publishNotice: null })),

  // Pages list.
  on(Ui.pagesOpened, (state) => ({ ...state, pages: [], pagesTotal: 0, pagesPage: 1, pagesStatus: '', pagesLoading: true, pagesError: null })),
  on(Ui.pagesStatusFiltered, (state, { status }) => ({ ...state, pagesStatus: status, pagesPage: 1, pagesLoading: true, pagesError: null })),
  on(Ui.pagesPageChanged, (state, { page }) => ({ ...state, pagesPage: page, pagesLoading: true, pagesError: null })),
  on(Ui.pagesPageSizeChanged, (state, { pageSize }) => ({ ...state, pagesPageSize: pageSize, pagesPage: 1, pagesLoading: true, pagesError: null })),
  on(Api.loadPagesSucceeded, (state, { result }) => ({
    ...state,
    pages: result.items,
    pagesTotal: result.totalCount,
    pagesPage: result.page,
    pagesLoading: false,
  })),
  on(Api.loadPagesFailed, (state, { error }) => ({ ...state, pagesLoading: false, pagesError: error })),

  // Page editor.
  // Arriving at a page that was just created keeps it and its "created" notice; any other page starts clean and waits for the load.
  on(Ui.pageEditorOpened, (state, { id }) =>
    id !== null && state.page?.id === id
      ? { ...state, pageLoading: false, pageError: null }
      : { ...state, page: null, pageLoading: id !== null, pageSaving: false, pageError: null, pageNotice: null }),
  on(Api.loadPageSucceeded, (state, { page }) => ({ ...state, page, pageLoading: false })),
  on(Api.loadPageFailed, (state, { error }) => ({ ...state, pageLoading: false, pageError: error })),
  on(Ui.pageSaveSubmitted, Ui.pageStatusSubmitted, (state) => ({ ...state, pageSaving: true, pageError: null, pageNotice: null })),
  on(Api.savePageSucceeded, (state, { page, created }) => ({
    ...state,
    page,
    pageSaving: false,
    pageNotice: created ? 'The page was created as a draft.' : 'Saved.',
  })),
  on(Api.savePageFailed, (state, { error }) => ({ ...state, pageSaving: false, pageError: error })),

  // Publish and versions.
  on(Ui.publishOpened, (state) => ({
    ...state,
    snapshots: [],
    snapshotsTotal: 0,
    snapshotsPage: 1,
    snapshotsLoading: true,
    publishing: false,
    restoringVersion: null,
    publishError: null,
    publishNotice: null,
    loading: true,
    loadError: null,
  })),
  on(Ui.snapshotsPageChanged, (state, { page }) => ({ ...state, snapshotsPage: page, snapshotsLoading: true })),
  on(Api.loadSnapshotsSucceeded, (state, { result }) => ({
    ...state,
    snapshots: result.items,
    snapshotsTotal: result.totalCount,
    snapshotsPage: result.page,
    snapshotsLoading: false,
  })),
  on(Api.loadSnapshotsFailed, (state, { error }) => ({ ...state, snapshotsLoading: false, publishError: error })),
  on(Ui.publishSubmitted, (state) => ({ ...state, publishing: true, publishError: null, publishNotice: null })),
  on(Api.publishSucceeded, (state, { result }) => ({
    ...state,
    publishing: false,
    snapshotsLoading: true,
    publishNotice: result.created
      ? `Published as version ${result.versionNo}. The shop shows it within a minute.`
      : `Nothing has changed since version ${result.versionNo}, so nothing new was published.`,
  })),
  on(Api.publishFailed, (state, { error }) => ({ ...state, publishing: false, publishError: error })),
  on(Ui.restoreSubmitted, (state, { versionNo }) => ({ ...state, restoringVersion: versionNo, publishError: null, publishNotice: null })),
  on(Api.restoreSucceeded, (state, { result }) => ({
    ...state,
    restoringVersion: null,
    snapshotsLoading: true,
    publishNotice: result.created
      ? `Version restored and published as version ${result.versionNo}.`
      : `Version ${result.versionNo} is already the one on the shop.`,
  })),
  on(Api.restoreFailed, (state, { error }) => ({ ...state, restoringVersion: null, publishError: error })),
);
