import { CaseDetailActions, CasesPageActions, ReverseQcApiActions, initialReverseQcState, reverseQcReducer } from './reverse-qc.store';
import { ReturnCaseDetail } from '../models';

const detail: ReturnCaseDetail = {
  id: 4, orderId: 9, orderNumber: 'RMP-9', status: 'IN_INSPECTION', sealIntact: false, receivingBinCode: 'A-01-01', receivedBy: 'Ayesha', receivedAt: '2026-11-10T09:00:00Z',
  completedBy: null, completedAt: null, openedAt: '2026-11-10T08:00:00Z', lines: [],
};

describe('Reverse QC store (spec RQC-2 to RQC-8, RQC-11)', () => {
  it('RQC-11 opening the list starts clean and loading; a page of cases fills it and ends loading', () => {
    const loading = reverseQcReducer({ ...initialReverseQcState, status: 'COMPLETED' }, CasesPageActions.opened());
    const done = reverseQcReducer(loading, ReverseQcApiActions.casesSucceeded({ result: { items: [], totalCount: 7, page: 2, pageSize: 20 } as never }));

    expect(loading.loading).toBe(true);
    expect(loading.status).toBe('');
    expect(done.total).toBe(7);
    expect(done.page).toBe(2);
    expect(done.loading).toBe(false);
  });

  it('RQC-11 a new status filter returns the list to page 1', () => {
    const paged = reverseQcReducer(initialReverseQcState, CasesPageActions.pageChanged({ page: 3 }));
    const filtered = reverseQcReducer(paged, CasesPageActions.statusChanged({ status: 'IN_INSPECTION' }));

    expect(paged.page).toBe(3);
    expect(filtered.page).toBe(1);
    expect(filtered.status).toBe('IN_INSPECTION');
  });

  it('RQC-2, RQC-4, RQC-8 receiving, deciding and completing make the page wait, and the answer frees it with a notice', () => {
    const receiving = reverseQcReducer(initialReverseQcState, CaseDetailActions.receiveSubmitted({ id: 4, request: { sealIntact: true, binCode: 'A-01-01' } }));
    const deciding = reverseQcReducer(initialReverseQcState, CaseDetailActions.lineDecided({ id: 4, lineId: 1, request: { decision: 'RESTOCK', quantity: 1, note: null } }));
    const completing = reverseQcReducer(initialReverseQcState, CaseDetailActions.completeSubmitted({ id: 4 }));
    const done = reverseQcReducer(receiving, ReverseQcApiActions.caseSucceeded({ detail, notice: 'Received. Now decide each line.' }));

    expect([receiving.busy, deciding.busy, completing.busy]).toEqual([true, true, true]);
    expect(done.busy).toBe(false);
    expect(done.detail?.status).toBe('IN_INSPECTION');
    expect(done.notice).toContain('Received');
  });

  it('RQC-7, RQC-9 a refusal frees the page and keeps the plain message', () => {
    const failed = reverseQcReducer({ ...initialReverseQcState, busy: true }, ReverseQcApiActions.requestFailed({ error: { status: 409, message: '2 lines are still waiting for a decision.' } as never }));

    expect(failed.busy).toBe(false);
    expect(failed.error?.message).toContain('still waiting');
  });
});
