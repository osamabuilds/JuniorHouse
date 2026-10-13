import { printJob } from '@app/testing/labelling-fixtures';
import { PrintJobsApiActions, PrintJobsPageActions } from './print-jobs.actions';
import { printJobsReducer } from './print-jobs.reducer';
import { initialPrintJobsState } from './print-jobs.state';

describe('printJobsReducer (spec PJ-2, PJ-3, PJ-5, PJ-6, PJ-13)', () => {
  it('opening the whole queue starts clean; opening with a variant shows that SKU\'s history', () => {
    const queue = printJobsReducer({ ...initialPrintJobsState, status: 'failed', page: 3, notice: 'x' }, PrintJobsPageActions.opened({ variantId: null, reprintSku: null, reprint: false }));
    expect(queue).toMatchObject({ status: 'all', page: 1, notice: null, variantId: null, reprintVariantId: null, loading: true });

    const history = printJobsReducer(initialPrintJobsState, PrintJobsPageActions.opened({ variantId: 31, reprintSku: 'ROMP-X', reprint: false }));
    expect(history).toMatchObject({ variantId: 31, reprintVariantId: null });
  });

  it('opening with reprint=1 opens the reprint form for that variant and shows its SKU (PJ-5)', () => {
    const state = printJobsReducer(initialPrintJobsState, PrintJobsPageActions.opened({ variantId: 31, reprintSku: 'ROMP-X', reprint: true }));

    expect(state.reprintVariantId).toBe(31);
    expect(state.reprintSku).toBe('ROMP-X');
  });

  it('filters go back to page one; the auto refresh does not flash the loading state', () => {
    const state = printJobsReducer({ ...initialPrintJobsState, page: 4 }, PrintJobsPageActions.statusChanged({ status: 'failed' }));
    expect(state).toMatchObject({ status: 'failed', page: 1, loading: true });

    const ticked = printJobsReducer(initialPrintJobsState, PrintJobsPageActions.autoRefreshTicked());
    expect(ticked.loading).toBe(false);
  });

  it('clearing the history goes back to the whole queue', () => {
    const state = printJobsReducer({ ...initialPrintJobsState, variantId: 31, reprintVariantId: 31, reprintSku: 'S', page: 2 }, PrintJobsPageActions.historyCleared());

    expect(state).toMatchObject({ variantId: null, reprintVariantId: null, reprintSku: null, page: 1, loading: true });
  });

  it('retry and cancel mark the job busy, then report what happened (PJ-3)', () => {
    const busy = printJobsReducer(initialPrintJobsState, PrintJobsPageActions.retrySubmitted({ id: 3, actingPerson: 'Asha' }));
    expect(busy.busyJobId).toBe(3);

    const done = printJobsReducer(busy, PrintJobsApiActions.jobActionSucceeded({ job: printJob({ jobNo: 'PJ-2026-00003' }), verb: 'is back in the queue' }));
    expect(done.busyJobId).toBeNull();
    expect(done.notice).toBe('Job PJ-2026-00003 is back in the queue.');
    expect(done.loading).toBe(true);

    const failed = printJobsReducer(busy, PrintJobsApiActions.jobActionFailed({ error: { status: 409, message: 'Only a queued or failed print job can be cancelled.', fieldErrors: {} } }));
    expect(failed.busyJobId).toBeNull();
    expect(failed.error?.message).toContain('cancelled');
  });

  it('a reprint shows saving, closes on success with a notice, and stays open with the reason on failure (PJ-5, PJ-7)', () => {
    const open = { ...initialPrintJobsState, reprintVariantId: 31, reprintSku: 'ROMP-X' };
    const saving = printJobsReducer(open, PrintJobsPageActions.reprintSubmitted({ variantId: 31, value: { qty: 2, reasonId: 2, note: null, printerCode: null, actingPerson: 'Asha' } }));
    expect(saving.reprintSaving).toBe(true);

    const done = printJobsReducer(saving, PrintJobsApiActions.reprintSucceeded({ job: printJob({ jobNo: 'PJ-2026-00004', kindCode: 'Reprint', qty: 2 }) }));
    expect(done.reprintVariantId).toBeNull();
    expect(done.reprintSaving).toBe(false);
    expect(done.notice).toBe('Reprint queued as job PJ-2026-00004 (2 tags, same barcode).');

    const refused = printJobsReducer(saving, PrintJobsApiActions.reprintFailed({ error: { status: 409, message: 'The GTIN for ROMP-X has been voided.', fieldErrors: {} } }));
    expect(refused.reprintVariantId).toBe(31);
    expect(refused.reprintSaving).toBe(false);
    expect(refused.reprintError?.message).toContain('voided');
  });

  it('keeps the page of jobs the API returned', () => {
    const state = printJobsReducer(
      { ...initialPrintJobsState, loading: true },
      PrintJobsApiActions.loadJobsSucceeded({ result: { items: [printJob(), printJob({ id: 4 })], page: 1, pageSize: 10, totalCount: 2, totalPages: 1 } }),
    );

    expect(state.jobs).toHaveLength(2);
    expect(state.total).toBe(2);
    expect(state.loading).toBe(false);
  });
});
