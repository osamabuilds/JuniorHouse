import { createReducer, on } from '@ngrx/store';
import { PrintJobsApiActions, PrintJobsPageActions } from './print-jobs.actions';
import { initialPrintJobsState } from './print-jobs.state';

export const printJobsReducer = createReducer(
  initialPrintJobsState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(PrintJobsPageActions.opened, (_state, { variantId, reprintSku, reprint }) => ({
    ...initialPrintJobsState,
    loading: true,
    variantId,
    reprintVariantId: reprint ? variantId : null,
    reprintSku: reprint ? reprintSku : null,
  })),
  on(PrintJobsPageActions.statusChanged, (state, { status }) => ({ ...state, status, page: 1, loading: true, error: null })),
  on(PrintJobsPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(PrintJobsPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(PrintJobsPageActions.refreshRequested, (state) => ({ ...state, loading: true, error: null })),
  on(PrintJobsPageActions.historyCleared, (state) => ({
    ...state,
    variantId: null,
    reprintVariantId: null,
    reprintSku: null,
    page: 1,
    loading: true,
  })),
  on(PrintJobsPageActions.retrySubmitted, PrintJobsPageActions.cancelSubmitted, (state, { id }) => ({
    ...state,
    busyJobId: id,
    error: null,
    notice: null,
  })),
  on(PrintJobsPageActions.reprintFormClosed, (state) => ({
    ...state,
    reprintVariantId: null,
    reprintSku: null,
    reprintSaving: false,
    reprintError: null,
  })),
  on(PrintJobsPageActions.reprintSubmitted, (state) => ({ ...state, reprintSaving: true, reprintError: null })),
  on(PrintJobsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(PrintJobsApiActions.loadJobsSucceeded, (state, { result }) => ({
    ...state,
    jobs: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(PrintJobsApiActions.loadJobsFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(PrintJobsApiActions.loadPrintersSucceeded, (state, { printers }) => ({ ...state, printers })),
  on(PrintJobsApiActions.loadPrintersFailed, (state, { error }) => ({ ...state, error })),
  on(PrintJobsApiActions.jobActionSucceeded, (state, { job, verb }) => ({
    ...state,
    busyJobId: null,
    loading: true,
    notice: `Job ${job.jobNo} ${verb}.`,
  })),
  on(PrintJobsApiActions.jobActionFailed, (state, { error }) => ({ ...state, busyJobId: null, error })),
  on(PrintJobsApiActions.reprintSucceeded, (state, { job }) => ({
    ...state,
    reprintVariantId: null,
    reprintSku: null,
    reprintSaving: false,
    reprintError: null,
    loading: true,
    notice: `Reprint queued as job ${job.jobNo} (${job.qty} tag${job.qty === 1 ? '' : 's'}, same barcode).`,
  })),
  on(PrintJobsApiActions.reprintFailed, (state, { error }) => ({ ...state, reprintSaving: false, reprintError: error })),
);
